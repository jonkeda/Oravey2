# Real-World-Anchored World Generation

**Status:** Draft  
**Milestone:** M1b  
**Depends on:** Map Storage (map-storage-incremental.md), Zoom Levels (multi-scale-zoom-levels.md), MapGeneratorService

---

## Summary

The game world is modelled on the **real Earth** — continents, coastlines, elevation, and major geographic features come from real data. But the world is **curated for gameplay**: an LLM selects a fun subset of towns/cities per region, towns are procedurally generated within their real-world boundaries, and large empty areas (oceans, deserts, ice) are sparse but traversable.

```
Real-World Data (OSM + Elevation)
        │
        ▼
┌─────────────────────────────────┐
│     World Template Builder       │
│                                   │
│  1. Import real elevation/coast  │
│  2. Import real road network     │
│  3. Import town/city boundaries  │
│  4. Import biome/landuse zones   │
│                                   │
│  Produces: WorldTemplate         │
│  (static reference data)         │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│     LLM Curation Pass            │
│                                   │
│  Per region, LLM selects:        │
│  - Which towns to include        │
│  - What makes each interesting   │
│  - Faction assignments           │
│  - Threat/difficulty gradient    │
│                                   │
│  Produces: CuratedWorldPlan      │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│     Procedural Generation        │
│                                   │
│  For each selected town:         │
│  - Real boundary + rough shape   │
│  - Procedural buildings/layout   │
│  - Procedural NPCs, loot, quests│
│                                   │
│  For terrain between towns:      │
│  - Real elevation data           │
│  - Procedural surface detail     │
│  - Roads from real road network  │
│                                   │
│  Produces: Chunks in world.db    │
└─────────────────────────────────┘
```

---

## Real-World Data Sources

### Elevation

| Source | Resolution | Coverage | Format |
|--------|-----------|----------|--------|
| SRTM (Shuttle Radar Topography) | ~30 m | 60°N to 56°S | GeoTIFF heightmap |
| ASTER GDEM | ~30 m | 83°N to 83°S | GeoTIFF |
| ETOPO1 | ~1.8 km | Global (incl. ocean floor) | NetCDF / GeoTIFF |

**Usage:** SRTM/ASTER for land elevation → Level 2/3 heightmaps. ETOPO1 for ocean depth and polar regions → globe texture. Downloaded once at build time, baked into a compact binary format.

### OpenStreetMap

| Data | OSM Tags | Usage |
|------|----------|-------|
| Coastlines | `natural=coastline` | Continent outlines for globe + L3 |
| Country/region boundaries | `admin_level=2,4` | Region grid alignment |
| City/town boundaries | `place=city/town/village` + `boundary=administrative` | Town boundary polygons |
| Major roads | `highway=motorway/trunk/primary/secondary` | Level 2 road network |
| Local roads | `highway=tertiary/residential` | Town internal road layout (reference only) |
| Water bodies | `natural=water`, `waterway=river` | Lakes, rivers for Level 2/3 |
| Land use | `landuse=forest/farmland/industrial/residential` | Biome classification |
| Railways | `railway=rail` | Rail network for Level 2 |

**Usage:** Extracted via Overpass API or PBF file processing at build time. Stored as a **WorldTemplate** — a read-only reference dataset that the generator samples from.

### Data Processing Pipeline (Build Time)

```
OSM PBF file (planet or regional extract)
    │
    ├──▶ osmium / osmosis filter
    │       └──▶ Extract: boundaries, roads, water, landuse, POIs
    │
    ├──▶ SRTM/ASTER GeoTIFF tiles
    │       └──▶ Merge + downsample to target resolution
    │
    └──▶ WorldTemplateCompiler
            │
            ├──▶ continent_templates.bin    (~50 MB — global elevation + coast)
            ├──▶ region_templates/          (~2 MB per region — roads, boundaries, landuse)
            └──▶ town_catalog.json          (~5 MB — all known towns with name, pop, boundary)
```

This pipeline runs **once** during development. The output ships with the game as static reference data (not in `world.db` — separate read-only files).

---

## World Template

The template is the **real-world reference** that the generator reads from. It never changes at runtime.

```csharp
public class WorldTemplate
{
    // Global
    public ElevationGrid GlobalElevation { get; }         // ~1 km resolution, entire planet
    public IReadOnlyList<ContinentPolygon> Continents { get; }

    // Per-region (loaded on demand)
    public RegionTemplate GetRegion(float lat, float lon);
}

public class RegionTemplate
{
    public ElevationGrid Elevation { get; }               // ~30 m resolution
    public IReadOnlyList<RoadSegment> Roads { get; }      // Real road network
    public IReadOnlyList<WaterFeature> Water { get; }     // Rivers, lakes
    public IReadOnlyList<LandUseZone> LandUse { get; }   // Forest, farmland, urban, etc.
    public IReadOnlyList<TownEntry> Towns { get; }        // All known towns in this region
    public IReadOnlyList<RailSegment> Railways { get; }
}

public record TownEntry(
    string Name,
    float Latitude,
    float Longitude,
    int Population,                    // From OSM/census data
    float AreaKm2,                     // Boundary area
    IReadOnlyList<Vector2> Boundary,   // Simplified polygon
    TownCategory Category);            // City, Town, Village, Hamlet

public enum TownCategory : byte
{
    Hamlet,      // < 500 pop
    Village,     // 500–5 000
    Town,        // 5 000–100 000
    City,        // 100 000–1 000 000
    Metropolis   // > 1 000 000
}
```

---

## LLM Town Curation

The LLM selects **which towns appear in the game** from the full catalog. This is the key step that makes the world fun rather than exhaustively realistic.

### Selection Prompt

For each region, the LLM receives the town catalog and selects a subset:

```
System: You are a post-apocalyptic world designer. Given a list of real-world 
towns in a region, select a subset that creates fun gameplay. Consider:

- Geographic spacing: towns should be 20-50 km apart (Level 2 travel distance)
- Variety: mix cities, towns, and small settlements
- Landmarks: prefer towns near interesting geography (coast, rivers, mountains)
- Narrative: each town should have a distinct character/purpose in the wasteland
- Density: aim for 8-15 towns per region of ~200×200 km
- Keep real relative positions: if A is north of B in reality, keep it that way

For each selected town, provide:
- name (real name or post-apocalyptic variant)
- role (trading hub, military outpost, raider camp, safe haven, ruin, etc.)
- faction (who controls it)
- threat_level (1-5)
- brief description (1-2 sentences of flavour)

Region: Noord-Holland, Netherlands
Towns available: [Purmerend (pop 81000), Amsterdam (pop 872000), 
Haarlem (pop 162000), Alkmaar (pop 109000), Zaandam (pop 77000), 
Hoorn (pop 73000), Den Helder (pop 55000), Hilversum (pop 90000), ...82 more]
```

### Example LLM Output

```json
{
  "region": "Noord-Holland",
  "selected_towns": [
    {
      "name": "Amsterdam",
      "real_name": "Amsterdam",
      "role": "ruined_metropolis",
      "faction": "free_traders",
      "threat_level": 4,
      "description": "Flooded canal city, partially submerged. Major trading hub in the dry eastern districts."
    },
    {
      "name": "Purmerend",
      "real_name": "Purmerend",
      "role": "safe_haven",
      "faction": "survivors_coalition",
      "threat_level": 2,
      "description": "Walled farming community on reclaimed polder land. Starting area."
    },
    {
      "name": "Fort Den Helder",
      "real_name": "Den Helder",
      "role": "military_outpost",
      "faction": "navy_remnants",
      "threat_level": 3,
      "description": "Former naval base, now a fortified port. Controls sea access to the north."
    },
    {
      "name": "Alkmaar",
      "real_name": "Alkmaar",
      "role": "raider_camp",
      "faction": "raiders",
      "threat_level": 4,
      "description": "Cheese market turned into a raider stronghold. Controls the northern road."
    }
  ],
  "excluded_reasoning": "Removed 80+ small towns to maintain spacing and avoid clutter. Kept major cities and strategically positioned settlements."
}
```

### Curation Rules

| Rule | Detail |
|------|--------|
| Minimum spacing | No two selected towns closer than ~15 km (prevents clutter at Level 2) |
| Maximum spacing | No gap larger than ~60 km without a point of interest (prevents boring travel) |
| Category distribution | At least 1 city, 2–4 towns, 2–4 villages/hamlets per region |
| Real positions | Latitude/longitude from OSM — converted to game world coordinates preserving relative positions |
| Narrative variety | LLM ensures variety: not all towns are the same faction/role |
| Difficulty gradient | Towns closer to start are lower threat; difficulty increases with distance |

### Storing the Curated Plan

```csharp
public record CuratedWorldPlan(
    int WorldSeed,
    IReadOnlyList<CuratedContinent> Continents);

public record CuratedContinent(
    string Name,
    IReadOnlyList<CuratedRegion> Regions);

public record CuratedRegion(
    string Name,
    IReadOnlyList<CuratedTown> Towns,
    IReadOnlyList<CuratedWildernessFeature> WildernessFeatures);

public record CuratedTown(
    string GameName,
    string RealName,
    float Latitude,
    float Longitude,
    IReadOnlyList<Vector2> Boundary,
    TownCategory Category,
    string Role,
    string FactionId,
    int ThreatLevel,
    string Description);
```

The curated plan is generated **once** at world creation and stored in `world.db` (a `curated_plan` table). It's the authoritative list of what exists in the game world.

---

## Geographic Coordinate Mapping

Real-world coordinates (latitude/longitude) map to game world coordinates (X/Z in metres):

```csharp
public class GeoMapper
{
    private readonly float _originLat;   // Centre of the starting region
    private readonly float _originLon;
    private readonly float _scale;       // Metres per degree (adjustable)

    public GeoMapper(float originLat, float originLon, float scale = 111_000f)
    {
        _originLat = originLat;
        _originLon = originLon;
        _scale = scale;  // ~111 km per degree at equator
    }

    public Vector2 ToGameCoords(float lat, float lon)
    {
        float x = (lon - _originLon) * _scale * MathF.Cos(_originLat * MathF.PI / 180f);
        float z = (lat - _originLat) * _scale;
        return new Vector2(x, z);
    }

    public (float lat, float lon) ToGeoCoords(Vector2 gamePos)
    {
        float lon = gamePos.X / (_scale * MathF.Cos(_originLat * MathF.PI / 180f)) + _originLon;
        float lat = gamePos.Y / _scale + _originLat;
        return (lat, lon);
    }
}
```

Distances are preserved: Purmerend to Amsterdam is ~15 km in reality → ~15 km in game. The coordinate system distorts slightly far from the origin (Mercator effect), but at regional scale (<500 km) it's negligible.

---

## Town Generation (Level 1)

When the player approaches a curated town, the generator creates L1 chunks within the town boundary.

### Input

```
CuratedTown:       Purmerend, safe_haven, threat 2
TownEntry:         boundary polygon, ~8 km² area
RegionTemplate:    elevation, road network, water, landuse
```

### Generation Pipeline

```
1. Map town boundary polygon to game chunk grid
   → Identify which 16×16 chunks fall inside/overlap

2. Sample real elevation for each chunk
   → WorldTemplate.GetRegion().Elevation at chunk coordinates
   → Convert to TileData.HeightLevel values

3. Generate road network within boundary
   → Use real OSM road layout as skeleton (major roads only)
   → Extend with procedural residential streets between major roads
   → Store as LinearFeatures

4. Place building zones
   → LandUse data: residential, commercial, industrial areas
   → Within each zone, procedurally place building footprints
   → Density based on TownCategory (City = dense, Village = sparse)
   → Vary by role: "raider_camp" → damaged, barricaded
                    "safe_haven" → repaired, gardens
                    "ruined_metropolis" → collapsed, overgrown

5. Generate individual buildings
   → Size/type from zone (residential → houses, commercial → shops)
   → Post-apocalyptic state from threat_level
   → Assign StructureIds, create entity spawns (NPCs, loot, props)

6. Place natural features
   → Trees in parks/outskirts (from landuse=forest/park)
   → Water features (from OSM water data)
   → Rubble/debris/craters (from threat_level)

7. LLM detail pass (async, non-blocking)
   → Name generators for NPCs
   → Specific quest hooks, flavour text, shop inventories
   → Can arrive after terrain is already playable
```

### Building Density Control

Not all real-world buildings are included. The generator uses a **density budget** per chunk:

| Town Category | Max Buildings per Chunk | Coverage |
|--------------|------------------------|----------|
| Hamlet | 2–4 | ~10% of real buildings |
| Village | 4–8 | ~15% |
| Town | 8–16 | ~20% |
| City | 12–24 | ~25% |
| Metropolis | 16–32 | ~15% (mostly ruins, few intact) |

Buildings are placed at **intersections and along major roads** first, then fill in. This creates a recognisable town layout without overwhelming detail.

### Building Selection Algorithm

```csharp
public IReadOnlyList<BuildingFootprint> SelectBuildings(
    TownEntry town, CuratedTown curated, LandUseZone[] zones, int budgetPerChunk)
{
    var candidates = new PriorityQueue<BuildingCandidate, float>();

    foreach (var zone in zones)
    {
        // Score each potential building location
        // Higher score = more likely to be included
        float score = 0;
        score += DistanceToRoad(zone) < 20f ? 3f : 0f;      // Near roads
        score += IsAtIntersection(zone) ? 5f : 0f;           // At intersections
        score += IsLandmark(zone) ? 10f : 0f;                // Churches, stations
        score += zone.Type == LandUseType.Commercial ? 2f : 0f; // Shops are fun
        score += Random.NextFloat() * 2f;                     // Variety

        candidates.Enqueue(new BuildingCandidate(zone), -score);
    }

    // Take top N per chunk, ensuring minimum spacing
    return PickWithSpacing(candidates, budgetPerChunk, minSpacing: 8f);
}
```

### Landmark Preservation

Certain real-world features are **always included** when they exist in OSM data:

| Feature | OSM Tag | Game Representation |
|---------|---------|-------------------|
| Church/cathedral | `building=church` | Landmark, quest location |
| Train station | `railway=station` | Fast travel hub |
| Hospital | `amenity=hospital` | Medical supplies, quest |
| Town hall | `amenity=townhall` | Faction HQ |
| School | `amenity=school` | Shelter, community area |
| Factory | `building=industrial` | Scavenge-rich, dangerous |
| Bridge | `bridge=yes` | Strategic chokepoint |
| Harbour | `harbour=yes` | Vehicle access, trade |

These serve as **anchor points** that make towns feel recognisable even with reduced building count.

---

## Terrain Between Towns (Level 1)

The countryside between towns uses real elevation but procedural surface detail.

### Generation

```
1. Real elevation → HeightLevel values
2. Real landuse (OSM) → SurfaceType:
   - farmland → Grass
   - forest → Grass + tree entity spawns
   - water → WaterLevel set, LiquidType.Water
   - industrial → Concrete + structures
   - heath/moor → Sand or Dirt
3. Roads from WorldTemplate → LinearFeatures
4. Rivers/canals from WorldTemplate → LinearFeatures (River/Stream type)
5. Procedural decay:
   - Cracks in roads, abandoned vehicles
   - Overgrown farmland (extra trees, tall grass surface)
   - Random scavenge nodes (roadside wrecks, ruins)
6. Occasional wilderness POIs:
   - Radio tower, abandoned gas station, military checkpoint
   - Placed by LLM curation or procedurally at road intersections
```

---

## Regional Road Selection

Not all real roads are included — only those connecting selected towns plus major highways.

```csharp
public IReadOnlyList<RoadSegment> SelectRoads(
    RegionTemplate region, IReadOnlyList<CuratedTown> towns)
{
    var selectedRoads = new List<RoadSegment>();

    // 1. Always include: motorways and trunk roads
    selectedRoads.AddRange(region.Roads
        .Where(r => r.Category is RoadCategory.Motorway or RoadCategory.Trunk));

    // 2. Include primary roads that connect selected towns
    var townPositions = towns.Select(t => ToGameCoords(t.Latitude, t.Longitude)).ToList();
    selectedRoads.AddRange(region.Roads
        .Where(r => r.Category == RoadCategory.Primary
            && ConnectsNearTowns(r, townPositions, maxDistance: 5000f)));

    // 3. Add secondary roads to prevent dead ends
    //    (ensure the road graph is connected between all selected towns)
    var graph = BuildRoadGraph(selectedRoads, townPositions);
    var missing = FindDisconnectedTowns(graph, townPositions);
    selectedRoads.AddRange(FindConnectingRoads(region.Roads, missing));

    return selectedRoads;
}
```

---

## Empty / Sparse Regions

Oceans, deserts, Antarctica, and other low-interest areas are **traversable but sparse**.

### Region Classification

```csharp
public enum RegionClassification : byte
{
    Populated,    // Has curated towns — full generation
    Sparse,       // Traversable with rare outposts — minimal generation
    Water,        // Ocean/sea — boat travel, islands, rare platforms
    Extreme       // Antarctica, deep desert — harsh, very rare POIs
}
```

### Sparse Generation

| Classification | What Gets Generated |
|---------------|-------------------|
| Populated | Towns, roads, full countryside detail |
| Sparse | Elevation + biome terrain, major roads only, 1–3 outposts per region |
| Water | Flat ocean heightmap, occasional island, offshore platform, shipwreck |
| Extreme | Harsh terrain (ice/sand), weather effects, 0–1 outposts per region |

Sparse regions still have procedural **points of interest** at wide spacing:

| POI Type | Spacing | Purpose |
|----------|---------|---------|
| Gas station | ~50 km | Fuel, rest, minor loot |
| Military checkpoint | ~80 km | High-value loot, combat |
| Radio tower | ~100 km | Communication, quest trigger |
| Abandoned settlement | ~40 km | Scavenging, shelter |
| Anomaly zone | ~120 km | Unique hazards, rare loot |

These are placed procedurally along roads or at random positions, seeded from the world seed + region coordinates.

---

## World Creation Flow

```
┌─────────────────────────────────────────────────────────┐
│                   New Game                                │
│                                                           │
│  1. Player picks starting location (or default)          │
│     → origin = Purmerend, NL  (lat 52.50, lon 4.95)     │
│                                                           │
│  2. Load WorldTemplate                                   │
│     → Global elevation, continent outlines               │
│     → Regional data for starting area                    │
│                                                           │
│  3. Generate globe (Level 4)                             │
│     → Project continents onto sphere texture             │
│     → ~100 ms                                            │
│                                                           │
│  4. Generate Level 3 continent data                      │
│     → Sample elevation at ~1 km resolution               │
│     → Classify biome zones                               │
│     → ~500 ms per continent                              │
│                                                           │
│  5. LLM curation for starting region                     │
│     → Send town catalog → receive curated plan           │
│     → Select roads connecting curated towns              │
│     → ~2–5 s (API call)                                  │
│                                                           │
│  6. Generate Level 2 for starting region                 │
│     → Height from template, biome from landuse           │
│     → Roads, rivers, POI markers                         │
│     → ~200 ms                                            │
│                                                           │
│  7. Generate Level 1 for starting town                   │
│     → Full chunk generation for Purmerend (3×3 chunks)   │
│     → Buildings, NPCs, loot, trees                       │
│     → ~500 ms                                            │
│                                                           │
│  8. Game starts — player is in Purmerend                 │
│     → Total: ~5–8 s                                      │
│                                                           │
│  9. Background: LLM curates adjacent regions             │
│     → Queued, non-blocking                               │
│     → Ready by the time player travels there             │
└─────────────────────────────────────────────────────────┘
```

### Progressive LLM Curation

The LLM doesn't curate the entire world at once — it curates **region by region** as the player approaches:

```
Player in Purmerend (Noord-Holland curated ✓)
    │
    ├── Background: Curate Zuid-Holland (to the south)
    ├── Background: Curate Flevoland (to the east)
    ├── Background: Curate Friesland (to the north)
    │
    Player drives south → Zuid-Holland ready ✓
    │
    ├── Background: Curate Zeeland, Noord-Brabant, Utrecht
    ...
```

Curation results are cached in `world.db` (`curated_plan` table). Once a region is curated, it never needs the LLM again.

---

## World Consistency Guarantees

| Guarantee | How |
|-----------|-----|
| Cities in correct relative positions | Real lat/lon from OSM → `GeoMapper` preserves relative positions |
| Recognisable geography | Real elevation data → hills, valleys, coastlines match reality |
| Roads connect the right places | Real road network from OSM, filtered to curated towns |
| Rivers and lakes in correct places | Real water features from OSM |
| Town character matches reality | LLM receives real population, geography, nearby features |
| Consistent across sessions | Curated plan stored in `world.db`; procedural generation seeded from plan |

### What's NOT Accurate

| Aspect | Reality vs Game |
|--------|----------------|
| Building count | ~15–25% of real buildings included |
| Building appearance | Procedural — not matching real architecture |
| Population | Scaled down dramatically |
| Small roads | Only major roads; residential streets are procedural |
| Exact building positions | Within the right neighbourhood, not GPS-accurate |
| Timeline | Post-apocalyptic decay applied everywhere |

---

## Database Additions

```sql
-- WorldTemplate reference (read-only, shipped with game)
-- NOT in world.db — separate files

-- Curated plan stored in world.db
CREATE TABLE curated_region (
    region_key     TEXT PRIMARY KEY,    -- e.g. "NL-NH" (country-province)
    curated_json   TEXT NOT NULL,       -- Full CuratedRegion JSON
    curated_utc    TEXT NOT NULL,
    llm_model      TEXT NOT NULL        -- Which model generated this
);

CREATE TABLE curated_town (
    town_id        INTEGER PRIMARY KEY,
    region_key     TEXT NOT NULL REFERENCES curated_region(region_key),
    game_name      TEXT NOT NULL,
    real_name      TEXT NOT NULL,
    latitude       REAL NOT NULL,
    longitude      REAL NOT NULL,
    category       INTEGER NOT NULL,
    role           TEXT NOT NULL,
    faction_id     TEXT,
    threat_level   INTEGER NOT NULL,
    description    TEXT,
    boundary_json  TEXT NOT NULL         -- Simplified polygon
);

CREATE INDEX idx_curated_town_region ON curated_town(region_key);

-- Region classification for sparse/water/extreme handling
CREATE TABLE region_classification (
    region_key     TEXT PRIMARY KEY,
    classification INTEGER NOT NULL      -- RegionClassification enum
);
```

---

## Files to Create / Modify

| Action | File | Notes |
|--------|------|-------|
| Create | `MapGen/WorldTemplate/WorldTemplate.cs` | Real-world reference data model |
| Create | `MapGen/WorldTemplate/WorldTemplateLoader.cs` | Load elevation + OSM data from packed files |
| Create | `MapGen/WorldTemplate/GeoMapper.cs` | Lat/lon ↔ game coordinates |
| Create | `MapGen/WorldTemplate/ElevationGrid.cs` | Elevation data container + sampling |
| Create | `MapGen/WorldTemplate/TownEntry.cs` | Real-world town catalog entry |
| Create | `MapGen/Curation/TownCurator.cs` | LLM prompt building + response parsing |
| Create | `MapGen/Curation/CuratedWorldPlan.cs` | Curation result records |
| Create | `MapGen/Curation/RoadSelector.cs` | Filter real roads to curated town set |
| Create | `MapGen/Curation/RegionClassifier.cs` | Classify regions as Populated/Sparse/Water/Extreme |
| Create | `MapGen/Generation/TownGenerator.cs` | Generate L1 chunks within town boundary |
| Create | `MapGen/Generation/BuildingPlacer.cs` | Density-budgeted building placement |
| Create | `MapGen/Generation/CountrysideGenerator.cs` | Terrain between towns from real elevation |
| Create | `MapGen/Generation/SparseRegionGenerator.cs` | Minimal generation for empty areas |
| Create | `Tools/WorldTemplateCompiler/` | Build-time tool: OSM + elevation → packed templates |
| Modify | `Services/MapGeneratorService.cs` | Integrate WorldTemplate + CuratedWorldPlan |
| Modify | `Data/WorldMapStore.cs` | Add curated_region/curated_town tables |

---

## Acceptance Criteria

1. Starting a new game in Purmerend produces a recognisable (approximate) layout with The Hague south of Amsterdam and Alkmaar to the north.
2. The LLM selects 8–15 towns per region from the OSM catalog, with variety in roles and factions.
3. Generated towns have buildings concentrated along real major roads, with density matching the budget.
4. Landmark buildings (churches, stations, hospitals) are always included when they exist in OSM.
5. Terrain between towns uses real elevation — hills, rivers, and coastlines are in the right places.
6. Roads from the real network connect curated towns; minor roads are omitted.
7. Sparse regions (deserts, oceans) are traversable with rare outposts every 40–100 km.
8. Adjacent regions are curated in the background before the player arrives.
9. The curated plan is stored in `world.db` and does not require LLM calls on subsequent loads.
10. Relative positions of all geographic features match the real world within ~5% distance error.
