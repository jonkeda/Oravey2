# Step 07 — Vegetation (Trees)

**Work streams:** WS10 (Vegetation)
**Depends on:** Step 03 (heightmap renderer)
**User-testable result:** Launch the game → trees render at Level 1, switch to billboards at distance.

---

## Goals

1. Tree data model and placement.
2. Full mesh rendering near camera, billboard LOD at distance.
3. Canopy clusters for Level 2 (used later in Step 11).

---

## Tasks

### 7.1 — Data Model

- [ ] Create `TreeSpecies` enum: `DeadOak`, `CharredPine`, `MutantWillow`, `RustedMetal`, `Scrub`, `Palm`, `Birch`
- [ ] Create `TreeSpawn` record: `Vector2 Position`, `TreeSpecies Species`, `byte GrowthStage`, `bool IsDead`
- [ ] Trees are stored as `entity_spawn` rows with `entity_type = 'tree'` and `data_json` containing `TreeSpawn`

### 7.2 — Tree Mesh Rendering (L1 Near)

- [ ] Create `World/Vegetation/TreeRenderer.cs`
- [ ] Load/create simple tree meshes per species (trunk cylinder + canopy sphere/cone — placeholder art)
- [ ] For each `TreeSpawn` in a chunk: instance the species mesh at position, snapped to heightmap Y via `GetSurfaceHeight`
- [ ] Scale by `GrowthStage` (0 = sapling → 255 = full-grown)
- [ ] Dead trees: trunk only (no canopy mesh)

### 7.3 — Billboard LOD (L1 Far)

- [ ] Trees beyond ~50 m from camera switch to billboard (camera-facing quad)
- [ ] Billboard texture: pre-rendered sprite per species (can generate at build time or use placeholder)
- [ ] Crossfade between mesh and billboard over a 5 m transition range
- [ ] Billboard batched into a single draw call per chunk

### 7.4 — Canopy Cluster LOD (for L2)

- [ ] Create `World/Vegetation/CanopyClusterBuilder.cs`
- [ ] Group ~20 trees in an area into one cluster mesh (merged canopy blob)
- [ ] Cluster stored per chunk, used in Step 11 when L2 rendering is implemented
- [ ] For now: just build the data; rendering comes with zoom levels

### 7.5 — Tree Placement in Test Scene

- [ ] Add `Forested` flag to some tiles in the test scene
- [ ] Place tree entity spawns in forested areas with varied species, growth stages
- [ ] Include some dead trees and some dense forest patches

### 7.6 — Integration with ChunkStreamingProcessor

- [ ] When chunk enters active grid: `TreeRenderer.SpawnTrees(chunk)` creates tree entities
- [ ] When chunk exits: dispose tree entities
- [ ] LOD switching handled per-frame based on camera distance

### 7.7 — Unit Tests

File: `tests/Oravey2.Tests/Vegetation/TreeSpawnTests.cs`

- [ ] `TreeSpawn_ConstructsWithAllFields` — all fields accessible
- [ ] `TreeSpawn_DeadTree_IsDead` — `IsDead = true` flag works

File: `tests/Oravey2.Tests/Vegetation/CanopyClusterBuilderTests.cs`

- [ ] `FewTrees_SingleCluster` — 5 trees → 1 cluster
- [ ] `ManySpreadTrees_MultipleCluster` — 40 trees spread wide → 2+ clusters
- [ ] `NoTrees_NoCluster` — empty input → empty output

### 7.8 — UI Tests

File: `tests/Oravey2.UITests/Terrain/TreeRenderingTests.cs`

- [ ] `ForestedArea_ShowsTrees` — screenshot of forested chunk has non-terrain geometry visible
- [ ] `DistantTrees_AreBillboards` — move camera far from trees, screenshot shows flat sprites instead of 3D meshes (lower vertex count in that area)

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Vegetation."
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~TreeRendering"
```

**User test:** Launch the game. Forested areas show trees — full 3D meshes up close, flat billboards at distance. Dead trees are bare trunks. Dense patches have many trees with overlapping canopies.
