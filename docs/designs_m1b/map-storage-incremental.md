# Map Storage & Incremental World Building

**Status:** Draft  
**Milestone:** M1b  
**Depends on:** All previous map designs, MapGeneratorService

---

## Summary

The game world is **fully procedurally generated** and built **incrementally**. Initial world creation produces a coarse Level 2/3 skeleton. Level 1 detail is generated on demand when the player approaches an area. New content can be pushed to players via **server download** while the game is running.

All map data lives in an **embedded SQLite database**. World data is **shared across save slots** — only player progress differs per save.

```
┌─────────────────────────────────────────────────────────┐
│                    On Disk                                │
│                                                           │
│  world.db          ← SQLite: all map data (shared)       │
│  saves/                                                   │
│    save_01.db      ← SQLite: player progress, state      │
│    save_02.db      ← SQLite: player progress, state      │
│                                                           │
│  content-server    ← Remote: pushes new regions/updates  │
└─────────────────────────────────────────────────────────┘
```

---

## Core Principles

1. **Generate coarse first, refine later.** The world starts as a Level 3 continent skeleton + Level 2 region outlines. Level 1 chunks are generated when needed.
2. **Never block the player.** If the player enters an area without Level 1 data, generate it immediately (async, behind a brief "entering area" transition).
3. **Server can push content.** New regions, updated generation parameters, or pre-generated detail can arrive over the network and merge into the local database.
4. **World data is immutable per version.** Saves reference a world version. Player actions are stored as deltas in the save database, never mutating world data.
5. **Partial loading.** Only load chunks near the camera. SQLite spatial queries fetch what's needed.

---

## Database Schema

### World Database (`world.db`)

```sql
-- ─── World Metadata ───

CREATE TABLE world_meta (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
-- Keys: 'world_id', 'seed', 'version', 'created_utc', 'generator_version'

-- ─── Level 3: Continent ───

CREATE TABLE continent (
    continent_id  INTEGER PRIMARY KEY,
    name          TEXT NOT NULL,
    bounds_min_x  REAL NOT NULL,
    bounds_min_y  REAL NOT NULL,
    bounds_max_x  REAL NOT NULL,
    bounds_max_y  REAL NOT NULL,
    height_data   BLOB NOT NULL,    -- Compressed float[] heightmap
    biome_data    BLOB NOT NULL,    -- Compressed BiomeType[] grid
    version       INTEGER NOT NULL DEFAULT 1
);

-- ─── Level 2: Regions ───

CREATE TABLE region (
    region_id      INTEGER PRIMARY KEY,
    continent_id   INTEGER NOT NULL REFERENCES continent(continent_id),
    grid_x         INTEGER NOT NULL,
    grid_y         INTEGER NOT NULL,
    height_data    BLOB NOT NULL,   -- Compressed float[] regional heightmap
    biome_data     BLOB NOT NULL,   -- Compressed BiomeType[] grid
    detail_level   INTEGER NOT NULL DEFAULT 2,  -- 2 = L2 coarse, 1 = L1 ready
    generation_seed INTEGER NOT NULL,
    version        INTEGER NOT NULL DEFAULT 1,
    UNIQUE(continent_id, grid_x, grid_y)
);

CREATE INDEX idx_region_grid ON region(continent_id, grid_x, grid_y);

-- ─── Level 1: Chunks ───

CREATE TABLE chunk (
    chunk_id       INTEGER PRIMARY KEY,
    region_id      INTEGER NOT NULL REFERENCES region(region_id),
    grid_x         INTEGER NOT NULL,
    grid_y         INTEGER NOT NULL,
    chunk_mode     INTEGER NOT NULL,    -- 0 = Heightmap, 1 = Hybrid
    tile_data      BLOB NOT NULL,       -- Compressed TileData[16,16]
    version        INTEGER NOT NULL DEFAULT 1,
    generated_utc  TEXT NOT NULL,
    UNIQUE(region_id, grid_x, grid_y)
);

CREATE INDEX idx_chunk_grid ON chunk(region_id, grid_x, grid_y);

-- ─── Map Layers (Underground, Elevated) ───

CREATE TABLE chunk_layer (
    chunk_layer_id INTEGER PRIMARY KEY,
    chunk_id       INTEGER NOT NULL REFERENCES chunk(chunk_id),
    map_layer      INTEGER NOT NULL,    -- MapLayer enum
    tile_data      BLOB NOT NULL,
    version        INTEGER NOT NULL DEFAULT 1,
    UNIQUE(chunk_id, map_layer)
);

-- ─── Entities & Spawns ───

CREATE TABLE entity_spawn (
    spawn_id       INTEGER PRIMARY KEY,
    chunk_id       INTEGER NOT NULL REFERENCES chunk(chunk_id),
    map_layer      INTEGER NOT NULL DEFAULT 0,  -- Surface by default
    entity_type    TEXT NOT NULL,       -- 'npc', 'tree', 'scavenge', 'prop', 'vehicle'
    tile_x         INTEGER NOT NULL,
    tile_y         INTEGER NOT NULL,
    data_json      TEXT NOT NULL,       -- Type-specific spawn data (JSON)
    version        INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX idx_spawn_chunk ON entity_spawn(chunk_id);

-- ─── Linear Features ───

CREATE TABLE linear_feature (
    feature_id     INTEGER PRIMARY KEY,
    region_id      INTEGER NOT NULL REFERENCES region(region_id),
    feature_type   INTEGER NOT NULL,    -- LinearFeatureType enum
    style          TEXT NOT NULL,
    width          REAL NOT NULL,
    nodes_json     TEXT NOT NULL,        -- JSON array of {x, y, override_height?}
    version        INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX idx_feature_region ON linear_feature(region_id);

-- ─── Points of Interest ───

CREATE TABLE poi (
    poi_id         INTEGER PRIMARY KEY,
    region_id      INTEGER NOT NULL REFERENCES region(region_id),
    name           TEXT NOT NULL,
    poi_type       INTEGER NOT NULL,    -- PoiType enum
    world_x        REAL NOT NULL,
    world_y        REAL NOT NULL,
    size           INTEGER NOT NULL,    -- PoiSize enum
    faction_id     INTEGER,
    has_fast_travel INTEGER NOT NULL DEFAULT 0,
    data_json      TEXT,                -- Extra config (interior links, etc.)
    version        INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX idx_poi_region ON poi(region_id);

-- ─── Building Interiors ───

CREATE TABLE interior (
    interior_id    INTEGER PRIMARY KEY,
    poi_id         INTEGER REFERENCES poi(poi_id),
    chunk_id       INTEGER REFERENCES chunk(chunk_id),
    floor_count    INTEGER NOT NULL,
    width          INTEGER NOT NULL,
    height         INTEGER NOT NULL,
    tile_data      BLOB NOT NULL,       -- Compressed TileData per floor
    entity_json    TEXT,                -- Interior entity spawns
    version        INTEGER NOT NULL DEFAULT 1
);

-- ─── Terrain Modifiers ───

CREATE TABLE terrain_modifier (
    modifier_id    INTEGER PRIMARY KEY,
    chunk_id       INTEGER NOT NULL REFERENCES chunk(chunk_id),
    modifier_type  TEXT NOT NULL,       -- 'flatten_strip', 'channel_cut', 'level_rect', 'crater'
    data_json      TEXT NOT NULL,       -- Type-specific parameters
    version        INTEGER NOT NULL DEFAULT 1
);

-- ─── Content Sync ───

CREATE TABLE sync_log (
    sync_id        INTEGER PRIMARY KEY,
    received_utc   TEXT NOT NULL,
    server_version INTEGER NOT NULL,
    regions_added  INTEGER NOT NULL DEFAULT 0,
    chunks_added   INTEGER NOT NULL DEFAULT 0,
    chunks_updated INTEGER NOT NULL DEFAULT 0
);
```

### Save Database (`save_XX.db`)

```sql
-- ─── Save Metadata ───

CREATE TABLE save_meta (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
-- Keys: 'save_id', 'world_version', 'play_time', 'last_saved_utc', 'party_location'

-- ─── Player State ───

CREATE TABLE party (
    party_id       INTEGER PRIMARY KEY,
    data_json      TEXT NOT NULL         -- Party members, inventory, skills, vehicle
);

-- ─── Chunk Persistence (per-save deltas on world data) ───

CREATE TABLE chunk_state (
    chunk_id       INTEGER NOT NULL,     -- References world.db chunk
    permanent_flags TEXT,                -- JSON set: quest flags that persist forever
    tile_overrides  BLOB,               -- Sparse delta: only changed tiles
    depleted_nodes  TEXT,               -- JSON int[]: depleted scavenge node IDs
    destroyed_entities TEXT,            -- JSON int[]: destroyed entity spawn IDs
    PRIMARY KEY(chunk_id)
);

-- ─── Fog of War ───

CREATE TABLE fog_of_war (
    region_id      INTEGER NOT NULL,
    discovered_mask BLOB NOT NULL,      -- Bitmask: 1 = discovered
    PRIMARY KEY(region_id)
);

-- ─── Discovered POIs ───

CREATE TABLE discovered_poi (
    poi_id         INTEGER NOT NULL,
    discovered_utc TEXT NOT NULL,
    PRIMARY KEY(poi_id)
);

-- ─── Fast Travel Unlocks ───

CREATE TABLE fast_travel_unlock (
    poi_id         INTEGER NOT NULL,
    unlocked_utc   TEXT NOT NULL,
    PRIMARY KEY(poi_id)
);

-- ─── Player Map Markers ───

CREATE TABLE map_marker (
    marker_id      INTEGER PRIMARY KEY,
    world_x        REAL NOT NULL,
    world_y        REAL NOT NULL,
    marker_type    INTEGER NOT NULL,
    label          TEXT,
    color_rgba     INTEGER
);

-- ─── Quest State ───

CREATE TABLE quest_state (
    quest_id       TEXT PRIMARY KEY,
    state_json     TEXT NOT NULL
);
```

---

## Data Compression

All `BLOB` columns store compressed data to keep the database compact.

| Data | Raw Size (per chunk) | Format | Compressed |
|------|---------------------|--------|-----------|
| `TileData[16,16]` | ~3 KB (256 tiles × ~12 bytes) | Brotli | ~0.5–1.5 KB |
| Region heightmap | ~40 KB | Brotli | ~5–15 KB |
| Continent heightmap | ~10 KB | Brotli | ~2–5 KB |
| Biome grid | ~1 KB | Run-length + Brotli | ~0.1–0.3 KB |

```csharp
public static class MapCompression
{
    public static byte[] Compress(ReadOnlySpan<byte> raw)
    {
        using var ms = new MemoryStream();
        using (var brotli = new BrotliStream(ms, CompressionLevel.Optimal))
            brotli.Write(raw);
        return ms.ToArray();
    }

    public static byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }
}
```

### TileData Serialisation

`TileData` is a fixed-size record struct. Serialise to a flat byte array via `MemoryMarshal`:

```csharp
public static byte[] SerializeTileGrid(TileData[,] grid)
{
    int w = grid.GetLength(0), h = grid.GetLength(1);
    var flat = new TileData[w * h];
    Buffer.BlockCopy(grid, 0, flat, 0, w * h * Unsafe.SizeOf<TileData>());
    var bytes = MemoryMarshal.AsBytes(flat.AsSpan());
    return MapCompression.Compress(bytes);
}
```

---

## Incremental World Generation

### Phase 1: World Creation (New Game)

When the player starts a new game:

```
1. Generate world seed
2. Generate continent(s):
   - Level 3 heightmap (coarse grid ~500m cells)
   - Place biome regions (Perlin noise + rules)
   - Place major POIs: cities, mountain ranges, coastlines
   → INSERT into continent, poi tables
3. Generate Level 2 regions within starting continent:
   - Regional heightmap (sample from L3 + detail noise)
   - Place roads connecting cities, rivers from mountains
   - Mark regions as detail_level = 2
   → INSERT into region, linear_feature tables
4. Generate Level 1 for starting area ONLY:
   - Full TileData grid for ~3×3 chunks around player start
   - Entity spawns, scavenge nodes, trees, structures
   - Mark detail_level = 1 on the region
   → INSERT into chunk, entity_spawn tables
```

**Result:** Player can immediately explore the starting town at Level 1. The rest of the world exists as a Level 2/3 skeleton — visible when zooming out but not yet detailed.

### Phase 2: On-Demand Generation (Gameplay)

When the player approaches an area that has Level 2 data but no Level 1 chunks:

```
  Camera approaching undetailed region
           │
           ▼
  ┌─────────────────────────────┐
  │  ChunkStreamingProcessor    │
  │  requests chunk(grid_x,y)   │
  │  from WorldMapStore         │
  └──────────┬──────────────────┘
             │ Cache miss — no chunk row in DB
             ▼
  ┌─────────────────────────────┐
  │  MapGeneratorService        │
  │  .GenerateChunk(region,     │
  │     grid_x, grid_y, seed)  │
  │                             │
  │  Reads region heightmap,    │
  │  biome, nearby linear       │
  │  features, POIs             │
  │                             │
  │  Produces:                  │
  │   - TileData[16,16]        │
  │   - EntitySpawns[]          │
  │   - TerrainModifiers[]      │
  │   - ChunkMode               │
  └──────────┬──────────────────┘
             │
             ▼
  ┌─────────────────────────────┐
  │  INSERT into chunk,         │
  │  entity_spawn,              │
  │  terrain_modifier tables    │
  │                             │
  │  Chunk now cached on disk   │
  └──────────┬──────────────────┘
             │
             ▼
  ┌─────────────────────────────┐
  │  ChunkTerrainBuilder.Build()│
  │  renders the new chunk      │
  └─────────────────────────────┘
```

### Generation Budget

Generation must be fast enough to stay ahead of the player:

| What | Target Time | Strategy |
|------|-------------|----------|
| Single chunk (16×16 tiles) | < 50 ms | Deterministic from seed — no LLM call |
| Region skeleton (Level 2) | < 200 ms | Height + biome + feature sampling |
| Town layout (POI with buildings) | < 500 ms | Procedural building placement + LLM for names/flavour |
| Full 3×3 chunk neighbourhood | < 150 ms | 9 × single chunk, parallelisable |

**Key rule:** Chunk generation uses **deterministic procedural algorithms** seeded from the region seed + grid coordinates. The LLM is used only for higher-level content (quest text, NPC dialogue, town flavour) which can be fetched asynchronously and patched in after the terrain is already playable.

### Generation Consistency

Because generation is seed-based, the same chunk at the same coordinates always produces the same terrain. This means:
- No need to pre-generate the entire world
- Chunks can be regenerated if the database is corrupted
- Two players with the same world seed see identical terrain

---

## Server Content Delivery

New content arrives from the server as **region packs** — bundles of pre-generated or curated data that merge into the local `world.db`.

### Content Pack Format

```csharp
public record ContentPack(
    int PackId,
    int WorldVersion,          // Must match local world version
    ContentPackType Type,
    byte[] Payload);           // Brotli-compressed SQL statements or binary data

public enum ContentPackType : byte
{
    NewRegion,            // Adds a new region with L2 data
    RegionDetail,         // Upgrades a region from L2 to L1 (adds chunks)
    PoiUpdate,            // Adds/updates POIs in existing regions
    LinearFeatureUpdate,  // Adds/modifies roads, rivers
    EntityUpdate,         // Adds NPCs, quests, scavenge nodes to existing chunks
    WorldEvent            // Temporary world changes (faction war, disaster)
}
```

### Sync Flow

```
┌───────────────┐          ┌──────────────────┐
│ Content Server │ ──push──▶│ Game Client       │
│                │          │                    │
│ Checks:        │          │ 1. Validate pack   │
│ - world_id     │          │    version match   │
│ - client ver   │          │ 2. Apply within    │
│ - last_sync    │          │    transaction     │
│                │          │ 3. Log to sync_log │
│ Sends:         │          │ 4. Invalidate      │
│ - delta packs  │          │    WorldLodCache   │
│   since last   │          │ 5. Chunk streaming │
│   sync         │          │    picks up new    │
└───────────────┘          │    data next frame │
                            └──────────────────┘
```

### Conflict Resolution

World data is **append/replace by version**. The server assigns monotonically increasing version numbers. When a content pack updates an existing row, it only applies if `pack.version > local.version`:

```csharp
public void ApplyContentPack(ContentPack pack, SQLiteConnection db)
{
    using var tx = db.BeginTransaction();

    foreach (var regionUpdate in pack.Regions)
    {
        db.Execute(@"
            INSERT INTO region (region_id, continent_id, grid_x, grid_y, 
                                height_data, biome_data, detail_level, 
                                generation_seed, version)
            VALUES (@id, @cid, @gx, @gy, @hd, @bd, @dl, @seed, @ver)
            ON CONFLICT(continent_id, grid_x, grid_y) DO UPDATE
            SET height_data = @hd, biome_data = @bd, detail_level = @dl,
                version = @ver
            WHERE version < @ver",
            regionUpdate);
    }

    // Same pattern for chunks, POIs, features, entities...
    
    db.Execute("INSERT INTO sync_log (...) VALUES (...)", pack);
    tx.Commit();
}
```

### Player State Safety

Content packs **never touch save databases**. They only modify `world.db`. Player progress (chunk_state, discovered POIs, quest state) is safe. If a content pack replaces chunk data:
- The save's `chunk_state` tile overrides still apply on top.
- Destroyed entities in the save are reconciled: if the entity spawn ID no longer exists in the new world data, the override is silently dropped.

---

## Data Access Layer

### WorldMapStore

```csharp
public class WorldMapStore : IDisposable
{
    private readonly SQLiteConnection _worldDb;
    private readonly LruCache<(int regionId, int x, int y), ChunkData> _chunkCache;

    public WorldMapStore(string worldDbPath, int cacheSize = 64)
    {
        _worldDb = new SQLiteConnection(worldDbPath, SQLiteOpenFlags.ReadOnly);
        _chunkCache = new LruCache<(int, int, int), ChunkData>(cacheSize);
    }

    // ─── Level 3 ───
    public ContinentMapData GetContinent(int continentId) { ... }

    // ─── Level 2 ───
    public RegionMapData GetRegion(int continentId, int gridX, int gridY) { ... }
    public IReadOnlyList<LinearFeature> GetLinearFeatures(int regionId) { ... }
    public IReadOnlyList<PointOfInterest> GetPois(int regionId) { ... }

    // ─── Level 1 ───
    public ChunkData? GetChunk(int regionId, int gridX, int gridY)
    {
        var key = (regionId, gridX, gridY);
        if (_chunkCache.TryGet(key, out var cached))
            return cached;

        var row = _worldDb.FindWithQuery<ChunkRow>(
            "SELECT * FROM chunk WHERE region_id = ? AND grid_x = ? AND grid_y = ?",
            regionId, gridX, gridY);

        if (row is null) return null;  // Not yet generated

        var chunk = DeserializeChunk(row);
        _chunkCache.Put(key, chunk);
        return chunk;
    }

    public bool HasChunk(int regionId, int gridX, int gridY)
    {
        return _worldDb.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM chunk WHERE region_id = ? AND grid_x = ? AND grid_y = ?",
            regionId, gridX, gridY) > 0;
    }
}
```

### SaveStateStore

```csharp
public class SaveStateStore : IDisposable
{
    private readonly SQLiteConnection _saveDb;

    // ─── Read with world + save overlay ───
    public ChunkData GetEffectiveChunk(ChunkData worldChunk)
    {
        var state = _saveDb.Find<ChunkStateRow>(worldChunk.ChunkId);
        if (state is null) return worldChunk;  // No player modifications

        return ApplyOverrides(worldChunk, state);
    }

    // ─── Write player changes ───
    public void SaveTileOverride(int chunkId, int tileX, int tileY, TileData newTile) { ... }
    public void MarkScavengeNodeDepleted(int chunkId, int spawnId) { ... }
    public void MarkEntityDestroyed(int chunkId, int spawnId) { ... }
    public void SetPermanentFlag(int chunkId, string flag) { ... }
    public void DiscoverPoi(int poiId) { ... }
    public void UnlockFastTravel(int poiId) { ... }
}
```

### Combined Access

```csharp
public class MapDataProvider
{
    private readonly WorldMapStore _world;
    private readonly SaveStateStore _save;
    private readonly MapGeneratorService _generator;

    public async Task<ChunkData> GetOrGenerateChunkAsync(
        int regionId, int gridX, int gridY)
    {
        // 1. Try world DB
        var chunk = _world.GetChunk(regionId, gridX, gridY);

        // 2. Generate if missing
        if (chunk is null)
        {
            var region = _world.GetRegion(regionId);
            chunk = await _generator.GenerateChunkAsync(region, gridX, gridY);
            _world.InsertChunk(chunk);  // Persist for next time
        }

        // 3. Apply save-state overrides
        return _save.GetEffectiveChunk(chunk);
    }
}
```

---

## Incremental Detail Levels

A region progresses through detail levels as the player explores:

```
State 0: Region exists in L3 continent data only
         → Just a coloured area on the continental map
         → No region row in DB yet

State 1: Region row created with L2 data (detail_level = 2)
         → Heightmap, biome, roads, POI markers visible at Level 2
         → Created when player zooms into L2 near this region, or via content pack

State 2: First L1 chunk generated (detail_level still 2)
         → Player entered the region; chunks generated on demand
         → Each chunk INSERTed as player approaches

State 3: Most chunks generated (detail_level → 1)
         → Player has explored significantly
         → Region marked as fully detailed
```

### Lazy Region Creation

At game start, only the starting continent and its immediate regions exist. Other regions are created when first needed:

```csharp
public RegionMapData GetOrCreateRegion(int continentId, int gridX, int gridY)
{
    var existing = _world.GetRegion(continentId, gridX, gridY);
    if (existing is not null) return existing;

    // Generate from continent data + seed
    var continent = _world.GetContinent(continentId);
    var region = _generator.GenerateRegion(continent, gridX, gridY);
    _world.InsertRegion(region);
    return region;
}
```

---

## World Size Estimates

| Scale | Grid | Cell Size | Total Coverage |
|-------|------|-----------|---------------|
| Continent | 200 × 200 | ~1 km | 200 × 200 km |
| Region | 32 × 32 per continent cell | ~30 m | ~1 km per region |
| Chunk | 16 × 16 per region | ~1 m tiles | ~30 m per chunk |

### Database Size Estimates

| Content | Row Count | Avg Row Size | Total |
|---------|-----------|-------------|-------|
| Continent (1) | 1 | ~15 KB | 15 KB |
| All L2 regions (explored) | ~500 | ~10 KB | 5 MB |
| L1 chunks (visited areas) | ~5 000 | ~2 KB | 10 MB |
| Entity spawns | ~50 000 | ~200 B | 10 MB |
| Linear features | ~2 000 | ~500 B | 1 MB |
| POIs | ~1 000 | ~300 B | 300 KB |
| **Total world.db** | | | **~25–50 MB** |
| **Save database** | | | **~1–5 MB** |

The world grows as the player explores. A complete playthrough exploring ~20% of the continent might produce a 50 MB world database. SQLite handles this comfortably.

---

## Caching Strategy

### In-Memory Cache

```
┌──────────────────────────────────────────────┐
│                 LruCache                      │
│                                                │
│  Layer 1: Active chunks (9)                   │
│           Currently rendered, always in RAM    │
│                                                │
│  Layer 2: Recent chunks (55)                  │
│           Recently visited, fast re-access     │
│                                                │
│  Layer 3: SQLite disk                         │
│           Everything else                      │
│                                                │
│  Total in-memory: 64 chunks × ~3 KB = ~200 KB│
└──────────────────────────────────────────────┘
```

### Cache Invalidation

- **Chunk streamed out:** Stays in LRU (layer 2) until evicted.
- **Content pack applied:** Invalidate all cached chunks for affected regions.
- **Save state written:** Invalidate the specific chunk (re-merge world + save on next access).
- **New chunk generated:** Insert into cache directly (skip DB read-back).

---

## Threading & Async

| Operation | Thread | Blocking? |
|-----------|--------|-----------|
| Chunk read from cache | Game thread | No |
| Chunk read from SQLite | Background task | No (async) |
| Chunk generation | Background task(s) | No (async) |
| Chunk insert to SQLite | Background task | No (async, batched) |
| Content pack apply | Background task | Brief lock on write |
| Save state write | Background task | Brief lock on write |

SQLite is opened in **WAL mode** (Write-Ahead Logging) for concurrent reads during writes:

```csharp
db.Execute("PRAGMA journal_mode = WAL");
db.Execute("PRAGMA synchronous = NORMAL");
```

### Write Batching

Newly generated chunks are batched and written in a single transaction every ~1 second, not one transaction per chunk. This avoids SQLite overhead from frequent small transactions.

---

## Backup & Integrity

| Concern | Mitigation |
|---------|-----------|
| Database corruption | SQLite `PRAGMA integrity_check` on load; regenerate from seed if corrupt |
| Power loss during write | WAL mode ensures atomic transactions |
| Save corruption | Auto-backup save DB every 5 minutes to `save_XX.backup.db` |
| Version mismatch | `world_meta.generator_version` checked on load; migration path if schema changes |
| World regeneration | Because generation is deterministic from seed, any chunk can be regenerated. DB is a cache, not source of truth — the seed is. |

### Recovery

If `world.db` is lost or corrupt:
1. Create empty `world.db` with fresh schema.
2. Re-generate continent from seed (stored in `save_meta`).
3. Chunks regenerate on demand as the player explores.
4. Save state (player progress) is intact in `save_XX.db`.
5. Only loss: pre-fetched content packs must be re-downloaded.

---

## Content Server Protocol

### Endpoints

```
GET  /api/world/{worldId}/sync?since={lastSyncVersion}
     → Returns ContentPack[] with all updates since that version

GET  /api/world/{worldId}/region/{regionId}
     → Returns full region data (for targeted download)

POST /api/world/{worldId}/report
     → Client sends anonymous telemetry: which regions visited, for prioritisation
```

### Push Strategy

The server may proactively push content when:
- Player is near a region boundary → pre-fetch adjacent region L2 data.
- A new content drop is published → notify client to sync.
- Player is idle (in menu, paused) → background sync of nearby regions.

### Offline Play

The game is fully playable offline. Procedural generation fills any gaps. Content packs are a bonus — curated POIs, named NPCs, quest chains, richer building layouts — but never required.

---

## Files to Create / Modify

| Action | File | Notes |
|--------|------|-------|
| Create | `Data/WorldMapStore.cs` | SQLite read access to world.db |
| Create | `Data/SaveStateStore.cs` | SQLite read/write for save databases |
| Create | `Data/MapDataProvider.cs` | Combined world + save + generation access |
| Create | `Data/MapCompression.cs` | Brotli compress/decompress for BLOBs |
| Create | `Data/TileDataSerializer.cs` | TileData[,] ↔ byte[] conversion |
| Create | `Data/ContentPack.cs` | Content pack records and application logic |
| Create | `Data/ContentSyncService.cs` | Server communication, pack download/apply |
| Create | `Data/WorldDbSchema.sql` | Schema creation script |
| Create | `Data/SaveDbSchema.sql` | Schema creation script |
| Create | `Data/LruCache.cs` | Generic LRU cache (or use existing library) |
| Modify | `World/ChunkStreamingProcessor.cs` | Use `MapDataProvider` instead of in-memory data |
| Modify | `Services/MapGeneratorService.cs` | Add `GenerateChunkAsync`, `GenerateRegion` |
| Modify | `World/WorldMapData.cs` | Backed by `WorldMapStore` instead of in-memory dict |

---

## Acceptance Criteria

1. New game creates `world.db` with continent + starting region + starting chunks in < 5 seconds.
2. Approaching an un-generated area produces Level 1 chunks behind a sub-second transition.
3. Generated chunks persist in `world.db`; revisiting loads from SQLite, not re-generates.
4. Player modifications (looted containers, destroyed props) appear in `save_XX.db` only — `world.db` unchanged.
5. Two save slots sharing the same world see identical terrain but independent progress.
6. Content packs download and merge without disrupting gameplay.
7. Corrupt `world.db` can be recovered from seed — player progress in save DB survives.
8. Database size stays under 100 MB for a thorough playthrough (~20% of continent explored).
9. Chunk load from SQLite cache < 1 ms; chunk generation < 50 ms.
10. Offline play functions identically to online (minus content pack updates).
