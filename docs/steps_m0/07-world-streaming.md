# Step 7 — World Streaming

**Goal:** Chunked world loading/unloading, zone transitions, day/night cycle, fast travel.

**Depends on:** Step 1

---

## Deliverables

1. `ChunkData` — extends tile map to chunk-based system: 16×16 tile chunks with unique IDs
2. `WorldMapData` — grid of chunk references, loaded from a world definition file
3. `ChunkStreamingProcessor` — loads/unloads chunks based on player position (3×3 active grid)
4. Chunk serialization: save modified chunk state (destroyed objects, looted containers)
5. `ZoneDefinition` — zone metadata: name, biome type, radiation level, enemy difficulty tier
6. Zone transitions: trigger volumes at chunk edges, loading screen if needed
7. `DayNightCycleComponent` — tracks in-game time (24h cycle, configurable real-time ratio)
8. `DayNightCycleProcessor` — updates time, adjusts global lighting/ambient, publishes time events
9. `FastTravelService` — maintains discovered locations, validates travel (time cost, radiation on route)
10. Nav-mesh per chunk: generated at chunk load, stitched to adjacent chunks for cross-chunk pathfinding
11. Minimap data: fog-of-war per chunk, revealed as player explores

---

## Key Constants

| Constant | Value |
|----------|-------|
| Chunk size | 16×16 tiles |
| Active grid | 3×3 chunks (48×48 tiles visible) |
| Day length | 24 in-game hours = 48 real minutes (configurable) |
| Dawn | 06:00 |
| Dusk | 20:00 |
| Fast travel time cost | Distance ÷ 10 in-game hours |

---

## Streaming Flow

```
1. Player crosses chunk boundary
2. Determine new 3×3 grid
3. Diff with current loaded chunks
4. Async load new chunks (deserialize + spawn entities)
5. Unload old chunks (serialize modified state + destroy entities)
6. Stitch nav-mesh edges
```
