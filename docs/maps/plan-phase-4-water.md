# Phase 4 — Water System

> **Status:** Not Started  
> **Depends on:** Phase 1 (TileData has WaterLevel)  
> **Unlocks:** Phase 5 (visual polish)

---

## Goal

Render water as translucent visual planes at configurable levels. Water remains non-walkable (visual only per doc 04 decision). Add shore detection for future texture blending.

---

## Steps

### Step 4.1 — Water presence and depth helpers

**File:** `src/Oravey2.Core/World/WaterHelper.cs` (new)

```
WaterHelper (static)
├── HasWater(TileData) → bool               // WaterLevel > HeightLevel
├── GetDepth(TileData) → int                // WaterLevel - HeightLevel (0 if dry)
├── GetWaterSurfaceY(TileData) → float      // WaterLevel * 0.25f
├── IsShore(TileMapData, x, y) → bool       // has water AND at least 1 dry neighbor
├── GetShoreDirections(TileMapData, x, y) → Direction[]  // which neighbors are dry
```

**Tests:** `WaterHelperTests.cs`
- Dry tile (WaterLevel=0, HeightLevel=1) → no water, depth 0
- Shallow (WaterLevel=2, HeightLevel=1) → water, depth 1, surface Y=0.5
- Deep (WaterLevel=6, HeightLevel=1) → water, depth 5, surface Y=1.5
- Shore: water tile next to dry tile → IsShore true
- Interior water: surrounded by water → IsShore false
- Shore directions: correct neighbor identification

---

### Step 4.2 — Water plane rendering

**File:** `src/Oravey2.Core/World/TileMapRendererScript.cs` (modify)

After rendering terrain tiles, add a second pass for water:
1. For each tile where `HasWater` is true
2. Create a flat quad (plane) at Y = `WaterLevel * 0.25f`
3. Apply translucent blue material (alpha = 0.6)
4. Water plane slightly larger than tile (0.98 × TileSize) to connect adjacent water tiles seamlessly

**Tests (unit — logic only):**
- `WaterRenderDataTests.cs`
- Water tile produces render data with correct Y position
- Dry tile produces no water render data
- Multiple adjacent water tiles at same WaterLevel → planes at same Y

---

### Step 4.3 — River helper (connected water path)

**File:** `src/Oravey2.Core/World/WaterHelper.cs` (extend)

```
FindConnectedWater(TileMapData, startX, startY) → HashSet<(int,int)>
```

Flood-fill from a water tile to find all connected water tiles. Used for:
- Lake detection (connected region, same WaterLevel)
- River detection (connected region, varying WaterLevel)

**Tests:** `ConnectedWaterTests.cs`
- Single water tile → set of 1
- 3×3 water block → set of 9
- L-shaped river → correct connected set
- Two separate water bodies → separate sets
- Diagonal water tiles NOT connected (4-directional flood fill)

---

### Step 4.4 — Shore tile classification

**File:** `src/Oravey2.Core/World/WaterHelper.cs` (extend)

For each shore tile, determine its shore configuration for future texture blending:

```
GetShoreConfig(TileMapData, x, y) → ShoreConfig
```

```csharp
public readonly record struct ShoreConfig(
    bool North, bool East, bool South, bool West,
    bool NorthEast, bool SouthEast, bool SouthWest, bool NorthWest)
{
    // true = that neighbor is dry land (shore faces that direction)
}
```

**Tests:** `ShoreConfigTests.cs`
- Water tile with dry North → ShoreConfig.North = true, others false
- Water tile in corner (dry North + East + NE) → correct config
- Interior water → all false
- Dry tile → not a shore, returns default

---

### Step 4.5 — Test map with water features

**File:** `tests/Oravey2.Tests/Fixtures/Maps/test_water/` (new)

Small fixture with:
- A 3-tile wide river running North–South
- A 4×4 lake in one corner
- Shore tiles at all edges
- A dry island in the middle of the lake

**Tests:** `WaterMapFixtureTests.cs`
- Load fixture → river tiles have water
- River tiles connected via flood fill
- Lake tiles connected via flood fill
- Shore tiles correctly identified at river/lake edges
- Dry island is not water
- All water tiles are non-walkable

---

## Acceptance Criteria

| # | Criteria | How to Verify |
|---|---------|--------------|
| 1 | Water presence/depth correctly computed from TileData | Unit tests |
| 2 | Shore detection identifies water boundary tiles | Unit tests |
| 3 | Connected water flood-fill finds correct regions | Unit tests |
| 4 | ShoreConfig correctly classifies all 8-neighbor configurations | Unit tests |
| 5 | Water planes render at correct Y position (visual check) | Manual + render data test |
| 6 | All water tiles remain non-walkable | Existing walkability tests |
| 7 | All existing tests pass | `dotnet test` |
| 8 | 15+ new unit tests | Test count |

## Verification

```powershell
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Water" --verbosity normal
dotnet test tests/Oravey2.Tests --verbosity quiet
```
