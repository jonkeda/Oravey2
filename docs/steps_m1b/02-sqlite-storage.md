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

- [ ] Add `Microsoft.Data.Sqlite` NuGet package to `Oravey2.Core.csproj`
- [ ] Verify it resolves and builds

### 2.2 — Database Schemas

- [ ] Create `Data/WorldDbSchema.sql` with all world tables: `world_meta`, `continent`, `region`, `chunk`, `chunk_layer`, `entity_spawn`, `linear_feature`, `poi`, `interior`, `terrain_modifier`, `sync_log`, `location_description`
- [ ] Create `Data/SaveDbSchema.sql` with all save tables: `save_meta`, `party`, `chunk_state`, `fog_of_war`, `discovered_poi`, `fast_travel_unlock`, `map_marker`, `quest_state`
- [ ] Create `DatabaseInitializer` static class — reads SQL files and executes `CREATE TABLE` statements on a fresh connection

### 2.3 — Compression

- [ ] Create `MapCompression` static class with `byte[] Compress(ReadOnlySpan<byte>)` and `byte[] Decompress(byte[], int expectedLength)` using Brotli

### 2.4 — TileData Serialisation

- [ ] Create `TileDataSerializer` static class
- [ ] `SerializeTileGrid(TileData[,])` → `byte[]` (flat via `MemoryMarshal.AsBytes`, then compress)
- [ ] `DeserializeTileGrid(byte[], int width, int height)` → `TileData[,]` (decompress, then reinterpret)

### 2.5 — WorldMapStore

- [ ] Create `WorldMapStore` class (implements `IDisposable`)
- [ ] Constructor opens/creates `world.db`, runs schema if needed, sets WAL mode
- [ ] `InsertContinent` / `GetContinent`
- [ ] `InsertRegion` / `GetRegion` / `GetRegionByGrid`
- [ ] `InsertChunk` / `GetChunk` / `GetChunkByGrid`
- [ ] `InsertChunkLayer` / `GetChunkLayers`
- [ ] `InsertEntitySpawn` / `GetEntitySpawns`
- [ ] `InsertLinearFeature` / `GetLinearFeatures` (by region)
- [ ] `InsertPoi` / `GetPois` (by region)
- [ ] `InsertTerrainModifier` / `GetTerrainModifiers` (by chunk)
- [ ] `GetOrSetMeta(key, value)` / `GetMeta(key)` for `world_meta` table

### 2.6 — SaveStateStore

- [ ] Create `SaveStateStore` class (implements `IDisposable`)
- [ ] Constructor opens/creates `save_XX.db`, runs schema if needed
- [ ] `SaveParty` / `LoadParty`
- [ ] `SaveChunkState` / `GetChunkState`
- [ ] `SaveFogOfWar` / `GetFogOfWar`
- [ ] `DiscoverPoi` / `GetDiscoveredPois`
- [ ] `UnlockFastTravel` / `GetFastTravelUnlocks`
- [ ] `AddMapMarker` / `RemoveMapMarker` / `GetMapMarkers`

### 2.7 — MapDataProvider

- [ ] Create `MapDataProvider` class
- [ ] Combines `WorldMapStore` + `SaveStateStore` reads
- [ ] `GetChunkData(regionId, gridX, gridY)` → loads world chunk, applies save delta (tile overrides from `chunk_state`)
- [ ] Returns merged result

### 2.8 — Remove Old Map Loader

- [ ] Delete `Oravey2.Windows/Maps/portland/` directory and all contents
- [ ] Delete any JSON-based world/chunk loader code
- [ ] Update `ChunkStreamingProcessor` to no longer reference the old loader (it will be fully wired to `MapDataProvider` in Step 10, but remove the dead code path now)

### 2.9 — Unit Tests

File: `tests/Oravey2.Tests/Data/MapCompressionTests.cs`

- [ ] `Compress_ThenDecompress_ReturnsOriginalBytes` — random 4 KB payload round-trips
- [ ] `Compress_EmptyInput_ReturnsValidOutput` — edge case

File: `tests/Oravey2.Tests/Data/TileDataSerializerTests.cs`

- [ ] `SerializeGrid_ThenDeserialize_RoundTrips` — create 16×16 grid with varied data, serialize, deserialize, compare field-by-field
- [ ] `SerializeGrid_CompressedSize_SmallerThanRaw` — verify compression actually shrinks data
- [ ] `SerializeGrid_SingleTileGrid_Works` — 1×1 edge case

File: `tests/Oravey2.Tests/Data/WorldMapStoreTests.cs`

- [ ] `InsertAndGetContinent_RoundTrips` — insert continent, read back, compare
- [ ] `InsertAndGetRegion_RoundTrips` — insert region with height/biome data
- [ ] `InsertAndGetChunk_RoundTrips` — insert chunk with compressed tile data, read back, decompress, compare tiles
- [ ] `GetChunkByGrid_NotFound_ReturnsNull` — missing chunk returns null
- [ ] `InsertPoi_GetByRegion_ReturnsAll` — insert 3 POIs, query by region, get 3 back
- [ ] `InsertLinearFeature_GetByRegion_ReturnsWithNodes` — feature JSON nodes round-trip

File: `tests/Oravey2.Tests/Data/SaveStateStoreTests.cs`

- [ ] `SaveAndLoadParty_RoundTrips` — party JSON round-trips
- [ ] `SaveChunkState_ThenGet_ReturnsDelta` — tile overrides persist
- [ ] `DiscoverPoi_AppearsInList` — discover POI, then query discovered list

File: `tests/Oravey2.Tests/Data/MapDataProviderTests.cs`

- [ ] `GetChunkData_NoSaveDelta_ReturnsWorldData` — clean world chunk
- [ ] `GetChunkData_WithSaveDelta_MergesOverrides` — save has tile overrides, merged result reflects them
- [ ] `GetChunkData_MissingChunk_ReturnsNull` — chunk not in DB yet

---

## Verify

```bash
dotnet build src/Oravey2.Core
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Data."
```

**User test:** All data layer tests are green. The game still launches (no old map loader crash — it just shows an empty scene since terrain rendering comes in Step 03).
