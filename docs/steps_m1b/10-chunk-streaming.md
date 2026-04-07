# Step 10 — Chunk Streaming & On-Demand Generation

**Work streams:** WS11 (Chunk Streaming Upgrades)
**Depends on:** Step 03 (renderer), Step 09 (generation)
**User-testable result:** Walk around the world → new chunks generate and stream in seamlessly. No loading screens, no pop-in.

---

## Goals

1. Upgrade `ChunkStreamingProcessor` to 5×5 active grid.
2. Wire SQLite as the only data source.
3. On-demand generation: missing chunks generated when the player approaches.
4. LRU cache for recently visited chunks.

---

## Tasks

### 10.1 — Configurable Grid Size

- [ ] Make active grid size a constructor parameter in `ChunkStreamingProcessor` (remove hardcoded 3×3)
- [ ] Default to 5×5 (decision D16: 160×160 m with 2 m tiles)
- [ ] Adjust all grid iteration and boundary logic

### 10.2 — SQLite Data Source

- [ ] Replace any remaining file-based chunk loading with `MapDataProvider.GetChunkData()`
- [ ] `MapDataProvider` is the single entry point for all chunk reads
- [ ] Pass `MapDataProvider` to `ChunkStreamingProcessor` via constructor injection

### 10.3 — On-Demand Generation

- [ ] When `ChunkStreamingProcessor` requests a chunk that returns null from `MapDataProvider`:
  - Call `WildernessChunkGenerator` (or `TownChunkGenerator` if within a curated town boundary)
  - Insert generated chunk into `world.db` via `WorldMapStore`
  - Load the newly inserted chunk
- [ ] Generation happens on a background thread; show a brief placeholder (flat terrain) until ready
- [ ] Generation must be fast enough to stay ahead of walking speed (<50 ms per chunk)

### 10.4 — LRU Cache

- [ ] Implement `ChunkLruCache` with configurable capacity (default 64)
- [ ] Chunks leaving the active 5×5 grid move to cache (not immediately disposed)
- [ ] Cache hit avoids SQLite read + mesh rebuild
- [ ] Eviction: dispose mesh + textures of oldest-accessed chunk when cache is full
- [ ] Content pack invalidation: clear cache entries for affected regions

### 10.5 — ChunkTerrainBuilder Integration

- [ ] Active grid chunk enter: `ChunkTerrainBuilder.Build()` → create Stride entities
- [ ] Active grid chunk exit: move `ChunkTerrainMesh` to LRU cache
- [ ] Trees (Step 07) also managed by the same enter/exit lifecycle

### 10.6 — Edge Cases

- [ ] Player teleporting (e.g., fast travel later): flush active grid, load new neighbourhood
- [ ] World boundary: chunks outside valid region bounds return empty/ocean terrain
- [ ] Region boundary: chunk near region edge may need neighbour region's data for stitching → `WorldMapStore.GetRegion()` for adjacent region

### 10.7 — Unit Tests

File: `tests/Oravey2.Tests/Streaming/ChunkLruCacheTests.cs`

- [ ] `Add_ThenGet_ReturnsCachedItem` — basic put/get
- [ ] `EvictionOrder_LeastRecentlyUsed` — oldest item evicted first
- [ ] `Capacity_Respected` — adding beyond capacity evicts
- [ ] `Get_UpdatesAccessTime` — accessed item not evicted next
- [ ] `Invalidate_RemovesEntry` — explicit invalidation works

File: `tests/Oravey2.Tests/Streaming/ChunkStreamingProcessorTests.cs`

- [ ] `ActiveGrid_5x5_Has25Chunks` — grid size produces correct chunk count
- [ ] `PlayerMoves_ChunksEnterAndExit` — simulate movement, verify enter/exit callbacks
- [ ] `MissingChunk_TriggersGeneration` — mock data provider returns null → generation called

### 10.8 — UI Tests

File: `tests/Oravey2.UITests/Streaming/ChunkStreamingTests.cs`

- [ ] `WalkAcrossChunkBoundary_NoGaps` — walk to a chunk edge, screenshot shows continuous terrain
- [ ] `WalkIntoUnexploredArea_TerrainAppears` — walk into area with no pre-generated chunks, terrain generates and appears

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Streaming."
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~ChunkStreaming"
```

**User test:** Start the game in Purmerend. Walk in any direction. New terrain appears seamlessly — no loading screens, no visible pop-in. Walk back to a previously visited area — it loads instantly from cache. The world extends in all directions.
