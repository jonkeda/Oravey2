# Phase 1 — TileData Model

> **Status:** Not Started  
> **Depends on:** Nothing  
> **Unlocks:** Phases 2, 3, 4, 6

---

## Goal

Replace the flat `TileType` enum with a rich `TileData` record as the per-tile data structure. Keep full backward compatibility so all existing tests pass without modification.

---

## Steps

### Step 1.1 — Create `SurfaceType` enum

**File:** `src/Oravey2.Core/World/SurfaceType.cs` (new)

```csharp
public enum SurfaceType : byte
{
    Dirt = 0,
    Asphalt = 1,
    Concrete = 2,
    Grass = 3,
    Sand = 4,
    Mud = 5,
    Rock = 6,
    Metal = 7
}
```

**Test:** `SurfaceTypeTests.cs`
- All 8 values defined
- Byte backing values are sequential 0–7

---

### Step 1.2 — Create `TileFlags` flags enum

**File:** `src/Oravey2.Core/World/TileFlags.cs` (new)

```csharp
[Flags]
public enum TileFlags : byte
{
    None         = 0,
    Walkable     = 1 << 0,
    Irradiated   = 1 << 1,
    Burnable     = 1 << 2,
    Destructible = 1 << 3,
}
```

**Test:** `TileFlagsTests.cs`
- Flags combine correctly (Walkable | Irradiated)
- HasFlag checks work

---

### Step 1.3 — Create `TileData` record

**File:** `src/Oravey2.Core/World/TileData.cs` (new)

```csharp
public readonly record struct TileData(
    SurfaceType Surface,
    byte HeightLevel,
    byte WaterLevel,
    int StructureId,
    TileFlags Flags,
    byte VariantSeed)
{
    public static readonly TileData Empty = default;

    public bool IsWalkable => Flags.HasFlag(TileFlags.Walkable);
    public bool HasWater => WaterLevel > HeightLevel;
    public int WaterDepth => HasWater ? WaterLevel - HeightLevel : 0;

    /// <summary>
    /// Maps this tile to a legacy TileType for backward compatibility.
    /// </summary>
    public TileType LegacyTileType
    {
        get
        {
            if (HasWater) return TileType.Water;
            if (StructureId != 0 && !IsWalkable) return TileType.Wall;
            return Surface switch
            {
                SurfaceType.Asphalt or SurfaceType.Concrete => TileType.Road,
                SurfaceType.Rock => TileType.Rubble,
                SurfaceType.Metal => IsWalkable ? TileType.Road : TileType.Wall,
                _ => IsWalkable ? TileType.Ground : TileType.Empty
            };
        }
    }
}
```

**Tests:** `TileDataTests.cs`
- `Empty` has all zero values, not walkable
- `IsWalkable` returns true when flag set, false when not
- `HasWater` true when WaterLevel > HeightLevel
- `WaterDepth` calculated correctly
- `LegacyTileType` mapping:
  - Dirt+Walkable → Ground
  - Asphalt+Walkable → Road
  - Rock+Walkable → Rubble
  - Water present → Water
  - StructureId+NonWalkable → Wall
  - NonWalkable+NothingElse → Empty

---

### Step 1.4 — Create legacy-compatible factory methods

**File:** `src/Oravey2.Core/World/TileData.cs` (extend)

```csharp
public static class TileDataFactory
{
    public static TileData Ground(byte height = 1, byte variant = 0)
        => new(SurfaceType.Dirt, height, 0, 0, TileFlags.Walkable, variant);

    public static TileData Road(byte height = 1, byte variant = 0)
        => new(SurfaceType.Asphalt, height, 0, 0, TileFlags.Walkable, variant);

    public static TileData Rubble(byte height = 1, byte variant = 0)
        => new(SurfaceType.Rock, height, 0, 0, TileFlags.Walkable, variant);

    public static TileData Water(byte waterLevel = 2, byte terrainHeight = 0, byte variant = 0)
        => new(SurfaceType.Mud, terrainHeight, waterLevel, 0, TileFlags.None, variant);

    public static TileData Wall(byte height = 1, byte variant = 0)
        => new(SurfaceType.Concrete, height, 0, 1, TileFlags.None, variant);

    public static TileData FromLegacy(TileType legacy) => legacy switch
    {
        TileType.Ground => Ground(),
        TileType.Road => Road(),
        TileType.Rubble => Rubble(),
        TileType.Water => Water(),
        TileType.Wall => Wall(),
        _ => TileData.Empty
    };
}
```

**Tests:** `TileDataFactoryTests.cs`
- Each factory method produces correct `LegacyTileType`
- `FromLegacy` round-trips: `FromLegacy(type).LegacyTileType == type` for all 6 TileType values
- Factory methods set correct default heights and flags

---

### Step 1.5 — Add `TileData` storage to `TileMapData`

**File:** `src/Oravey2.Core/World/TileMapData.cs` (modify)

Add a new `TileData[,]` array alongside the existing `TileType[,] Tiles` property. The legacy property keeps working but delegates to the new data.

Changes:
1. Add `TileData[,] TileDataGrid` property (internal storage)
2. Keep `TileType[,] Tiles` as a computed view (reads from `TileDataGrid[x,y].LegacyTileType`)
3. `SetTile(x, y, TileType)` → calls `SetTileData(x, y, TileDataFactory.FromLegacy(type))`
4. Add `SetTileData(x, y, TileData)` and `GetTileData(x, y)`
5. `IsWalkable(x, y)` → reads `TileDataGrid[x,y].IsWalkable`

**Key rule:** `Tiles` property becomes a compatibility shim. All new code uses `GetTileData` / `SetTileData`.

**Tests:** `TileMapDataTests.cs` — **no changes to existing tests**. They use `GetTile`, `SetTile`, `IsWalkable`, which all still work through the shim.

**New tests in `TileMapDataExtendedTests.cs`:**
- `SetTileData` / `GetTileData` round-trip
- `SetTile` with legacy type → `GetTileData` returns matching `TileData`
- `SetTileData` with rich data → `GetTile` returns correct legacy type
- `IsWalkable` uses `TileData.IsWalkable` flag
- `WorldToTile` / `TileToWorld` unchanged (no modification needed)

---

### Step 1.6 — Update `TownMapBuilder` and `WastelandMapBuilder`

**Files:** `TownMapBuilder.cs`, `WastelandMapBuilder.cs` (modify)

Minimal change: replace `map.SetTile(x, y, TileType.Ground)` with `map.SetTileData(x, y, TileDataFactory.Ground())` etc. The layout, coordinates, and tile positions stay identical.

**Tests:** existing `TownMapBuilderTests.cs` and `WastelandMapBuilderTests.cs` — **no changes needed**. They assert on `GetTile` which returns legacy types through the shim.

**New tests (optional):**
- Verify builders set meaningful `VariantSeed` values (deterministic per position)

---

### Step 1.7 — Update `ChunkData`

**File:** `src/Oravey2.Core/World/ChunkData.cs` (modify)

- `GetWorldTile` returns `TileType` (unchanged API)
- Add `GetWorldTileData(int worldX, int worldY) → TileData`
- `CreateDefault` uses `TileDataFactory.Ground()` internally

**Tests:** existing `ChunkDataTests.cs` — **no changes**. Add `ChunkDataExtendedTests.cs`:
- `GetWorldTileData` returns correct data
- Default chunks have `TileDataFactory.Ground()` tiles

---

### Step 1.8 — Update `TileMapRendererScript`

**File:** `src/Oravey2.Core/World/TileMapRendererScript.cs` (modify)

Minimal change: `CreateTileEntity` reads `GetTileData(x, y)` to get `SurfaceType`, still uses `GetTileColor` mapped from legacy type for now. The visual output is identical.

**Tests:** UI tests — **no changes**. Visual output is the same.

---

### Step 1.9 — Keep legacy `TileType` enum

**File:** `src/Oravey2.Core/World/TileType.cs` — **no changes**

The enum stays. It's used by the backward-compat shim and by all existing tests. It will naturally become less referenced over time as new code uses `TileData` directly.

---

## Acceptance Criteria

| # | Criteria | How to Verify |
|---|---------|--------------|
| 1 | `SurfaceType`, `TileFlags`, `TileData`, `TileDataFactory` exist and compile | `dotnet build` |
| 2 | `TileMapData` supports both `GetTile`/`SetTile` (legacy) and `GetTileData`/`SetTileData` (new) | New unit tests |
| 3 | `TileData.LegacyTileType` round-trips for all 6 TileType values | `TileDataFactoryTests` |
| 4 | `TownMapBuilder` and `WastelandMapBuilder` use `TileData` internally | Code review |
| 5 | All existing unit tests pass without modification | `dotnet test tests/Oravey2.Tests` |
| 6 | All existing UI tests pass without modification | `dotnet test tests/Oravey2.UITests` |
| 7 | 15+ new unit tests covering TileData, SurfaceType, TileFlags, factory, extended TileMapData | Test count check |

## Verification

```powershell
dotnet test tests/Oravey2.Tests --verbosity quiet
# Expected: all pass, 0 failures

dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~TileData" --verbosity normal
# Expected: 15+ new tests pass
```
