# Generation Pipeline v2

## Overview

A redesigned end-to-end pipeline that runs inside the MapGen app, replacing
the current two-stage process (CLI tool → game-time generation) with a single
interactive workflow. The user picks a real-world region, the pipeline
downloads data, curates content via LLM, condenses town maps, generates unique
3D assets via Meshy, and writes a complete `world.db` ready for the game.

```
┌──────────────────────────────────────────────────────────────┐
│  MapGen App — Generation Pipeline v2                         │
│                                                              │
│  ① Region Select ──► ② SRTM Download ──► ③ OSM Download     │
│                                                              │
│  ④ Parse & Extract ──► ⑤ LLM Town Selection                 │
│                                                              │
│  ⑥ LLM Feature Design ──► ⑦ Town Map Condensation           │
│                                                              │
│  ⑧ Meshy 3D Assets ──► ⑨ World Generation ──► world.db      │
└──────────────────────────────────────────────────────────────┘
```

---

## Step ①  Region Selection

**What happens:** User picks a geographic region from the Geofabrik index tree.

**Existing code:** `GeofabrikService` → `GeofabrikIndex` → `RegionPickerDialog`
already does this. The picker returns a `GeofabrikRegion` with bounding box +
PBF download URL, which converts to a `RegionPreset` via `ToRegionPreset()`.

**No changes needed** — this step is already implemented.

---

## Step ②  SRTM Download

**What happens:** Elevation tiles for the selected bounding box are downloaded
from NASA Earthdata and stored as `.hgt.gz` files.

**Existing code:** `DataDownloadService.DownloadSrtmTilesAsync()` handles
tile enumeration, OAuth2 auth, download, extraction, and gzip caching.

**No changes needed** — triggered by a button on the pipeline page.

---

## Step ③  OSM Download

**What happens:** The OpenStreetMap PBF extract for the region is downloaded
from Geofabrik.

**Existing code:** `DataDownloadService.DownloadOsmExtractAsync()` downloads
the PBF atomically.

**No changes needed** — triggered by the same page, after SRTM.

---

## Step ④  Parse & Extract

**What happens:** SRTM and OSM data are parsed into in-memory structures.

**Existing code:**
- `SrtmParser.ParseHgtFile()` → `float[,]` elevation grid
- `OsmParser.ParsePbf()` → `OsmExtract` (towns, roads, water, railways, land use)
- `GeoMapper` converts lat/lon ↔ game-space XZ

### Do we still need `.regiontemplate` files?

**No.** The `.regiontemplate` was a serialised snapshot of parsed OSM + SRTM
data, designed for offline use and CLI-tool workflows. In the new pipeline:

- The MapGen app downloads and parses directly → the in-memory
  `RegionTemplate` object is passed straight to generation steps.
- There is no separate CLI tool that needs a pre-baked file.
- The parsed data is transient — what gets persisted is the final `world.db`.

The `RegionTemplate` **class** remains (it's the in-memory data model). The
binary file format and `RegionTemplateBuilder.Serialize/Deserialize` become
unused by the pipeline but can stay for debugging/export.

### What changes

- `FeatureCuller` is no longer needed as a manual user step. The LLM (step ⑤)
  replaces manual culling. We keep the culler as a pre-filter to reduce LLM
  token usage (drop hamlets < 50 pop, residential roads, tiny ponds).

---

## Step ⑤  LLM Town Selection

**What happens:** An LLM picks 8–15 towns from the full OSM extract and
assigns each a post-apocalyptic identity.

**Existing code:** `TownCurator` already does this — builds a prompt listing
all towns, asks the LLM to select and annotate them, parses the JSON response.

### What changes

The prompt is extended to also assign towns to **regions** (currently the code
assumes a single region per template). The LLM groups towns into 1–4 regions
based on geography, roads, and natural borders:

```
New prompt addition:
  Group the selected towns into 1–4 geographic regions.
  Each region should have 3–8 towns.
  Name each region with a post-apocalyptic name.
  Regions should follow natural boundaries (rivers, elevation changes, coastlines).

Response format:
[
  {
    "regionName": "The Flooded Coast",
    "towns": [
      { "gameName": "Rustwater", "realName": "Purmerend", ... },
      ...
    ]
  },
  ...
]
```

**Output:** `List<CuratedRegion>` with towns partitioned into regions.

### Validation

- Each region has ≥ 3 towns
- No two towns < 5 km apart within a region
- Every selected town maps to a real OSM `TownEntry`
- Total towns across all regions: 8–15
- At least one safe (threat ≤ 2) and one dangerous (threat ≥ 7) settlement

---

## Step ⑥  LLM Feature Design (per town)

**What happens:** For each curated town, the LLM designs its unique
characteristics — what makes this settlement interesting to visit.

**This is new.** Currently the pipeline generates generic buildings based only
on `TownCategory`. The new step asks the LLM to design each town's identity.

### Prompt per town

```
You are designing a town for a post-apocalyptic RPG.

Town: {gameName} (formerly {realName})
Role: {role}
Faction: {faction}
Threat level: {threatLevel}/10
Category: {townCategory} (pop: {population})
Description: {description}

Design the following for this town:

1. **Landmark building** — one signature structure that defines the town
   (e.g., "The Lighthouse Fortress", "Collapsed Cathedral turned market hall").
   Provide a short visual description for 3D generation.

2. **Key locations** (3–6) — places the player visits:
   - Name, purpose (shop, quest-giver, dungeon entrance, etc.)
   - Surface type (concrete, metal, wood)
   - Rough size category: small (1–2 tiles), medium (3–4), large (5–8)

3. **Town layout style** — how the town is organised:
   - "grid" (American-style blocks)
   - "radial" (ring roads around centre)
   - "organic" (medieval winding streets)
   - "linear" (along one road/canal)
   - "compound" (walled cluster)

4. **Environmental hazards** (0–2):
   - E.g., radiation zone, flooded district, collapsed overpass
   - Location hint (north edge, centre, etc.)

Respond with JSON only.
```

### Output: `TownDesign` (new record)

```csharp
public sealed record TownDesign(
    string TownName,
    LandmarkBuilding Landmark,
    List<KeyLocation> KeyLocations,
    string LayoutStyle,           // "grid", "radial", "organic", "linear", "compound"
    List<EnvironmentalHazard> Hazards
);

public sealed record LandmarkBuilding(
    string Name,
    string VisualDescription,     // fed to Meshy prompt
    string SizeCategory           // "medium", "large"
);

public sealed record KeyLocation(
    string Name,
    string Purpose,               // "shop", "quest", "dungeon", "bar", "clinic"
    SurfaceType Surface,
    string SizeCategory
);

public sealed record EnvironmentalHazard(
    string Type,                  // "radiation", "flood", "collapse", "fire"
    string LocationHint
);
```

### Storage

The `TownDesign` list is serialised as JSON into `world_meta` under key
`"town_designs"`, alongside the existing `"curated_plan"`.

---

## Step ⑦  Town Map Condensation

**What happens:** The real-world OSM road network and building footprints are
condensed into a game-scale town layout.

**Why condensation?** A real town might span 5 km × 5 km. The game represents
it in a handful of 16×16 m chunks (e.g., 8×8 chunks = 128×128 m). The full
OSM data must be squeezed into this footprint while keeping recognisable
street shapes and feature placement.

### Algorithm: `TownCondenser` (new class)

```
Input:
  - RegionTemplate (full OSM roads, water, land use)
  - CuratedTown (game position, boundary polygon)
  - TownDesign (layout style, key locations, hazards)

Steps:
  1. Clip — extract OSM features inside the town boundary polygon
  2. Scale — shrink the clipped geometry by a condensation factor:
       realBounds (metres) → gameBounds (chunk grid)
       factor = max(realWidth, realHeight) / targetSize
       targetSize = TownCategory → Hamlet: 48m, Village: 80m,
                    Town: 128m, City: 192m, Metropolis: 256m
  3. Prioritise roads — keep the N most important roads (by class)
     that form the street skeleton
  4. Simplify — Douglas-Peucker on each polyline at tolerance = 2m
  5. Place key locations — use road intersections + town centre as
     anchor points for KeyLocation placement
  6. Place landmark — put the LandmarkBuilding at the most central
     road intersection
  7. Apply hazards — mark tiles in hazard zones with appropriate flags

Output:
  - CondensedTownPlan {
      Vector2 Origin,              // game-space XZ of town NW corner
      int ChunksWide, ChunksHigh,  // town extent in chunks
      List<RoadSegment> Roads,     // condensed + simplified
      List<PlacedLocation> Locations,
      PlacedLandmark Landmark,
      List<HazardZone> Hazards
    }
```

### Condensation Factor Examples

| Category | Real extent | Game extent | Factor |
|----------|-----------|------------|--------|
| Hamlet | ~500 m | 48 m (3×3 chunks) | ~10× |
| Village | ~1 km | 80 m (5×5 chunks) | ~12× |
| Town | ~3 km | 128 m (8×8 chunks) | ~23× |
| City | ~8 km | 192 m (12×12 chunks) | ~42× |
| Metropolis | ~15 km | 256 m (16×16 chunks) | ~59× |

After condensation the road network still resembles the real layout but at a
walkable game scale.

---

## Step ⑧  Meshy 3D Assets

**What happens:** For each town's landmark building (and optionally key
locations), a unique 3D model is generated via the Meshy text-to-3D API.

**Existing code:** `MeshyClient` is fully implemented with text-to-3D,
remeshing, rigging, and download support.

### Workflow per landmark

```
1. Build prompt from LandmarkBuilding.VisualDescription:
     "Post-apocalyptic {description}, game asset, low-poly,
      Fallout style, rusty metal and concrete, {sizeHint}"

2. MeshyClient.CreateTextTo3DAsync(prompt, art_style: "realistic")
   → task ID

3. MeshyClient.StreamTextTo3DAsync(taskId)
   → poll until SUCCEEDED, report progress to UI

4. MeshyClient.CreateRemeshAsync(taskId, target_polycount: 5000)
   → optimise topology for real-time rendering

5. MeshyClient.DownloadModelAsync(model_urls["glb"])
   → save to data/regions/{name}/assets/{townName}_landmark.glb

6. Register asset path in TownDesign metadata
```

### Budget control

- **Per world:** max 15 landmarks × ~50 API credits = ~750 credits
- **Skip if offline:** fall back to generic `PrefabId` references
- **Cache:** if asset file exists, skip regeneration

### Asset Storage

GLB files stored under `data/regions/{regionName}/assets/`. The `world.db`
stores only the relative path in the entity_spawn `prefab_id` field:

```
prefab_id = "asset:noordholland/rustwater_landmark"
```

The game's asset loader resolves `asset:` prefixes to the file system.

---

## Step ⑨  World Generation → world.db

**What happens:** All preceding data is fed into the generators, producing a
complete SQLite `world.db`.

### Modified `WorldGenerator` flow

```csharp
public async Task GenerateAsync(
    RegionTemplate regionTemplate,     // parsed OSM + SRTM (not from file)
    List<CuratedRegion> curatedRegions,
    List<TownDesign> townDesigns,
    List<CondensedTownPlan> townPlans,
    Dictionary<string, string> meshyAssets,  // townName → glb path
    int seed,
    WorldMapStore store,
    CancellationToken ct)
{
    // 1. Store metadata
    store.GetOrSetMeta("curated_plan", Serialize(curatedRegions));
    store.GetOrSetMeta("town_designs", Serialize(townDesigns));
    store.GetOrSetMeta("world_seed", seed.ToString());

    // 2. Continent (L3)
    var continentData = new ContinentGenerator().Generate(regionTemplate);
    long continentId = store.InsertContinent(...);

    // 3. Regions (L2)
    foreach region:
        var regionData = new RegionGenerator().Generate(region, ...);
        long regionId = store.InsertRegion(...);

        // Roads + rivers as linear features
        var roads = new RoadSelector().Select(region, curated);
        var rivers = new RiverGenerator().Generate(region);
        store.InsertLinearFeature(...);

        // POIs for towns
        foreach town: store.InsertPoi(...);

    // 4. Town chunks (L1) — using condensed plans
    foreach townPlan:
        var townGen = new TownChunkGeneratorV2();
        for each chunk in townPlan grid:
            var result = townGen.Generate(chunk, townPlan, townDesign, meshyAssets);
            store.InsertChunk(regionId, result);

    // 5. Wilderness chunks (L1) — around towns + starting area
    foreach chunk not covered by a town:
        var result = new WildernessChunkGenerator().Generate(chunk, ...);
        store.InsertChunk(regionId, result);
}
```

### New: `TownChunkGeneratorV2`

Replaces the current `TownChunkGenerator` which only places random buildings.
The v2 generator uses the `CondensedTownPlan`:

1. **Road rasterisation** — draw condensed road polylines onto tile grid
   (same as current `ApplyRoadSkeleton` but using condensed geometry)
2. **Key location placement** — place buildings at designated positions from
   the condensed plan, with correct size and surface type
3. **Landmark placement** — place the landmark building entity with the Meshy
   asset path as `prefab_id`
4. **Hazard zone application** — set `TileFlags.Irradiated` or
   `WaterLevel > 0` for hazard areas
5. **Fill** — remaining walkable tiles get decay detail (rubble, cracks,
   vegetation regrowth)

---

## Pipeline State Machine

The pipeline runs as a stateful workflow in the MapGen app. Each step can be
re-run independently (e.g., re-roll town selection without re-downloading).

```
        ┌─────────────────────────────────────────────────┐
        │  PipelineState                                  │
        │                                                 │
  Idle ──► RegionSelected ──► SrtmReady ──► OsmReady     │
        │                                                 │
        │  ──► Parsed ──► TownsCurated ──► FeaturesDesigned
        │                                                 │
        │  ──► TownsCondensed ──► AssetsGenerated         │
        │                                                 │
        │  ──► WorldGenerated ──► Complete                │
        └─────────────────────────────────────────────────┘
```

Each state enables the next step's UI button. Intermediate results are held in
the `PipelineViewModel` and can be inspected/edited before proceeding.

### Re-roll points

| Step | Re-roll effect |
|------|---------------|
| ⑤ Town selection | Invalidates ⑥–⑨ |
| ⑥ Feature design | Invalidates ⑦–⑨ |
| ⑧ Meshy assets | Only regenerates changed assets |
| ⑨ Generation | Rewrites world.db (fast, ~seconds) |

---

## New Files to Create

| File | Purpose |
|------|---------|
| `src/Oravey2.MapGen/Generation/TownDesigner.cs` | Step ⑥ — LLM per-town feature design |
| `src/Oravey2.MapGen/Generation/TownDesign.cs` | Data records for step ⑥ output |
| `src/Oravey2.MapGen/Generation/TownCondenser.cs` | Step ⑦ — OSM → game-scale condensation |
| `src/Oravey2.MapGen/Generation/CondensedTownPlan.cs` | Data records for step ⑦ output |
| `src/Oravey2.MapGen/Generation/TownChunkGeneratorV2.cs` | Step ⑨ — chunk gen from condensed plan |
| `src/Oravey2.MapGen/Generation/MeshyAssetGenerator.cs` | Step ⑧ — orchestrates Meshy calls |
| `src/Oravey2.MapGen/ViewModels/PipelineViewModel.cs` | Full pipeline state + commands |
| `src/Oravey2.MapGen.App/Views/PipelineView.xaml` | Pipeline page UI |
| `src/Oravey2.MapGen.App/Views/PipelineView.xaml.cs` | Code-behind |

## Files to Modify

| File | Change |
|------|--------|
| `TownCurator.cs` | Extended prompt for region grouping |
| `WorldGenerator.cs` | Accept pre-curated data + condensed plans |
| `MauiProgram.cs` | Register `PipelineViewModel` |
| `MainPage.xaml` | Add Pipeline tab |

## Files Unchanged

| File | Why |
|------|-----|
| `RegionTemplateBuilder.cs` | Kept for debug/export but not used in pipeline |
| `DataDownloadService.cs` | Already handles SRTM + OSM |
| `OsmParser.cs`, `SrtmParser.cs`, `GeoMapper.cs` | Parsing layer unchanged |
| `MeshyClient.cs` | Already fully implemented |
| `WildernessChunkGenerator.cs` | Wilderness gen unchanged |
| `WorldMapStore.cs` | Storage API unchanged |

---

## Summary: Answering the Design Questions

| # | Question | Answer |
|---|----------|--------|
| 1 | Region selection | Geofabrik picker → `RegionPreset` (already works) |
| 2 | SRTM download | `DataDownloadService` (already works) |
| 3 | OSM download | `DataDownloadService` (already works) |
| 4 | Do we need `.regiontemplate`? | **No.** Parsed data stays in memory; `world.db` is the output. Keep the class, drop the file format from the pipeline. |
| 5 | LLM selects towns | `TownCurator` with extended prompt for region grouping |
| 6 | Towns in regions | LLM groups towns into 1–4 named regions in step ⑤ |
| 7 | LLM per-town features | **New** `TownDesigner` — landmark, key locations, layout style, hazards |
| 8 | Town map from OSM, condensed | **New** `TownCondenser` — clip, scale, simplify real OSM layout to game scale |
| 9 | Unique buildings via Meshy | **New** `MeshyAssetGenerator` — text-to-3D for each landmark, cached as GLB |
| 10 | Generators → game data | `WorldGenerator` v2 + **new** `TownChunkGeneratorV2` using condensed plans → `world.db` |
