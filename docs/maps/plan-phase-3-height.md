# Phase 3 — Height System

> **Status:** Not Started  
> **Depends on:** Phase 1 (TileData has HeightLevel)  
> **Unlocks:** Phase 5 (sub-tile rendering needs height meshes)

---

## Goal

Render tiles at different heights with slopes, cliffs, and height-aware pathfinding.

---

## Steps

### Step 3.1 — Height-aware mesh generation in renderer

**File:** `src/Oravey2.Core/World/TileMapRendererScript.cs` (modify)

Change `CreateTileEntity` to read `tileData.HeightLevel`:
- Tile Y position = `HeightLevel * 0.25f` (each level = 0.25m)
- Column mesh from Y=0 to Y=HeightLevel * 0.25f
- Top face is the walkable surface

**Tests:** `HeightRenderingTests.cs` (unit, pure logic)
- Height 0 tile at Y=0
- Height 4 tile at Y=1.0
- Height 10 tile at Y=2.5
- Wall on height 4: top of wall at (4*0.25 + WallHeight)

---

### Step 3.2 — Slope detection between adjacent tiles

**File:** `src/Oravey2.Core/World/HeightHelper.cs` (new)

```
HeightHelper (static)
├── GetHeightDelta(TileMapData, x1, y1, x2, y2) → int
├── GetSlopeType(int delta) → SlopeType { Flat, Gentle, Steep, Cliff }
├── IsPassable(int delta) → bool (Δ < 7)
├── GetSlopeMovementCost(int delta) → float (1.0, 1.5×per step, IMPASSABLE)
```

| Delta | SlopeType | Passable | Cost per step from base |
|-------|-----------|----------|------------------------|
| 0 | Flat | yes | 1.0 |
| 1–2 | Gentle | yes | 1.0 + delta × 0.25 |
| 3–6 | Steep | yes | 1.0 + delta × 0.5 |
| 7+ | Cliff | no | float.PositiveInfinity |

**Tests:** `HeightHelperTests.cs`
- Flat: delta 0 → Flat, cost 1.0
- Gentle: delta 1 → Gentle, cost 1.25
- Gentle: delta 2 → Gentle, cost 1.5
- Steep: delta 3 → Steep, cost 2.5
- Steep: delta 6 → Steep, cost 4.0
- Cliff: delta 7 → Cliff, not passable
- Cliff: delta 20 → Cliff, infinity cost
- Negative deltas (going downhill): same magnitude rules

---

### Step 3.3 — Update pathfinder with height cost

**File:** `src/Oravey2.Core/AI/TileGridPathfinder.cs` (modify)

The pathfinder currently checks `IsWalkable(x, y)`. Add:
1. Check `HeightHelper.IsPassable(delta)` between current and neighbor
2. Add `HeightHelper.GetSlopeMovementCost(delta)` to movement cost

**Tests:** `TileGridPathfinderHeightTests.cs`
- Path avoids cliff edges (delta ≥ 7)
- Path prefers flat ground over steep slope when both lead to goal
- Path goes over gentle slope (delta 1–2) normally
- Completely blocked by ring of cliffs → no path found

---

### Step 3.4 — Height-based line of sight

**File:** `src/Oravey2.Core/World/HeightHelper.cs` (extend)

```
HasLineOfSight(TileMapData, fromX, fromY, toX, toY) → bool
```

A unit at height H1 can see over obstacles at height H2 if H1 > H2. Uses Bresenham line to check intermediate tiles.

**Tests:** `LineOfSightTests.cs`
- Unit on height 10 sees over wall on height 5 → true
- Unit on height 3 cannot see past wall on height 5 → false
- Flat terrain, no obstacles → always visible
- Diagonal line of sight through multiple tiles

---

### Step 3.5 — Test maps with height variation

**File:** `tests/Oravey2.Tests/Fixtures/Maps/test_height/` (new)

Create a small test map fixture with known height layout:
- Flat center (height 1)
- Hill in corner (heights 2–5)
- Cliff on one side (height 1 → 10 in one tile step)
- Ramp (height 1 → 6 over 6 tiles, 1 step per tile)

**Tests:** `HeightMapFixtureTests.cs`
- Load fixture → verify heights at known positions
- Pathfind from flat to hilltop → path goes up ramp, avoids cliff
- Pathfind across cliff → no path

---

## Acceptance Criteria

| # | Criteria | How to Verify |
|---|---------|--------------|
| 1 | Tiles render at correct Y positions based on HeightLevel | Visual + unit test |
| 2 | HeightHelper classifies slopes correctly (Flat/Gentle/Steep/Cliff) | Unit tests |
| 3 | Pathfinder avoids cliffs and applies slope cost | Pathfinder tests |
| 4 | Line of sight accounts for height differences | LOS tests |
| 5 | Existing flat maps (height=1 everywhere) behave identically | Existing tests pass |
| 6 | 20+ new unit tests | Test count |

## Verification

```powershell
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Height" --verbosity normal
dotnet test tests/Oravey2.Tests --verbosity quiet
```
