# Step 11 — Multi-Scale Zoom (L1–L3)

**Work streams:** WS7 (Multi-Scale Zoom)
**Depends on:** Step 03 (heightmap renderer), Step 04 (linear features)
**User-testable result:** Scroll to zoom out → smooth transition from local terrain (L1) to regional map (L2) to continental strategic view (L3).

---

## Goals

1. Camera-altitude-driven LOD with crossfade transitions.
2. Level 2: coarse heightmap + biome splatting + town silhouettes + POI markers.
3. Level 3: very coarse heightmap + faction territory + city dots.
4. Time scaling per zoom level.

---

## Tasks

### 11.1 — ZoomLevelController

- [ ] Create `Camera/ZoomLevelController.cs`
- [ ] Maps camera altitude to `ZoomLevel` enum (L1/L2/L3)
- [ ] Transition zones: L1↔L2 at 30–50 m, L2↔L3 at 400–600 m
- [ ] Exposes `CurrentLevel`, `TransitionAlpha` (0–1 blend during crossfade)

### 11.2 — WorldLodCache

- [ ] Create `World/WorldLodCache.cs`
- [ ] Derives Level 2 height data: average Level 1 chunk heights over each L2 cell
- [ ] Derives Level 2 biome: dominant `SurfaceType` → biome classification
- [ ] Derives Level 3 data: average Level 2 regions
- [ ] Cached in memory; invalidated per-region when chunks change

### 11.3 — Level 2 Rendering

- [ ] Coarser heightmap mesh (~50–100 m per height sample)
- [ ] Biome splat map replacing surface splat (Grassland, Forest, Desert, Urban, Wasteland, Snow, Water, Mountain)
- [ ] Town silhouette meshes: low-poly building cluster blobs on the heightmap
- [ ] Road/rail/river linear features rendered as thicker lines
- [ ] POI markers with labels (town names, dungeon icons, landmarks)

### 11.4 — Level 3 Rendering

- [ ] Very coarse heightmap (~500 m–1 km per vertex)
- [ ] Continent-scale biome tinting (large colour regions)
- [ ] Major geographic features only (mountain ranges, coastlines, major rivers)
- [ ] City dots with labels (only large towns/cities visible)
- [ ] Faction territory colour overlays
- [ ] Major road network as thick lines

### 11.5 — L1↔L2 Crossfade

- [ ] At 30 m: individual entities start fading out
- [ ] At 35 m: tile overlay fades; splat transitions from surface to biome
- [ ] At 40 m: individual trees → canopy clusters (from Step 07)
- [ ] At 45 m: buildings → town silhouettes
- [ ] At 50 m: L2 fully active
- [ ] Both levels briefly render simultaneously, blended by alpha

### 11.6 — L2↔L3 Crossfade

- [ ] Silhouettes fade, replaced by city dots
- [ ] Road detail reduces to thick route lines
- [ ] Faction territory overlays fade in
- [ ] At 600 m: L3 fully active

### 11.7 — Time Scaling

- [ ] L1: 1× real-time
- [ ] L2: ~60× (1 min real ≈ 1 hr game)
- [ ] L3: ~1440× (1 min real ≈ 1 day game)
- [ ] `GameTimeService.Update()` multiplies delta by time scale based on current zoom level
- [ ] Day/night cycle speed adjusts accordingly

### 11.8 — L2 Entity Rendering

- [ ] Party: vehicle model (if driving) or walking-group sprite
- [ ] NPC convoys/traders on roads (placeholder icons)
- [ ] Major threat markers with radius circles

### 11.9 — L3 Entity Rendering

- [ ] Party: single icon with trail line
- [ ] Event markers (war fronts, disasters, quest markers)
- [ ] No individual entities visible

### 11.10 — Fog of War per Level

- [ ] L1: line-of-sight per character (existing `HeightHelper.HasLineOfSight`)
- [ ] L2: circular reveal around party (~5 km radius), unexplored areas greyed
- [ ] L3: regions revealed when any POI in them is discovered, unknown regions dark

### 11.11 — Unit Tests

File: `tests/Oravey2.Tests/Zoom/ZoomLevelControllerTests.cs`

- [ ] `Altitude10_IsLevel1` — 10 m altitude → ZoomLevel.Local
- [ ] `Altitude40_IsTransition_L1L2` — alpha between 0 and 1
- [ ] `Altitude100_IsLevel2` — fully L2
- [ ] `Altitude500_IsTransition_L2L3`
- [ ] `Altitude1000_IsLevel3`

File: `tests/Oravey2.Tests/Zoom/WorldLodCacheTests.cs`

- [ ] `BiomeDerivation_DominantGrass_IsGrassland` — 80% grass → Grassland
- [ ] `BiomeDerivation_DominantConcrete_IsUrban` — mostly concrete → Urban
- [ ] `BiomeDerivation_MixedForest_IsForest` — grass + forested flag → Forest
- [ ] `InvalidateRegion_RecalculatesAffectedCells`

File: `tests/Oravey2.Tests/Zoom/TimeScalingTests.cs`

- [ ] `Level1_TimeScale_Is1` — 1× multiplier
- [ ] `Level2_TimeScale_Is60` — 60× multiplier
- [ ] `Level3_TimeScale_Is1440` — 1440× multiplier

### 11.12 — UI Tests

File: `tests/Oravey2.UITests/Zoom/ZoomTransitionTests.cs`

- [ ] `ZoomOut_L1ToL2_SmoothTransition` — scroll zoom out, screenshot at L2 altitude shows biome colours + POI markers
- [ ] `ZoomOut_L2ToL3_ShowsContinentView` — continue zooming, screenshot shows city dots + territory colours
- [ ] `ZoomIn_L3ToL1_RestoresDetail` — zoom back in, screenshot shows local terrain detail

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Zoom."
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~ZoomTransition"
```

**User test:** In game, scroll the mouse wheel to zoom out. Watch the terrain smoothly transition from detailed local view to a regional map with biome colours and town names, then to a continental strategic view with city dots and faction territories. Zoom back in and detail returns.
