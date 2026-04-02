# Phase 6 — Buildings & Props

> **Status:** Not Started  
> **Depends on:** Phase 1 (TileData with StructureId)  
> **Can run in parallel with:** Phases 3, 4, 5

---

## Goal

Place 3D building meshes (from meshy.ai) and static props on the tile map. Buildings block walkability via footprint tiles. Props are decorative.

---

## Steps

### Step 6.1 — BuildingDefinition data model

**File:** `src/Oravey2.Core/World/BuildingDefinition.cs` (new)

```csharp
public sealed record BuildingDefinition(
    string Id,
    string Name,
    string MeshAssetPath,
    BuildingSize Size,
    (int X, int Y)[] Footprint,
    int Floors,
    float Condition,
    string? InteriorChunkId   // Large buildings only, null = not enterable
);

public enum BuildingSize { Small, Large }
```

Per doc 04: Small (1–4 tiles) = not enterable. Large (5+ tiles) = loads separate interior chunk (deferred — the field exists but is unused for now).

**Tests:** `BuildingDefinitionTests.cs`
- Small building: footprint ≤ 4 tiles, InteriorChunkId null
- Large building: footprint ≥ 5 tiles
- MeshAssetPath not null or empty
- Condition clamped 0.0–1.0

---

### Step 6.2 — PropDefinition data model

**File:** `src/Oravey2.Core/World/PropDefinition.cs` (new)

```csharp
public sealed record PropDefinition(
    string Id,
    string MeshAssetPath,
    int ChunkX,
    int ChunkY,
    int LocalTileX,
    int LocalTileY,
    float RotationDegrees,
    float Scale,
    bool BlocksWalkability,
    (int X, int Y)[]? Footprint   // only if BlocksWalkability = true
);
```

**Tests:** `PropDefinitionTests.cs`
- Non-blocking prop: Footprint null, BlocksWalkability false
- Blocking prop (car wreck): Footprint has tiles, BlocksWalkability true
- Scale defaults to 1.0

---

### Step 6.3 — BuildingRegistry

**File:** `src/Oravey2.Core/World/BuildingRegistry.cs` (new)

```
BuildingRegistry
├── Register(BuildingDefinition)
├── GetById(string id) → BuildingDefinition?
├── GetByChunk(int cx, int cy) → IReadOnlyList<BuildingDefinition>
├── GetAll() → IReadOnlyList<BuildingDefinition>
```

Stores all building definitions for the current map. Populated during map load.

**Tests:** `BuildingRegistryTests.cs`
- Register + GetById round-trip
- GetByChunk returns only buildings in that chunk
- Duplicate ID throws
- GetById for non-existent returns null

---

### Step 6.4 — Footprint walkability application

**File:** `src/Oravey2.Core/World/BuildingPlacer.cs` (new)

```
BuildingPlacer (static)
├── ApplyFootprint(TileMapData, BuildingDefinition) → void
│     sets StructureId on footprint tiles, clears Walkable flag
├── ApplyPropFootprint(TileMapData, PropDefinition) → void
│     same for blocking props
├── ValidatePlacement(TileMapData, footprint) → bool
│     checks footprint tiles are in bounds and not already occupied
```

**Tests:** `BuildingPlacerTests.cs`
- Apply building footprint → footprint tiles have StructureId != 0
- Apply building footprint → footprint tiles are not walkable
- Apply building footprint → non-footprint tiles unchanged
- Validate: footprint in bounds → true
- Validate: footprint out of bounds → false
- Validate: footprint overlaps existing building → false
- Apply prop footprint (blocking) → tiles not walkable
- Apply prop footprint (non-blocking) → tiles unchanged

---

### Step 6.5 — JSON serialization for buildings and props

**File:** `src/Oravey2.Core/World/Serialization/BuildingJsonFormat.cs` (new)

```csharp
public sealed record BuildingJson(
    string Id,
    string Name,
    string MeshAsset,
    string Size,
    int[][] Footprint,
    int Floors,
    float Condition,
    string? InteriorChunkId
);

public sealed record PropJson(
    string Id,
    string MeshAsset,
    PlacementJson Placement,
    float Rotation,
    float Scale,
    bool BlocksWalkability,
    int[][]? Footprint
);
```

Integrate with `MapLoader` — buildings and props loaded from `buildings.json` and `props.json` in the map directory.

**Tests:** `BuildingSerializationTests.cs`
- Round-trip building definition through JSON
- Round-trip prop definition through JSON
- Load buildings.json from fixture → correct building count and IDs

---

### Step 6.6 — Building mesh placement in renderer

**File:** `src/Oravey2.Core/World/TileMapRendererScript.cs` (modify)

After terrain tiles are rendered, place building entities:
1. For each building in `BuildingRegistry.GetByChunk(current chunk)`
2. Calculate world position from footprint center
3. Create entity with `ModelComponent` referencing `MeshAssetPath`
4. Position at terrain height of footprint center tile

For props: same approach, simpler (single tile position, rotation, scale).

**Note:** Actual mesh loading from glTF/FBX is Stride-specific and depends on the asset pipeline. For testing, use placeholder cube meshes. Real meshy.ai assets come later.

**Tests:** UI tests — manual visual verification. Unit tests verify that the correct position and mesh path are computed.

---

### Step 6.7 — Test fixtures with buildings

**Directory:** `tests/Oravey2.Tests/Fixtures/Maps/test_buildings/` (new)

Fixture with:
- 1 Small building (2×2 footprint)
- 1 Large building (3×4 footprint)
- 2 props (1 blocking, 1 non-blocking)
- buildings.json + props.json + chunk file

**Tests:** `BuildingFixtureTests.cs`
- Load fixture → BuildingRegistry has 2 buildings
- Small building footprint tiles are non-walkable
- Large building has InteriorChunkId field (even if unused)
- Blocking prop's footprint tiles are non-walkable
- Non-blocking prop's tile is still walkable
- Pathfinder routes around building footprints

---

## Acceptance Criteria

| # | Criteria | How to Verify |
|---|---------|--------------|
| 1 | BuildingDefinition and PropDefinition data models exist | Compile |
| 2 | BuildingPlacer correctly marks footprint tiles non-walkable | Unit tests |
| 3 | BuildingRegistry stores and retrieves by ID/chunk | Unit tests |
| 4 | Placement validation rejects overlapping/out-of-bounds buildings | Unit tests |
| 5 | JSON round-trip for buildings and props | Serialization tests |
| 6 | Pathfinder routes around building footprints | Pathfinder tests |
| 7 | All existing tests pass | `dotnet test` |
| 8 | 20+ new unit tests | Test count |

## Verification

```powershell
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Building" --verbosity normal
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Prop" --verbosity normal
dotnet test tests/Oravey2.Tests --verbosity quiet
```
