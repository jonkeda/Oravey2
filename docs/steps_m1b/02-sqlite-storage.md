# Step 02 — SQLite Storage Layer

**Work streams:** WS2 (SQLite Storage Layer)
**Depends on:** Step 01 (expanded TileData)
**User-testable result:** Unit tests prove tiles round-trip through serialise → compress → SQLite → decompress → deserialise. Old Portland map files are gone.

---

## Goals

1. Implement `world.db` and `save_XX.db` schemas.
2. Build the data access layer (`WorldMapStore`, `SaveStateStore`, `MapDataProvider`).
3. Implement Brotli compression and `TileData` binary serialisation.
4. Remove old JSON map infrastructure.

---

## Tasks

### 2.1 — Add SQLite Dependency

- [x] Add `Microsoft.Data.Sqlite` NuGet package to `Oravey2.Core.csproj`
- [x] Verify it resolves and builds

### 2.2 — Database Schemas

- [x] Create `Data/WorldDbSchema.sql` with all world tables: `world_meta`, `continent`, `region`, `chunk`, `chunk_layer`, `entity_spawn`, `linear_feature`, `poi`, `interior`, `terrain_modifier`, `sync_log`, `location_description`
- [x] Create `Data/SaveDbSchema.sql` with all save tables: `save_meta`, `party`, `chunk_state`, `fog_of_war`, `discovered_poi`, `fast_travel_unlock`, `map_marker`, `quest_state`
- [x] Create `DatabaseInitializer` static class — reads SQL files and executes `CREATE TABLE` statements on a fresh connection

### 2.3 — Compression

- [x] Create `MapCompression` static class with `byte[] Compress(ReadOnlySpan<byte>)` and `byte[] Decompress(byte[], int expectedLength)` using Brotli

### 2.4 — TileData Serialisation

- [x] Create `TileDataSerializer` static class
- [x] `SerializeTileGrid(TileData[,])` → `byte[]` (flat via `MemoryMarshal.AsBytes`, then compress)
- [x] `DeserializeTileGrid(byte[], int width, int height)` → `TileData[,]` (decompress, then reinterpret)

### 2.5 — WorldMapStore

- [x] Create `WorldMapStore` class (implements `IDisposable`)
- [x] Constructor opens/creates `world.db`, runs schema if needed, sets WAL mode
- [x] `InsertContinent` / `GetContinent`
- [x] `InsertRegion` / `GetRegion` / `GetRegionByGrid`
- [x] `InsertChunk` / `GetChunk` / `GetChunkByGrid`
- [x] `InsertChunkLayer` / `GetChunkLayers`
- [x] `InsertEntitySpawn` / `GetEntitySpawns`
- [x] `InsertLinearFeature` / `GetLinearFeatures` (by region)
- [x] `InsertPoi` / `GetPois` (by region)
- [x] `InsertTerrainModifier` / `GetTerrainModifiers` (by chunk)
- [x] `GetOrSetMeta(key, value)` / `GetMeta(key)` for `world_meta` table

### 2.6 — SaveStateStore

- [x] Create `SaveStateStore` class (implements `IDisposable`)
- [x] Constructor opens/creates `save_XX.db`, runs schema if needed
- [x] `SaveParty` / `LoadParty`
- [x] `SaveChunkState` / `GetChunkState`
- [x] `SaveFogOfWar` / `GetFogOfWar`
- [x] `DiscoverPoi` / `GetDiscoveredPois`
- [x] `UnlockFastTravel` / `GetFastTravelUnlocks`
- [x] `AddMapMarker` / `RemoveMapMarker` / `GetMapMarkers`

### 2.7 — MapDataProvider

- [x] Create `MapDataProvider` class
- [x] Combines `WorldMapStore` + `SaveStateStore` reads
- [x] `GetChunkData(regionId, gridX, gridY)` → loads world chunk, applies save delta (tile overrides from `chunk_state`)
- [x] Returns merged result

### 2.8 — Remove Old Map Loader

- [x] Delete `Oravey2.Windows/Maps/portland/` directory and all contents
- [x] Delete any JSON-based world/chunk loader code
- [x] Update `ChunkStreamingProcessor` to no longer reference the old loader (it will be fully wired to `MapDataProvider` in Step 10, but remove the dead code path now)

### 2.9 — Unit Tests

File: `tests/Oravey2.Tests/Data/MapCompressionTests.cs`

- [x] `Compress_ThenDecompress_ReturnsOriginalBytes` — random 4 KB payload round-trips
- [x] `Compress_EmptyInput_ReturnsValidOutput` — edge case

File: `tests/Oravey2.Tests/Data/TileDataSerializerTests.cs`

- [x] `SerializeGrid_ThenDeserialize_RoundTrips` — create 16×16 grid with varied data, serialize, deserialize, compare field-by-field
- [x] `SerializeGrid_CompressedSize_SmallerThanRaw` — verify compression actually shrinks data
- [x] `SerializeGrid_SingleTileGrid_Works` — 1×1 edge case

File: `tests/Oravey2.Tests/Data/WorldMapStoreTests.cs`

- [x] `InsertAndGetContinent_RoundTrips` — insert continent, read back, compare
- [x] `InsertAndGetRegion_RoundTrips` — insert region with height/biome data
- [x] `InsertAndGetChunk_RoundTrips` — insert chunk with compressed tile data, read back, decompress, compare tiles
- [x] `GetChunkByGrid_NotFound_ReturnsNull` — missing chunk returns null
- [x] `InsertPoi_GetByRegion_ReturnsAll` — insert 3 POIs, query by region, get 3 back
- [x] `InsertLinearFeature_GetByRegion_ReturnsWithNodes` — feature JSON nodes round-trip

File: `tests/Oravey2.Tests/Data/SaveStateStoreTests.cs`

- [x] `SaveAndLoadParty_RoundTrips` — party JSON round-trips
- [x] `SaveChunkState_ThenGet_ReturnsDelta` — tile overrides persist
- [x] `DiscoverPoi_AppearsInList` — discover POI, then query discovered list

File: `tests/Oravey2.Tests/Data/MapDataProviderTests.cs`

- [x] `GetChunkData_NoSaveDelta_ReturnsWorldData` — clean world chunk
- [x] `GetChunkData_WithSaveDelta_MergesOverrides` — save has tile overrides, merged result reflects them
- [x] `GetChunkData_MissingChunk_ReturnsNull` — chunk not in DB yet

---

## Verify

```bash
dotnet build src/Oravey2.Core
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Data."
```

**User test:** All data layer tests are green. The game still launches (no old map loader crash — it just shows an empty scene since terrain rendering comes in Step 03).
