# Map & Generation — Decisions & Implementation Tasks

**Status:** Pre-Development
**Milestone:** M1b
**Scope:** Map rendering, terrain, storage, generation pipeline only. NPCs, factions, quests, and combat mechanics are deferred to later milestones.

---

## How to Use This Document

Each section contains:

- **Decision** — a question that must be answered before implementation.
- **Options** — as a checklist. Pick one (or propose an alternative).
- **Recommendation** — the suggested path, if one is clear.
- **Status** — `[ ] Open`, `[x] Decided`, `[~] Deferred`.

After all decisions are made, the **Task Lists** at the bottom become the implementation plan.

---

## Part 1: Decisions

### D1. Tile Scale — What Does 1 Tile Represent?

The current code has no explicit scale. The designs assume 1 tile ≈ 1 m, but this has cascading effects on chunk size, rendering detail, and world size.

- [ ] **A) 1 m per tile** — chunk = 16 m, region ≈ 500 m. Detailed interior play, very many chunks for overworld.
- [x] **B) 2 m per tile** — chunk = 32 m, region ≈ 1 km. Good compromise — enough detail for combat, fewer chunks.
- [ ] **C) 0.5 m per tile** — chunk = 8 m, region ≈ 250 m. Ultra-detailed, but 4× more chunks and tiles.

**Decided:** Option B (2 m). Chunk = 32 m, region ≈ 1 km.

**Status:** `[x] Decided`

---

### D2. Chunk Size — Is 16×16 the Right Granularity?

Current `ChunkData` is 16×16. The heightmap design adds a 17×17 vertex grid. Is this the right size?

- [x] **A) 16×16** — 17×17 vertices (289), 256 tiles. Current — simple, small, fast to generate.
- [ ] **B) 32×32** — 33×33 vertices (1 089), 1 024 tiles. Fewer chunks loaded, but larger per-chunk cost.
- [ ] **C) 8×8** — 9×9 vertices (81), 64 tiles. Super granular streaming, more draw calls.

**Decided:** Option A (16×16). With 2 m tiles, a chunk is 32×32 m. A 5×5 active grid = 160×160 m.

**Status:** `[x] Decided`

---

### D3. TileData Struct — When to Expand?

The current `TileData` is 8 fields, ~12 bytes. The designs propose adding `LiquidType`, `HalfCover`, `FullCover`, and expanding `TileFlags` from `byte` to `ushort`. This changes the struct layout and serialisation.

- [x] **A) Expand now with all planned fields** — larger struct (maybe 16–20 bytes), but no future migration pain.
- [ ] **B) Expand incrementally as features are built** — smaller initial struct, but each expansion requires migration.
- [ ] **C) Sidecar dictionary for optional data, keep TileData minimal** — no struct change, but memory/lookup overhead.

**Decided:** Option A. Expand TileData to full layout upfront. No backward compatibility with old struct layout — old serialisation code will be removed.

**Status:** `[x] Decided`

---

### D4. Heightmap Mesh — Build on CPU or GPU?

The `ChunkTerrainBuilder` pipeline can run on CPU (Stride `Mesh` via vertex buffers) or GPU (compute shader generating vertices).

- [x] **A) CPU** — simple, debuggable, no compute shader complexity. Slower for High quality (4,225 vertices).
- [ ] **B) GPU compute** — fast, parallel. More complex, harder to debug, Stride compute API less mature.
- [ ] **C) CPU for Low/Med, GPU for High** — best of both, but two code paths to maintain.

**Decided:** Option A (CPU).

**Status:** `[x] Decided`

---

### D5. Splat Map Resolution — Per-Tile or Higher?

The design says 16×16 texels per chunk (1 texel per tile). This is very low — bilinear filtering will blur heavily.

- [ ] **A) 16×16** — 2 × 1 KB. Blurry blends, soft transitions.
- [x] **B) 32×32** — 2 × 4 KB. Smoother, still small.
- [ ] **C) 64×64** — 2 × 16 KB. Sharp, per-quarter-tile control.

**Decided:** Option B (32×32).

**Status:** `[x] Decided`

---

### D6. Linear Features — Region-Level or Chunk-Level Storage?

The storage schema puts `linear_feature` on `region`. But rendering and terrain modification need per-chunk access.

- [x] **A) Region-level, clip to chunk at load time** — fewer rows, but clipping on every chunk load.
- [ ] **B) Chunk-level (features duplicated/split at chunk boundaries)** — fast load, but feature continuity harder to manage.
- [ ] **C) Region-level with a chunk-to-feature index table** — best of both, but more schema complexity.

**Decided:** Option A. Store at region level, clip to chunk at load time.

**Status:** `[x] Decided`

---

### D7. SQLite vs JSON — Chunk Storage Format for Development

The storage design specifies SQLite. But the current codebase uses JSON files (`world.json`, `chunks/` directory). During development, which format should be the primary path?

- [x] **A) SQLite from day 1** — matches final design, no migration. Harder to hand-edit and diff during dev.
- [ ] **B) JSON during dev, migrate to SQLite later** — human-readable, git-friendly. Migration cost, two serialisation paths.
- [ ] ~~**C) Both (JSON → SQLite loader)**~~ — no longer needed; old JSON/Portland map code will be removed.

**Decided:** Option A. SQLite from day 1. The old JSON map format and Portland test data are removed — no backward compatibility needed.

**Status:** `[x] Decided`

---

### D8. WorldTemplate — Ship as Embedded Resource or Download?

Real-world data (OSM + SRTM) becomes a `WorldTemplate`. It could be large.

- [ ] **A) Embedded in game binary** — ~200–500 MB (full planet). Offline-first, no download. Bloated installer.
- [ ] **B) Download on first run** — same size, deferred. Small installer. Needs internet, progress UX.
- [ ] **C) Ship a small starter region, download rest on demand** — ~10 MB starter, rest streamed. Fast start. Complex delivery, offline issues.
- [x] **D) Region packs built from OSM at compile time, shipped per update** — ~10–50 MB per region. Manageable size, offline-friendly. Build pipeline needed.

**Decided:** Option D. Region packs built at compile time.

**Status:** `[x] Decided`

---

### D9. LLM Dependency — What Is Required vs Optional?

Multiple designs use LLM for town curation, descriptions, and naming. What happens if the LLM is unavailable?

- [ ] **Town selection/curation** — required at generation. Fallback: pre-curated starter plan shipped with game.
- [ ] **Town descriptions (Tier 1 tagline)** — required for towns. Fallback: templates for generic POIs.
- [ ] **Town descriptions (Tier 2/3)** — not required, on-demand. Fallback: template expansion (Tier 2) / unavailable (Tier 3).
- [ ] **Building names** — not required. Fallback: template-based names.
- [ ] **Terrain generation** — not required, fully deterministic/procedural. No fallback needed.

**Decision needed:** Should the starter region ship with a **pre-curated plan** so the game works 100% offline for the first N hours?

**Decided:** No pre-curated fallback. If the LLM isn't available, the game waits for connectivity. LLM is always required for town curation and descriptions. Terrain generation remains purely procedural.

**Status:** `[x] Decided`

---

### D10. Rendering Backend — Current Per-Tile vs New Heightmap

The current renderer places per-tile quads. The design replaces this with heightmap meshes. This is a large change to `TileMapRendererScript` and `ChunkMeshBatcher`.

- [x] **A) Replace the current renderer entirely** — clean, fresh start. Old per-tile renderer code removed.
- [ ] **B) Build the new renderer alongside, feature-flag switch** — safe, old renderer still works during development.
- [ ] **C) Build the new renderer in a branch, merge when stable** — git-based isolation, but branch divergence risk.

**Decided:** Option A. Replace entirely. The old per-tile quad renderer (`TileMapRendererScript`, `ChunkMeshBatcher`, `NeighborAnalyzer`) will be deleted. No feature flag needed.

**Status:** `[x] Decided`

---

### D11. Destructible Terrain — Option A or B?

From `map-terrain-completeness.md §12`. Option A (limited scripted destruction) vs Option B (freeform destruction).

- [x] **A) Limited scripted destruction** — flagged objects swap to "destroyed" mesh variant. Simple, predictable, low save cost. Less emergent gameplay.
- [ ] **B) Freeform destruction** — any wall/structure can be destroyed. Highly emergent, satisfying tactical play. More complex, larger save deltas, harder to balance.

**Decided:** Option A (scripted). Data model supports Option B upgrade later. This is a combat-phase concern but the data fields (`Destructible` flag, `CoverEdges`) are in the struct from the start.

**Status:** `[x] Decided (deferred to combat phase)`

---

### D12. Underground Layers — Separate ChunkData or Same Chunk?

From `map-terrain-completeness.md §1`. Underground content needs its own tile grid.

- [x] **A) `chunk_layer` table — separate tile_data BLOB per layer per chunk** — clean separation, only loaded when player is in that layer.
- [ ] **B) Single chunk with stacked tile arrays** — complex struct, always loaded even when underground is irrelevant.

**Decided:** Option A. Separate `chunk_layer` table.

**Status:** `[x] Decided`

---

### D13. Building Interiors — Inline or Separate Scenes?

From `map-terrain-completeness.md §3`.

- [x] **A) Interior is a separate tilemap loaded via transition** — clean separation, independent generation. Loading pause, two "worlds".
- [ ] **B) Interior is an underground layer in the same chunk** — seamless, same streaming system. Complex, ceiling rendering, always loaded.

**Decided:** Option A. Separate scene with transition.

**Status:** `[x] Decided`

---

### D14. Map Generation Pipeline — Keep LLM Blueprint or Switch to Procedural + LLM Hybrid?

The current `MapGeneratorService` uses an LLM to produce full `MapBlueprint` JSON via tools. The new design proposes deterministic procedural generation seeded from `WorldTemplate`, with LLM used only for curation and flavour.

- [ ] ~~**A) Keep current LLM-generates-everything approach for towns**~~ — removed; old `MapGeneratorService` LLM blueprint pipeline will be deleted.
- [x] **B) Switch to procedural generation with LLM for metadata only** — fast, deterministic, repeatable. LLM adds names/descriptions.
- [ ] **C) Hybrid — LLM generates town layout plan, procedural fills detail** — mid-ground. LLM decides building zones, procedural places buildings.

**Decided:** Option B. Procedural generation with LLM for curation/flavour only. Old LLM-blueprint generation code (PromptBuilder, BlueprintCollector, MapBlueprint tools) will be removed.

**Status:** `[x] Decided`

---

### D15. Starting Region — Which Area?

The designs say Purmerend, Netherlands. Confirm or change.

- [x] **A) Purmerend, Noord-Holland, Netherlands** — flat polder land (simplest terrain to debug), excellent OSM coverage, ~100×60 km region, starting safe haven narrative.
- [ ] **B) Other location** — specify alternative.

**Decided:** Option A. Purmerend, Noord-Holland.

**Status:** `[x] Decided`

---

### D16. Active Chunk Grid — 3×3 or Larger?

`ChunkStreamingProcessor` currently uses a 3×3 grid (9 chunks). With 16×16 tiles at 2 m, that's a 96×96 m visible area. For a top-down camera at 10–30 m altitude, is this enough?

- [ ] **A) 3×3** — 96×96 m visible, 9 chunks in memory. Current implementation.
- [x] **B) 5×5** — 160×160 m visible, 25 chunks in memory. 1-chunk buffer all around.
- [ ] **C) 7×7** — 224×224 m visible, 49 chunks in memory. Large buffer, higher memory.

**Decided:** Option B (5×5). With 2 m tiles, 5×5 = 160×160 m — comfortable for a top-down camera at 20–30 m altitude.

**Status:** `[x] Decided`

---

---

## Part 2: Task Lists

Tasks are grouped into **work streams** that can be developed somewhat independently. Within each stream, tasks are ordered by dependency.

### Camera Perspective

The game uses a **top-down perspective camera** (not isometric). This affects field-of-view calculations, visible area estimates, and UI layout. All design docs that reference "isometric" should be read as "top-down".

---

### WS1: Data Model Foundation

*Expands TileData, adds new enums and records. Must be done first — everything else depends on it.*

- [ ] **WS1.1** Expand `TileFlags` from `byte` to `ushort`, add `Forested`, `Interior`, `FastTravel`, `Searchable` flags
- [ ] **WS1.2** Create `LiquidType` enum (`None`, `Water`, `Toxic`, `Acid`, `Sewage`, `Lava`, `Oil`, `Frozen`, `Anomaly`)
- [ ] **WS1.3** Add `LiquidType Liquid` field to `TileData`
- [ ] **WS1.4** Add `CoverEdges HalfCover` and `CoverEdges FullCover` fields to `TileData`
- [ ] **WS1.5** Create `CoverEdges` flags enum (`None`, `North`, `East`, `South`, `West`)
- [ ] **WS1.6** Create `CoverLevel` enum (`None`, `Half`, `Full`)
- [ ] **WS1.7** Create `ChunkMode` enum (`Heightmap`, `Hybrid`)
- [ ] **WS1.8** Create `MapLayer` enum (`DeepUnderground`, `Underground`, `Surface`, `Elevated`)
- [ ] **WS1.9** Create `LinearFeatureType` enum (`Path`, `DirtRoad`, `Road`, `Highway`, `Rail`, `River`, `Stream`, `Canal`, `Pipeline`)
- [ ] **WS1.10** Create `LinearFeature` record (type, style, width, nodes list)
- [ ] **WS1.11** Create `LinearFeatureNode` record (position, optional `OverrideHeight`)
- [ ] **WS1.12** Create `TerrainModifier` abstract record + `FlattenStrip`, `ChannelCut`, `LevelRect`, `Crater` subtypes
- [ ] **WS1.13** Add `ChunkMode`, `MapLayer`, terrain modifiers list, and linear features reference to `ChunkData`
- [ ] **WS1.14** Update `TileDataFactory` to handle new fields
- [ ] **WS1.15** Update all existing `TileData` construction sites to supply new default values
- [ ] **WS1.16** Remove old serialisation code (`MapBlueprint` JSON, chunk files) — replaced by SQLite path
- [ ] **WS1.17** Write unit tests for new TileData layout (size, defaults, flag combinations)

---

### WS2: SQLite Storage Layer

*Implements world.db and save_XX.db. Depends on WS1 for data types.*

- [ ] **WS2.1** Add `Microsoft.Data.Sqlite` NuGet package to `Oravey2.Core`
- [ ] **WS2.2** Create `Data/WorldDbSchema.sql` — all world tables from the storage design
- [ ] **WS2.3** Create `Data/SaveDbSchema.sql` — all save tables from the storage design
- [ ] **WS2.4** Create `MapCompression` static class — Brotli compress/decompress
- [ ] **WS2.5** Create `TileDataSerializer` — `TileData[,]` ↔ flat byte array via `MemoryMarshal`
- [ ] **WS2.6** Create `WorldMapStore` — open `world.db`, CRUD for continents, regions, chunks, POIs, linear features, entity spawns, terrain modifiers
- [ ] **WS2.7** Create `SaveStateStore` — open `save_XX.db`, CRUD for party, chunk_state, fog_of_war, discovered_poi, fast_travel_unlock, map_marker
- [ ] **WS2.8** Create `MapDataProvider` — combined read from `WorldMapStore` + `SaveStateStore` with delta merge
- [ ] **WS2.9** Remove old Portland JSON map loader and map data files
- [ ] **WS2.10** Write unit tests for round-trip: TileData → serialize → compress → decompress → deserialize → compare
- [ ] **WS2.11** Write unit tests for WorldMapStore CRUD operations
- [ ] **WS2.12** Write unit tests for MapDataProvider delta-merge logic

---

### WS3: Heightmap Terrain Renderer

*Replaces per-tile quads with heightmap mesh. Depends on WS1.*

- [ ] **WS3.1** Create `HeightmapMeshGenerator` — 17×17 vertex grid from `TileData` heights, configurable subdivision
- [ ] **WS3.2** Implement chunk-edge vertex stitching (sample neighbour chunk border tiles)
- [ ] **WS3.3** Implement midpoint subdivision for Medium/High quality
- [ ] **WS3.4** Implement per-vertex normal calculation from adjacent edge cross products
- [ ] **WS3.5** Create `TerrainSplatBuilder` — generate splat map textures from `TileData.Surface`
- [ ] **WS3.6** Create `TerrainSplatEffect.sdsl` — Stride shader consuming 2 splat maps + 8 terrain albedo textures
- [ ] **WS3.7** Create `ChunkTerrainBuilder` — orchestrates mesh gen + splat + modifiers + features → `ChunkTerrainMesh`
- [ ] **WS3.8** Apply `TerrainModifier` pipeline (flatten, cut, level, crater) between height sampling and normal calc
- [ ] **WS3.9** Create `ChunkTerrainMesh` output class (mesh, splat textures, overlay data, feature meshes)
- [ ] **WS3.10** Remove old per-tile renderer (`TileMapRendererScript`, `ChunkMeshBatcher`, `NeighborAnalyzer`)
- [ ] **WS3.11** Integrate `ChunkTerrainBuilder` into `ChunkStreamingProcessor` — call `Build()` on chunk enter, dispose on chunk exit
- [ ] **WS3.12** Implement triplanar mapping mode in terrain shader (Medium/High)
- [ ] **WS3.13** Write unit test: 3×3 flat chunks → no seam vertices have mismatched normals
- [ ] **WS3.14** Write unit test: single chunk → vertex count matches quality preset expectation
- [ ] **WS3.15** Write unit test: subdivision produces correct vertex count
- [ ] **WS3.16** Write UI test: wilderness chunk renders without visual artefacts at all 3 quality levels

---

### WS4: Hybrid Mode (Tile Overlay)

*Town/dungeon rendering on top of heightmap. Depends on WS3.*

- [ ] **WS4.1** Create `TileOverlayBuilder` — generates floor decal quads projected onto heightmap surface
- [ ] **WS4.2** Implement `GetSurfaceHeight(Vector2 worldXZ)` — barycentric interpolation on heightmap triangle
- [ ] **WS4.3** Structure mesh placement: read `StructureId`, instance wall/door meshes snapped to heightmap Y
- [ ] **WS4.4** Implement zone transition blending: 2-tile fade at Heightmap↔Hybrid chunk boundaries
- [ ] **WS4.5** Update `ChunkTerrainBuilder.Build()` to branch on `ChunkMode` — include overlay for Hybrid
- [ ] **WS4.6** Write unit test: Hybrid chunk produces non-null `TileOverlayData`; Heightmap chunk produces null
- [ ] **WS4.7** Write UI test: town chunk renders floor tiles + wall structures on heightmap base

---

### WS5: Linear Feature Renderer

*Roads, rails, rivers as spline-projected geometry. Depends on WS1 + WS3.*

- [ ] **WS5.1** Implement Catmull-Rom spline evaluation in `SplineMath` (evaluate point, tangent, arc-length parameterisation)
- [ ] **WS5.2** Implement spline-to-ribbon mesh: sample spline → project to heightmap → extrude width → vertex buffer
- [ ] **WS5.3** Road rendering: surface texture + shoulder decals, UV tiled along spline length
- [ ] **WS5.4** Rail rendering: ballast ribbon + sleeper instances + rail strips (quality-dependent detail)
- [ ] **WS5.5** River rendering: channel cut into heightmap vertices + water plane at original height + animated UV flow
- [ ] **WS5.6** Bridge rendering: detect `OverrideHeight` on nodes → render deck at fixed Y + support pillars down to terrain
- [ ] **WS5.7** Implement splat-map override under roads (set surface to Asphalt/Concrete for consistency at distance)
- [ ] **WS5.8** Clip region-level features to chunk bounds at load time
- [ ] **WS5.9** Write unit test: Catmull-Rom spline through 4 points → sampled points lie on curve
- [ ] **WS5.10** Write unit test: ribbon mesh has correct vertex count and UV layout
- [ ] **WS5.11** Write UI test: road feature crossing 2 chunks renders as continuous ribbon

---

### WS6: Liquid Rendering

*Water, lava, toxic, oil, etc. Depends on WS1 + WS3.*

- [ ] **WS6.1** Create `LiquidRenderer` — group contiguous liquid tiles per `LiquidType`, build flat mesh at `WaterLevel` height
- [ ] **WS6.2** Create `WaterShader.sdsl` — Low/Medium/High variants (flat tinted → normal-mapped → reflection)
- [ ] **WS6.3** Create `LavaShader.sdsl` — crust pattern + emissive cracks + dynamic point light
- [ ] **WS6.4** Create `ToxicShader.sdsl` — bubble noise + green emissive pulse
- [ ] **WS6.5** Create `OilShader.sdsl` — thin-film interference + dark base
- [ ] **WS6.6** Create `FrozenShader.sdsl` — ice texture + crack normal map
- [ ] **WS6.7** Create `AnomalyShader.sdsl` — swirl UV distortion + purple emissive
- [ ] **WS6.8** Implement waterfall detection (cliff edge between two liquid tiles at different heights)
- [ ] **WS6.9** Waterfall cascade mesh: vertical ribbon from upper to lower water surface
- [ ] **WS6.10** Shore edge effects per liquid type (foam, crust, scum decals) using `WaterHelper.IsShore()`
- [ ] **WS6.11** Write unit test: connected liquid region detection groups correct tiles
- [ ] **WS6.12** Write UI test: lava pool renders with emissive glow at Medium quality

---

### WS7: Multi-Scale Zoom (Level 1–3)

*Camera-driven LOD transitions. Depends on WS3 + WS5. Level 4 globe is separate (WS7b).*

- [ ] **WS7.1** Create `ZoomLevel` enum and `ZoomLevelController` — maps camera altitude to active zoom level
- [ ] **WS7.2** Create `WorldLodCache` — derives Level 2/3 height data + biome classification from Level 1 data
- [ ] **WS7.3** Implement Level 2 heightmap mesh (coarser vertex grid, biome splat map replacing surface splat)
- [ ] **WS7.4** Implement Level 3 heightmap mesh (very coarse, continent-scale biome tinting)
- [ ] **WS7.5** Implement L1↔L2 crossfade (30–50m altitude): entity fade, overlay fade, splat swap, tree → canopy cluster LOD
- [ ] **WS7.6** Implement L2↔L3 crossfade (400–600m altitude): silhouette fade, route lines, territory overlays
- [ ] **WS7.7** Implement time scaling per zoom level (1×, 60×, 1440×) — `gameTime × timeScale`
- [ ] **WS7.8** Implement day/night cycle speed adjustment per zoom level
- [ ] **WS7.9** Level 2 entity rendering: party icon (vehicle or walking group), POI markers, road labels
- [ ] **WS7.10** Level 3 entity rendering: party trail icon, city dots, faction territory tints, event markers
- [ ] **WS7.11** Fog of war: L1 line-of-sight, L2 radius reveal, L3 region reveal
- [ ] **WS7.12** Write unit test: `WorldLodCache` biome derivation from dominant surface type
- [ ] **WS7.13** Write UI test: smooth zoom from L1 to L3 without visible pops or seams

---

### WS7b: Globe View (Level 4)

*Separate navigation UI. Can be built independently once WS7 exists.*

- [ ] **WS7b.1** Create UV sphere mesh for planet (64 segments)
- [ ] **WS7b.2** Project continent outlines from Level 3 data onto sphere texture
- [ ] **WS7b.3** Implement biome tinting on continent areas
- [ ] **WS7b.4** Implement ocean surface (base colour + animated normal map)
- [ ] **WS7b.5** Implement atmosphere rim-glow shader (fresnel)
- [ ] **WS7b.6** Implement cloud layer (semi-transparent sphere, slow rotation)
- [ ] **WS7b.7** Orbital camera: drag to rotate, scroll to zoom, auto-rotate on idle
- [ ] **WS7b.8** Discovered/undiscovered continent states (lit vs dark silhouette)
- [ ] **WS7b.9** Continent click → travel dialog (distance, time, cost, confirmation)
- [ ] **WS7b.10** Travel cutscene transition from globe back to Level 3 at destination

---

### WS8: Real-World Data Pipeline (WorldTemplate)

*Build-time processing of OSM + SRTM data. Depends on WS2.*

- [ ] **WS8.1** Create `WorldTemplate` data model (continent outlines, elevation grid, town entries, road segments, land use zones)
- [ ] **WS8.2** Create build-time tool: parse SRTM/ASTER GeoTIFF → compact elevation grid
- [ ] **WS8.3** Create build-time tool: parse OSM PBF → extract coastlines, boundaries, towns, roads, water, railways, land use
- [ ] **WS8.4** Create `GeoMapper` — lat/lon ↔ game XZ coordinate conversion with configurable origin
- [ ] **WS8.5** Create `RegionTemplate` — per-region elevation + roads + town catalog + land use zones
- [ ] **WS8.6** Package starter region (Noord-Holland) as binary `WorldTemplate` file
- [ ] **WS8.7** Create region-pack format for additional regions (server-downloadable)
- [ ] **WS8.8** Write unit test: GeoMapper round-trip (lat/lon → XZ → lat/lon, error < 1m at regional scale)
- [ ] **WS8.9** Write unit test: OSM parser extracts expected count of towns, roads for test PBF extract

---

### WS9: Procedural Map Generation (New Pipeline)

*Replaces LLM-generates-everything with seed-based procedural generation. Depends on WS1, WS2, WS8.*

- [ ] **WS9.1** Define the generation pipeline stages: WorldTemplate → CuratedPlan → Region skeleton → Chunk detail
- [ ] **WS9.2** LLM curation pass: send town catalog, receive curated subset with roles/factions/threat (LLM required — game waits if unavailable)
- [ ] **WS9.2b** Remove old LLM-blueprint generation code (`PromptBuilder`, `BlueprintCollector`, `MapBlueprint` tools, `ValidateBlueprintTool`, `WriteBlueprintTool`, `CheckOverlapTool`, `CheckWalkabilityTool`)
- [ ] **WS9.3** Create `CuratedWorldPlan` data model and storage in `world.db`
- [ ] **WS9.4** Create Level 3 continent generator: sample elevation at ~1km resolution, classify biome zones, store in `continent` table
- [ ] **WS9.5** Create Level 2 region generator: regional heightmap from L3 + detail noise, biome grid, POI markers from curated plan
- [ ] **WS9.6** Create Level 1 chunk generator (wilderness): real elevation → `HeightLevel`, land use → `SurfaceType`, seed-based detail
- [ ] **WS9.7** Create Level 1 chunk generator (town): boundary polygon → chunk grid, road skeleton, building zones, density-budget placement
- [ ] **WS9.8** Building selection algorithm: prioritise intersections + major roads, preserve landmarks (churches, stations, town halls), density budget per chunk
- [ ] **WS9.9** Road selection: filter to curated-town connections + motorways, Catmull-Rom smoothing with real elevation
- [ ] **WS9.10** River/canal generation from OSM water features → `LinearFeature` records
- [ ] **WS9.11** Sparse region generation: elevation + biome terrain, major roads, 1–3 outposts per region
- [ ] **WS9.12** On-demand generation integration with `ChunkStreamingProcessor`: detect missing chunk → generate → insert → stream in
- [ ] **WS9.13** Write unit test: chunk generation from same seed + coordinates produces identical TileData
- [ ] **WS9.14** Write unit test: town generator respects building density budget
- [ ] **WS9.15** Write unit test: road selection includes all curated-town connections

---

### WS10: Vegetation (Trees)

*Trees at Level 1 + canopy clusters at Level 2. Depends on WS1 + WS3.*

- [ ] **WS10.1** Create `TreeSpecies` enum and `TreeSpawn` record
- [ ] **WS10.2** Extend `TileFlags` with `Forested` flag (already in WS1.1)
- [ ] **WS10.3** Tree mesh rendering at Level 1: trunk + canopy model per species
- [ ] **WS10.4** Billboard LOD: trees beyond ~50m render as camera-facing quads
- [ ] **WS10.5** Canopy cluster LOD for Level 2: merge ~20 trees in an area into one cluster mesh
- [ ] **WS10.6** L1↔L2 crossfade: billboard trees fade out, canopy clusters fade in
- [ ] **WS10.7** Tree placement in generator (WS9): density from land use (forest/park/overgrown), spawn as entity_spawn rows
- [ ] **WS10.8** Write unit test: forest density values produce expected trees-per-chunk count
- [ ] **WS10.9** Write UI test: tree renders at L1, billboard at distance, cluster at L2

---

### WS11: Chunk Streaming Upgrades

*Expand the active grid and integrate new terrain builder. Depends on WS3, WS9.*

- [ ] **WS11.1** Make active grid size configurable in `ChunkStreamingProcessor` (currently hardcoded 3×3)
- [ ] **WS11.2** Default to 5×5 active grid
- [ ] **WS11.3** Implement LRU cache (64 chunks) — chunks leaving active grid stay in memory, evicted when cache full
- [ ] **WS11.4** Integrate on-demand generation: if chunk not in `world.db`, generate → insert → load
- [ ] **WS11.5** Integrate `ChunkTerrainBuilder` calls (WS3) as the rendering path in `ChunkStreamingProcessor`
- [ ] **WS11.6** Integrate `MapDataProvider` (WS2) as the data source — SQLite is the only storage path
- [ ] **WS11.7** Write unit test: LRU eviction order is correct
- [ ] **WS11.8** Write UI test: walking across chunk boundaries → no gaps, no pop-in

---

### WS12: Weather (Visual Only)

*Shader overlays on terrain. Depends on WS3. No data model changes.*

- [ ] **WS12.1** Create `WeatherState` record (`WeatherType`, `Intensity`, `WindDirection`, `Temperature`)
- [ ] **WS12.2** Create `WeatherOverlay` shader pass: rain wetness (darken + specular), snow (white on upward normals), dust tint
- [ ] **WS12.3** Fog distance adjustment based on weather type
- [ ] **WS12.4** Integrate weather state into terrain shader material parameters
- [ ] **WS12.5** Skip surface effects on Low quality (fog only)
- [ ] **WS12.6** Write UI test: rain weather state produces visible surface darkening

---

### WS13: Minimap

*HUD corner map. Depends on WS7 (WorldLodCache).*

- [ ] **WS13.1** Create minimap render target (circular masked texture in bottom-right HUD)
- [ ] **WS13.2** Render terrain from `WorldLodCache` splat colours (simplified top-down)
- [ ] **WS13.3** Overlay icons: party, POIs, markers
- [ ] **WS13.4** Fog of war overlay (dark for unexplored)
- [ ] **WS13.5** Scale minimap zoom to match current game zoom level
- [ ] **WS13.6** Player-placed map markers (CRUD, persist in save via `SaveStateStore`)

---

### WS14: Location Descriptions

*Info panel + tiered LLM descriptions. Can be done independently once WS2 + WS9 exist.*

- [ ] **WS14.1** Create `LocationDescription` record and `LocationType` enum
- [ ] **WS14.2** Create `location_description` table in `world.db` schema
- [ ] **WS14.3** Create `DescriptionTemplates` — tagline + summary generators for generic POI types
- [ ] **WS14.4** Create `DescriptionService` — tier routing, LLM calls, caching to `world.db`
- [ ] **WS14.5** Create info panel UI: slide-in from right, header + body + stats + "Read more"
- [ ] **WS14.6** Integrate Tier 1 tagline display in POI tooltips/markers
- [ ] **WS14.7** Loading UX: show available tier + spinner while LLM generates next tier
- [ ] **WS14.8** Offline fallback: template Tier 2, "communications link required" for Tier 3

---

---

## Part 3: Dependency Graph

```
WS1 (Data Model) ──────────────┬──── WS2 (SQLite Storage) ──── WS9 (Generation Pipeline)
                                │                                        │
                                ├──── WS3 (Heightmap Renderer) ──┬── WS4 (Hybrid Overlay)
                                │         │                      │
                                │         ├──── WS5 (Linear Features)
                                │         │
                                │         ├──── WS6 (Liquids)
                                │         │
                                │         ├──── WS7 (Zoom Levels) ──┬── WS7b (Globe)
                                │         │         │               │
                                │         │         ├── WS13 (Minimap)
                                │         │         │
                                │         │         └── WS14 (Descriptions)
                                │         │
                                │         ├──── WS10 (Trees)
                                │         │
                                │         └──── WS12 (Weather)
                                │
                                └──── WS11 (Chunk Streaming) ←── WS3 + WS9
                              
WS8 (WorldTemplate Pipeline) ──── WS9 (Generation Pipeline)
```

---

## Part 4: Recommended Implementation Order

Phases group work streams into parallel tracks. Each phase should result in a working, testable state.

### Phase A: Foundation (Do First)

| Stream        | What                 | Why First                        |
| ------------- | -------------------- | -------------------------------- |
| **WS1** | Data model expansion | Everything depends on this       |
| **WS2** | SQLite storage layer | Generation and streaming need it |

**Exit criteria:** Expanded `TileData` compiles, round-trip serialise/compress passes, `WorldMapStore` reads/writes a test database.

---

### Phase B: Terrain Rendering (Core Visual)

| Stream         | What                                   | Parallel?                          |
| -------------- | -------------------------------------- | ---------------------------------- |
| **WS3**  | Heightmap terrain renderer             | Start first                        |
| **WS5**  | Linear features (roads, rails, rivers) | Can start once WS3.1–3.4 are done |
| **WS4**  | Hybrid overlay (towns)                 | Can start once WS3.7 is done       |
| **WS6**  | Liquid rendering                       | Can start once WS3.1 is done       |
| **WS10** | Trees                                  | Can start once WS3.7 is done       |

**Exit criteria:** A test map renders as heightmap mesh with splat textures, roads drape correctly, town chunks show tile overlay, water/lava pools render, trees appear at Level 1.

---

### Phase C: World Generation

| Stream         | What                                | Parallel?                        |
| -------------- | ----------------------------------- | -------------------------------- |
| **WS8**  | WorldTemplate pipeline (build tool) | Independent — can start anytime |
| **WS9**  | Procedural generation pipeline      | Needs WS1, WS2, WS8              |
| **WS11** | Chunk streaming upgrades            | Needs WS3, WS9                   |

**Exit criteria:** A new game creates a world.db from WorldTemplate data. Walking around generates chunks on demand. The starter region (Purmerend area) is explorable with procedurally generated terrain, roads, and town layout.

---

### Phase D: Scale & Polish

| Stream         | What                      | Parallel?      |
| -------------- | ------------------------- | -------------- |
| **WS7**  | Multi-scale zoom (L1–L3) | Needs WS3, WS5 |
| **WS7b** | Globe (L4)                | Needs WS7      |
| **WS12** | Weather visuals           | Needs WS3      |
| **WS13** | Minimap                   | Needs WS7      |
| **WS14** | Location descriptions     | Needs WS2, WS9 |

**Exit criteria:** Smooth zoom from L1 to L3. Globe shows continents. Weather overlay on terrain. Minimap in HUD corner. Info panel shows tiered descriptions.

---

## Part 5: Out of Scope (Deferred)

These are explicitly **not** in this document's task lists. They come after map/generation is stable.

| System                                      | Reason for Deferral                                              |
| ------------------------------------------- | ---------------------------------------------------------------- |
| NPC spawning & AI                           | Depends on entity system, not map                                |
| Faction territories & diplomacy             | Gameplay layer on top of map                                     |
| Quest system                                | Content layer, not map infrastructure                            |
| Combat mechanics (turns, damage, abilities) | Separate system; only needs tile data for cover/height           |
| Inventory & equipment                       | No map dependency                                                |
| Dialogue system                             | No map dependency                                                |
| Save/load full game state                   | Needs all systems; map persistence (WS2) is the map part only    |
| Multiplayer / co-op                         | Architecture decision far in the future                          |
| Sound design                                | Can be layered on after visuals                                  |
| Building interiors (full)                   | Designed in completeness doc; implement after exterior map works |
| Underground layers (full)                   | Designed in completeness doc; implement after surface works      |
| Fast travel                                 | QoL; implement when world is large enough to need it             |
| Destructible terrain                        | Decision D11 deferred; implement with combat                     |
