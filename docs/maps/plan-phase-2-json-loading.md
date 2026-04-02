# Phase 2 — JSON Chunk Loading

> **Status:** Not Started  
> **Depends on:** Phase 1 (TileData model)  
> **Unlocks:** Phase 7 (Blueprint compiler)

---

## Goal

Load tile maps from JSON files instead of hardcoded C# builders. This is the foundation for LLM-generated maps.

---

## Steps

### Step 2.1 — Define chunk JSON format

**File:** `src/Oravey2.Core/World/Serialization/ChunkJsonFormat.cs` (new)

Define the JSON-serializable DTOs matching the separate-layer format from doc 03 §9.3:

```csharp
public sealed record ChunkJson(
    int ChunkX,
    int ChunkY,
    byte[][] Surface,    // 16×16 SurfaceType values
    byte[][] Height,     // 16×16 HeightLevel values
    byte[][] Water,      // 16×16 WaterLevel values
    int[][] Structure,   // 16×16 StructureId values
    byte[][] Flags,      // 16×16 TileFlags values
    byte[][] Variant,    // 16×16 VariantSeed values
    EntitySpawnJson[]? Entities
);

public sealed record EntitySpawnJson(
    string PrefabId,
    float LocalX,
    float LocalZ,
    float RotationY,
    string? Faction,
    int? Level,
    string? DialogueId,
    string? LootTable,
    bool Persistent,
    string? ConditionFlag
);
```

**Test:** `ChunkJsonFormatTests.cs`
- All records instantiate with valid data
- Default nullables are null

---

### Step 2.2 — Create `ChunkSerializer`

**File:** `src/Oravey2.Core/World/Serialization/ChunkSerializer.cs` (new)

```
ChunkSerializer
├── SerializeChunk(ChunkData) → string (JSON)
├── DeserializeChunk(string json) → ChunkData
├── SaveChunk(ChunkData, string directory)
├── LoadChunk(string directory, int cx, int cy) → ChunkData
```

Uses `System.Text.Json` with source generators for AOT compatibility.

**Tests:** `ChunkSerializerTests.cs`
- Round-trip: create ChunkData → serialize → deserialize → all tile data matches
- Serialize TownMapBuilder output → deserialize → layout matches original
- Ground tiles have correct SurfaceType/HeightLevel/Flags after round-trip
- Wall tiles have correct StructureId + non-walkable flag
- Water tiles have correct WaterLevel > HeightLevel
- Entity spawn info survives round-trip
- Invalid JSON throws descriptive exception
- Missing file throws FileNotFoundException

---

### Step 2.3 — Define world JSON format

**File:** `src/Oravey2.Core/World/Serialization/WorldJsonFormat.cs` (new)

```csharp
public sealed record WorldJson(
    int ChunksWide,
    int ChunksHigh,
    float TileSize,
    PlayerStartJson PlayerStart,
    string? DefaultWeather
);

public sealed record PlayerStartJson(
    int ChunkX,
    int ChunkY,
    int LocalTileX,
    int LocalTileY
);
```

**Test:** basic instantiation tests

---

### Step 2.4 — Create `MapLoader`

**File:** `src/Oravey2.Core/World/Serialization/MapLoader.cs` (new)

```
MapLoader
├── LoadWorld(string mapDirectory) → WorldMapData
│     reads world.json → dimensions
│     creates WorldMapData(chunksWide, chunksHigh)
│
├── LoadChunk(string mapDirectory, int cx, int cy) → ChunkData?
│     reads chunks/{cx}_{cy}.json → ChunkData
│     returns null if file doesn't exist
│
└── LoadWorldFull(string mapDirectory) → WorldMapData
      loads world.json + all chunk files, sets all chunks
```

**Tests:** `MapLoaderTests.cs`
- Load a test world directory with world.json + 2 chunk files → WorldMapData has correct dimensions
- LoadChunk reads correct tile data from file
- LoadChunk returns null for non-existent chunk file
- LoadWorldFull populates all chunks that have files
- Invalid world.json directory throws

---

### Step 2.5 — Create `MapExporter` (builder → JSON)

**File:** `src/Oravey2.Core/World/Serialization/MapExporter.cs` (new)

Converts existing builder output to JSON files for migration:

```
MapExporter
├── ExportChunk(ChunkData, string directory) → writes {cx}_{cy}.json
├── ExportWorld(WorldMapData, string directory) → writes world.json + all chunks
└── ExportBuilderMap(TileMapData, int cx, int cy, string dir) → single chunk
```

**Tests:** `MapExporterTests.cs`
- Export TownMapBuilder → files exist on disk
- Export → Load round-trip: layout matches original builder output
- Export WastelandMapBuilder → Load → gates at same positions

---

### Step 2.6 — Create test map fixtures

**Directory:** `tests/Oravey2.Tests/Fixtures/Maps/` (new)

Export current TownMapBuilder and WastelandMapBuilder to JSON fixtures:
- `test_town/world.json`
- `test_town/chunks/0_0.json` (32×32 town as a single chunk)
- `test_wasteland/world.json`
- `test_wasteland/chunks/0_0.json`
- `test_minimal/world.json` + `chunks/0_0.json` (4×4 minimal test map)

These become the reference test data for all future loading tests.

**Test:** `MapFixtureTests.cs`
- Load test_town fixture → validate tile at known position matches expected type
- Load test_wasteland fixture → validate west gate is walkable
- Load test_minimal → correct dimensions

---

### Step 2.7 — Update `ChunkStreamingProcessor` (optional)

If the streaming processor currently gets chunks from builders, wire it to use `MapLoader.LoadChunk` instead. If it already uses `WorldMapData.GetChunk`, no change needed.

**Tests:** existing `ChunkStreamingProcessorTests.cs` — should still pass

---

## Acceptance Criteria

| # | Criteria | How to Verify |
|---|---------|--------------|
| 1 | `ChunkSerializer` round-trips all tile data including new TileData fields | Unit tests |
| 2 | `MapLoader` reads world.json + chunk files into `WorldMapData` | Unit tests |
| 3 | `MapExporter` writes valid JSON that `MapLoader` can read | Round-trip tests |
| 4 | Town and wasteland maps exported as JSON fixtures | Fixture files exist |
| 5 | Loading fixtures produces maps identical to builder output | Comparison tests |
| 6 | All existing tests pass | `dotnet test` |
| 7 | 20+ new unit tests covering serialization pipeline | Test count |

## Verification

```powershell
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Serialization" --verbosity normal
dotnet test tests/Oravey2.Tests --verbosity quiet
```
