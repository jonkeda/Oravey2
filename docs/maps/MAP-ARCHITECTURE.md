# Map Architecture

## 1. How Maps Are Stored

### Two-Database Split

| Database | Purpose | Mutability |
|----------|---------|------------|
| `world.db` | All generated map data — shared across saves | Read-only at runtime |
| `save_XX.db` | Per-save deltas — fog of war, looted containers, destroyed objects | Read-write |

`MapDataProvider` merges both at load time: world data is the base, save-state overrides are applied on top.

### SQLite Schema (world.db)

```
continent       L3 heightmap + biome grid
region          L2 region rows (parent → continent)
chunk           L1 tile grids, compressed (parent → region)
entity_spawn    NPCs, trees, scavenge nodes (parent → chunk)
linear_feature  Roads, rivers, railways as spline node lists (parent → region)
poi             Towns, dungeons, landmarks (parent → region)
interior        Building floor plans + entities
terrain_modifier Flattens, channels, craters
```

### Tile Compression

Tile grids are serialised with `TileDataSerializer`:

```
TileData[16,16] → flat byte array (22 bytes per tile) → Brotli compress → store as BLOB
```

On load, the inverse path decompresses and reinterprets the bytes as `TileData` structs.

### RegionTemplate Files (.regiontemplate)

Pre-generated from real-world data (SRTM elevation + OpenStreetMap). Binary format:

```
Bytes 0–3    "ORTP" magic
Bytes 4–7    Int32 version (2 = GZip-compressed payload)
Byte 8+      GZip stream containing:
               String  Name
               Double  OriginLatitude, OriginLongitude
               Int32   ContinentOutlineCount → Vector2[]…
               Int32   RegionCount → per region:
                   String  Name
                   Double  GridOriginLat/Lon, GridCellSizeMetres
                   Int32   Rows, Cols → float[rows,cols] elevation
                   Int32   TownCount → TownEntry…
                   Int32   RoadCount → RoadSegment…
                   Int32   WaterCount → WaterBody…
                   Int32   RailwayCount → RailwaySegment…
                   Int32   LandUseCount → LandUseZone…
```

Version 1 files (uncompressed payload) are still readable; the reader checks the version header and skips GZip for v1.

---

## 2. How Zoom Levels Work

### Four-Level Hierarchy

```
L4  Globe          Entire planet      Menu/globe screen only (3D sphere texture)
L3  Continental    Country-scale      Coarse heightmap + faction territories
L2  Regional       20–100 km          Biome splats, town silhouettes, POI markers
L1  Local          16×16 m chunks     Full tile detail + entity models
```

### Camera Altitude → Active Level

The `ZoomLevelController` maps camera height to the active zoom level with smooth crossfade bands:

| Altitude (m) | Active Level | Rendering |
|--------------|-------------|-----------|
| 0 – 30 | L1 Local | Tile meshes, heightmap, entities, buildings |
| 30 – 50 | L1↔L2 blend | Alpha crossfade between L1 detail and L2 overview |
| 50 – 400 | L2 Regional | Heightmap terrain, biome colour splats, road lines, POI icons |
| 400 – 600 | L2↔L3 blend | Blend biome view into continental overlay |
| 600 – 2000+ | L3 Continental | Coarse terrain, faction territory borders, city dots |

### Time Scaling

Each zoom level multiplies game-time speed:

| Level | Time Scale | Effect |
|-------|-----------|--------|
| L1 Local | 1× | Real-time gameplay |
| L2 Regional | 60× | 1 real minute ≈ 1 game hour |
| L3 Continental | 1440× | 1 real minute ≈ 1 game day |

### LOD Data

`WorldLodCache` derives L2 and L3 data from L1 chunks:

- **L2 cell** (1 per chunk): average height + dominant surface type → biome classification
- **L3 cell** (1 per 4×4 L2 group): averaged L2 data → coarse biome

Biome derivation uses surface type distribution, water presence, and average height to classify each cell as one of: Grassland, Forest, Desert, Urban, Wasteland, Snow, Water, Mountain.

---

## 3. How Regions and Towns Are Stored

### Generation Pipeline

```
.regiontemplate file (OSM + SRTM real-world data)
        │
        ▼
   TownCurator (LLM)
   Selects 8–15 towns, assigns post-apocalyptic names,
   factions, threat levels, roles, descriptions
        │
        ▼
   CuratedWorldPlan
   ├── CuratedRegion[]
   │     └── CuratedTown[]
   │
   ▼
   WorldGenerator orchestrates three phases:
   │
   ├── ContinentGenerator  → L3: 1×1 coarse biome grid
   │
   ├── RegionGenerator     → L2: heightmap + biome grid + POIs
   │       also: RoadSelector → filters roads to curated-town area
   │       also: RiverGenerator → derives rivers from water bodies
   │
   └── ChunkGenerators     → L1: 16×16 tile chunks
           ├── WildernessChunkGenerator  (grass, forest, rubble)
           └── TownChunkGenerator        (streets, buildings)
```

### Region Storage

Regions are rows in the `region` table, each linking to a parent continent:

```sql
INSERT INTO region (continent_id, name, grid_x, grid_y, biome, base_height);
```

Each region owns:
- **Chunks** (`chunk` table) — L1 tile grids at (grid_x, grid_y) within the region
- **Linear features** (`linear_feature` table) — roads, rivers, railways stored as JSON spline nodes
- **POIs** (`poi` table) — towns, dungeons, landmarks at chunk coordinates

### Town Storage

Towns are stored at two levels:

**1. As POIs** in `world.db`:
```sql
INSERT INTO poi (region_id, name, type, grid_x, grid_y, description);
-- type = 'town', grid_x/grid_y = chunk coordinates of town centre
```

**2. As curated metadata** (JSON in `meta` table):
```json
{
  "gameName": "Rustwater",
  "realName": "Purmerend",
  "latitude": 52.50, "longitude": 4.95,
  "gamePosition": { "x": 12500, "y": 8300 },
  "role": "trading_hub",
  "faction": "Haven Guard",
  "threatLevel": 2,
  "description": "A fortified market town on the old canal…",
  "boundaryPolygon": [[x,y], …]
}
```

**3. As L1 chunks**: `TownChunkGenerator` produces chunks around the town centre with:
- Road skeleton rasterised from OSM polylines
- 2–32 building footprints (scaled by `TownCategory`: Hamlet → Metropolis)
- Concrete/asphalt surface tiles replacing wilderness terrain

### Linear Features (Roads, Rivers, Railways)

```csharp
LinearFeature {
    Type    // Road, Highway, Path, River, Canal, Stream, Rail
    Style   // "asphalt_2lane", "dirt_track", "river_wide"
    Width   // metres
    Nodes[] // Vector2 waypoints (with optional height override for bridges/tunnels)
}
```

Stored per-region in the `linear_feature` table as JSON node arrays. Rendered at all zoom levels with varying line widths.

### Chunk Streaming

`ChunkStreamingProcessor` maintains a 5×5 active grid centred on the player:

```
1. Check in-memory WorldMapData
2. Check 64-entry LRU cache (recently unloaded chunks)
3. Query SQLite via MapDataProvider
4. Generate on-demand (deterministic from world seed + chunk coords)
5. Fallback: flat default chunk
```

Generation is deterministic: `HashCode.Combine(seed, chunkX, chunkY)` produces identical terrain on every load.
