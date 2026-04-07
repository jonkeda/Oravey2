# Step 09 — Procedural Map Generation

**Work streams:** WS9 (Procedural Map Generation)
**Depends on:** Step 01 (data model), Step 02 (storage), Step 08 (WorldTemplate)
**User-testable result:** "New Game" creates a `world.db` with continent/region/chunk data generated from real-world data. LLM curates towns. An initial area is explorable.

---

## Goals

1. Build the generation pipeline: WorldTemplate → LLM curation → region skeleton → chunk detail.
2. Deterministic seed-based chunk generation for terrain.
3. LLM town curation (required — game waits if unavailable).
4. Remove old LLM-blueprint generation code.

---

## Tasks

### 9.1 — Remove Old Generation Pipeline

- [ ] Delete `PromptBuilder.cs` from `Oravey2.MapGen`
- [ ] Delete `BlueprintCollector.cs` from `Oravey2.MapGen`
- [ ] Delete `ValidateBlueprintTool.cs`, `WriteBlueprintTool.cs`, `CheckOverlapTool.cs`, `CheckWalkabilityTool.cs` from `Oravey2.MapGen/Tools`
- [ ] Delete `MapBlueprint.cs` and related blueprint models from `Oravey2.Core/World/Blueprint/`
- [ ] Delete `TerrainCompiler.cs`, `WaterCompiler.cs`, `StructureCompiler.cs` from Blueprint folder
- [ ] Clean up any remaining references

### 9.2 — Generation Pipeline Stages

- [ ] Create `MapGen/Generation/WorldGenerator.cs` — top-level orchestrator
- [ ] Pipeline: Load WorldTemplate → LLM curation → Level 3 continent → Level 2 regions → Level 1 starting chunks
- [ ] All results written to `world.db` via `WorldMapStore`

### 9.3 — CuratedWorldPlan

- [ ] Create `MapGen/Generation/CuratedWorldPlan.cs` — plan record with continents, regions, towns
- [ ] Create `MapGen/Generation/CuratedTown.cs` — `GameName`, `RealName`, `LatLon`, `Role`, `Faction`, `ThreatLevel`, `Description`
- [ ] Create `MapGen/Generation/CuratedRegion.cs` — name, bounds, list of curated towns
- [ ] Store in `world.db` as JSON in a `curated_plan` meta entry

### 9.4 — LLM Town Curation

- [ ] Create `MapGen/Generation/TownCurator.cs`
- [ ] Load town catalog from `RegionTemplate.Towns`
- [ ] Build LLM prompt: town list → select 8–15 per region with roles, factions, threat levels
- [ ] Parse LLM JSON response → `CuratedRegion`
- [ ] Validation: minimum spacing (~15 km), category distribution, difficulty gradient
- [ ] If LLM unavailable: block and retry (game waits as per D9 decision)

### 9.5 — Level 3 Continent Generator

- [ ] Create `MapGen/Generation/ContinentGenerator.cs`
- [ ] Sample WorldTemplate elevation at ~1 km resolution
- [ ] Classify biome zones from land use data
- [ ] Store in `continent` table via `WorldMapStore`

### 9.6 — Level 2 Region Generator

- [ ] Create `MapGen/Generation/RegionGenerator.cs`
- [ ] Regional heightmap: sample from L3 + add detail noise seeded from region coordinates
- [ ] Biome grid from land use data
- [ ] Insert POI markers from curated plan (towns as POIs)
- [ ] Insert linear features (roads, rivers) selected from WorldTemplate
- [ ] Store in `region` table

### 9.7 — Road Selection

- [ ] Create `MapGen/Generation/RoadSelector.cs`
- [ ] Keep all motorways + roads connecting curated towns
- [ ] Discard roads to non-curated towns
- [ ] Catmull-Rom smoothing with real elevation data
- [ ] Store as `LinearFeature` records in `linear_feature` table

### 9.8 — River/Canal Generation

- [ ] Map OSM water features to `LinearFeature` records with `River`/`Stream`/`Canal` type
- [ ] Width from OSM waterway class
- [ ] Store in `linear_feature` table

### 9.9 — Level 1 Wilderness Chunk Generator

- [ ] Create `MapGen/Generation/WildernessChunkGenerator.cs`
- [ ] Input: region seed + grid coordinates → deterministic output
- [ ] Sample real elevation → `HeightLevel` values (quantise continuous elevation to byte)
- [ ] Map land use to `SurfaceType` (farmland→Grass, forest→Grass+Forested, residential→Concrete, etc.)
- [ ] Add decay detail: cracks, rubble, abandoned props seeded from `VariantSeed`
- [ ] Optional tree spawns in forested areas → `entity_spawn` rows
- [ ] Store in `chunk` table

### 9.10 — Level 1 Town Chunk Generator

- [ ] Create `MapGen/Generation/TownChunkGenerator.cs`
- [ ] Input: `CuratedTown` + `TownEntry` boundary polygon + region template
- [ ] Map boundary polygon to chunk grid
- [ ] Road skeleton from OSM major roads within boundary
- [ ] Building zone classification from land use
- [ ] Density-budget placement: prioritise intersections + major roads, preserve landmarks
- [ ] Chunk mode = `Hybrid`
- [ ] Building footprints → `StructureId` on tiles
- [ ] Store in `chunk` + `entity_spawn` tables

### 9.11 — Building Selection

- [ ] Landmark preservation: churches, stations, hospitals, town halls always included (matched by OSM tags)
- [ ] Density budget per chunk: Hamlet 2–4, Village 4–8, Town 8–16, City 12–24
- [ ] Priority queue: intersections first, then road-facing, then fill
- [ ] Minimum 8 m spacing between buildings

### 9.12 — Sparse Region Generation

- [ ] For regions classified as `Sparse` (no curated towns): elevation + biome + major roads only
- [ ] Place 1–3 procedural outposts (gas station, checkpoint, radio tower) at road intersections or random positions
- [ ] For `Water` regions: flat ocean, occasional island POI
- [ ] For `Extreme` regions: harsh terrain, 0–1 outposts

### 9.13 — New Game Flow

- [ ] Wire `WorldGenerator` into the game startup
- [ ] "New Game" → generate world seed → run pipeline → create `world.db` → load starting chunks at Purmerend
- [ ] Show progress UI during generation ("Generating world… Curating towns… Building terrain…")

### 9.14 — Unit Tests

File: `tests/Oravey2.Tests/Generation/WildernessChunkGeneratorTests.cs`

- [ ] `SameSeedSameCoords_ProducesIdenticalTileData` — deterministic generation
- [ ] `DifferentCoords_ProducesDifferentTileData` — adjacent chunks aren't identical
- [ ] `HeightLevels_WithinByteRange` — no overflow

File: `tests/Oravey2.Tests/Generation/TownChunkGeneratorTests.cs`

- [ ] `TownChunk_Mode_IsHybrid` — generated town chunk has `ChunkMode.Hybrid`
- [ ] `BuildingCount_WithinDensityBudget` — count of StructureId>0 tiles ≤ budget
- [ ] `Landmarks_ArePreserved` — if input has a church, output has a structure at that location

File: `tests/Oravey2.Tests/Generation/RoadSelectorTests.cs`

- [ ] `AllCuratedTowns_AreConnected` — every curated town has at least one road reaching it
- [ ] `NonCuratedTowns_RoadsExcluded` — roads only to excluded towns are dropped

File: `tests/Oravey2.Tests/Generation/TownCuratorTests.cs`

- [ ] `CuratedTowns_WithinSpacingLimits` — no two curated towns closer than 15 km
- [ ] `CuratedTowns_CountInRange` — 8–15 per region

### 9.15 — UI Tests

File: `tests/Oravey2.UITests/Generation/WorldGenerationTests.cs`

- [ ] `NewGame_GeneratesWorld_TerrainVisible` — start new game with test WorldTemplate, verify terrain appears after generation completes

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Generation."
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~WorldGeneration"
```

**User test:** Click "New Game." Progress UI shows generation stages. After ~5–10 seconds, you're standing in the Purmerend area with terrain generated from real elevation data, roads connecting to nearby towns, and a town area with buildings.
