# Step 02 — Feature Culling Engine

**Work streams:** WS-Culling (Pure-function culling logic)
**Depends on:** Step 01 (CullSettings, RoadClass, TownCategory)
**User-testable result:** Unit tests pass for town culling, road culling, water culling, and Douglas-Peucker line simplification.

---

## Goals

1. Implement a pure-function `FeatureCuller` class that reduces raw OSM features to a gameplay-relevant subset.
2. Town culling: category → population → protected categories → spacing → max cap.
3. Road culling: class → motorway protection → town proximity → dead-end removal → geometry simplification.
4. Water culling: area → river length → protected types.
5. Douglas-Peucker line simplification for road geometry.
6. Full unit test coverage.

---

## Problem

`OsmParser` extracts all features from the PBF — for Noord-Holland that's ~85 towns, ~1200 road segments, ~300 water bodies. The game only needs ~25 towns and ~350 roads. A culling step is needed between parsing and template building.

---

## Tasks

### 2.1 — FeatureCuller Static Class

File: `src/Oravey2.MapGen/WorldTemplate/FeatureCuller.cs`

- [ ] Create static class `FeatureCuller`
- [ ] All methods are pure functions — no side effects, no state

### 2.2 — CullTowns

```csharp
public static List<TownEntry> CullTowns(
    List<TownEntry> towns, CullSettings settings)
```

Algorithm (applied in order):
1. **Category filter** — remove towns below `settings.TownMinCategory`
2. **Population filter** — remove towns below `settings.TownMinPopulation`
3. **Protected categories** — restore any City (if `TownAlwaysKeepCities`) or Metropolis (if `TownAlwaysKeepMetropolis`) removed by step 2
4. **Sort** by `settings.TownPriority`:
   - `CullPriority.Population` → descending population
   - `CullPriority.Category` → descending category, then population
   - `CullPriority.Spacing` → descending population (spacing enforced next)
5. **Spacing enforcement** — greedy: iterate sorted list, skip any town within `TownMinSpacingKm` of an already-kept town. Use Haversine or flat-Earth approximation.
6. **Max cap** — keep at most `TownMaxCount` towns

- [ ] Implement distance calculation (reuse `GeoMapper` if possible, or simple Haversine)
- [ ] Return new list (do not mutate input)

### 2.3 — CullRoads

```csharp
public static List<RoadSegment> CullRoads(
    List<RoadSegment> roads,
    List<TownEntry> includedTowns,
    CullSettings settings)
```

Algorithm:
1. **Class filter** — remove roads below `settings.RoadMinClass`
2. **Motorway protection** — restore motorways if `RoadAlwaysKeepMotorways`
3. **Town proximity** — if `RoadKeepNearTowns`, include any road that has at least one point within `RoadTownProximityKm` of an included town
4. **Dead-end removal** — if `RoadRemoveDeadEnds`, remove road segments shorter than `RoadDeadEndMinKm` that connect to only one other road (degree-1 endpoints)
5. **Geometry simplification** — if `RoadSimplifyGeometry`, apply Douglas-Peucker to each road's point list with `RoadSimplifyToleranceM`

- [ ] Road-to-town distance: minimum distance from any road point to town lat/lon
- [ ] Dead-end detection: build adjacency from shared endpoints (within tolerance)
- [ ] Return new list with simplified geometry

### 2.4 — CullWater

```csharp
public static List<WaterBody> CullWater(
    List<WaterBody> water, CullSettings settings)
```

Algorithm:
1. **Area filter** — remove water bodies with area below `WaterMinAreaKm2`
2. **River length** — remove rivers/streams shorter than `WaterMinRiverLengthKm`
3. **Protected types** — restore sea/ocean if `WaterAlwaysKeepSea`, lakes if `WaterAlwaysKeepLakes`

- [ ] Check `WaterBody` for type/area/length fields; add if missing
- [ ] Return new list

### 2.5 — SimplifyLine (Douglas-Peucker)

```csharp
public static Vector2[] SimplifyLine(Vector2[] points, double toleranceMetres)
```

- [ ] Standard Douglas-Peucker algorithm
- [ ] Input: lat/lon points as Vector2 (X=lon, Y=lat) or game-space points
- [ ] Tolerance in metres — convert to degree-space internally if working in lat/lon
- [ ] Return simplified point array
- [ ] Edge case: if result would have fewer than 2 points, return start + end

### 2.6 — Unit Tests

File: `tests/Oravey2.Tests/WorldTemplate/FeatureCullerTests.cs`

**Town culling tests:**
- [ ] `CullTowns_BelowMinCategory_Removed` — hamlets removed when min is Village
- [ ] `CullTowns_BelowMinPopulation_Removed` — small towns filtered out
- [ ] `CullTowns_ProtectedCategory_NotRemoved` — City kept despite low population
- [ ] `CullTowns_SpacingEnforced_TooCloseRemoved` — two towns 2km apart, min spacing 5km → one removed
- [ ] `CullTowns_MaxCount_Honored` — 50 towns, max 10 → returns 10
- [ ] `CullTowns_EmptyInput_ReturnsEmpty`
- [ ] `CullTowns_AllKept_WhenSettingsPermissive` — min category Hamlet, min pop 0, max 999

**Road culling tests:**
- [ ] `CullRoads_BelowMinClass_Removed` — residential roads removed when min is Primary
- [ ] `CullRoads_Motorway_AlwaysKept` — motorway kept even below class filter
- [ ] `CullRoads_NearTown_Kept` — secondary road near included town kept
- [ ] `CullRoads_FarFromTown_Removed` — road 10km from any town, removed
- [ ] `CullRoads_DeadEnd_Removed` — short dead-end segment removed
- [ ] `CullRoads_GeometrySimplified` — road with 100 points simplified to fewer

**Water culling tests:**
- [ ] `CullWater_SmallArea_Removed` — pond below 0.1 km² removed
- [ ] `CullWater_ShortRiver_Removed` — stream below 2km removed
- [ ] `CullWater_Sea_AlwaysKept` — sea kept despite area filter

**Douglas-Peucker tests:**
- [ ] `SimplifyLine_StraightLine_ReturnsEndpoints` — collinear points → 2 points
- [ ] `SimplifyLine_ZigZag_KeepsPeaks` — zig-zag with peaks above tolerance kept
- [ ] `SimplifyLine_SinglePoint_ReturnsSame` — degenerate input handled
- [ ] `SimplifyLine_TwoPoints_ReturnsSame` — minimal input unchanged

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~FeatureCuller"
```

**User test:** All unit tests pass. No runtime verification — this is a pure computation step.
