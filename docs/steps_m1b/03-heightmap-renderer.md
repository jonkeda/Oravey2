# Step 03 — Heightmap Terrain Renderer

**Work streams:** WS3 (Heightmap Terrain Renderer)
**Depends on:** Step 01 (data model), Step 02 (storage)
**User-testable result:** Launch the game → see continuous heightmap terrain with texture-splatted surfaces. Old per-tile quads are gone.

---

## Goals

1. Build the heightmap mesh generator (CPU, 17×17 vertices per chunk).
2. Build the terrain splat shader (2 RGBA splat maps, 8 surface textures).
3. Wire it into the chunk streaming processor.
4. Remove the old per-tile renderer.

---

## Tasks

### 3.1 — HeightmapMeshGenerator

- [x] Create `World/Terrain/HeightmapMeshGenerator.cs`
- [x] Build 17×17 vertex grid from `TileData[16,16]` heights (vertex = average of adjacent tiles)
- [x] Support `IChunkNeighborProvider` interface for sampling border tiles from neighbour chunks
- [x] Implement edge stitching — shared vertices at chunk borders use averaged heights from both chunks
- [x] Implement midpoint subdivision for Medium quality (33×33) and High quality (65×65) with noise from `VariantSeed`
- [x] Implement per-vertex normal calculation (cross product of adjacent edges, averaged at shared vertices)
- [x] Return `VertexPositionNormalTexture[]` + index buffer

### 3.2 — TerrainSplatBuilder

- [x] Create `World/Terrain/TerrainSplatBuilder.cs`
- [x] Generate two 32×32 RGBA textures from `TileData.Surface` values
- [x] Splat0: R=Dirt, G=Asphalt, B=Concrete, A=Grass
- [x] Splat1: R=Sand, G=Mud, B=Rock, A=Metal
- [x] Each texel corresponds to a tile; sub-sample between tile centres using bilinear weights for the 2× oversampling
- [x] Return two `Texture` objects ready for the shader

### 3.3 — Terrain Shader

- [x] Create `Rendering/Shaders/TerrainSplatEffect.sdsl` (Stride SDSL shader)
- [x] Input: 2 splat maps + 8 terrain albedo textures + world UV scale
- [x] Sample both splat maps at fragment UV, weight-blend the 8 albedo lookups
- [ ] On Medium/High: add triplanar mapping for steep slopes
- [ ] On Low: flat per-tile surface type (skip bilinear splat)

### 3.4 — Terrain Modifier Pipeline

- [x] Create `World/Terrain/TerrainModifierApplicator.cs`
- [x] `FlattenStrip` — vertices within width of centre line set to `TargetHeight`
- [x] `ChannelCut` — vertices within width pushed down by `Depth`
- [x] `LevelRect` — vertices within rectangle set to `TargetHeight`
- [x] `Crater` — vertices within radius depressed by `Depth × (1 - dist/radius)²`
- [x] Applied after base height sampling, before subdivision and normal calculation

### 3.5 — ChunkTerrainBuilder

- [x] Create `World/Terrain/ChunkTerrainBuilder.cs`
- [x] Orchestrates: height sampling → modifier application → subdivision → normal calc → splat build
- [x] Input: `ChunkData`, `ChunkMode`, `QualityPreset`, neighbours
- [x] Output: `ChunkTerrainMesh` (heightmap mesh, splat textures, linear feature meshes, overlay data)

### 3.6 — ChunkTerrainMesh

- [x] Create `World/Terrain/ChunkTerrainMesh.cs`
- [x] Properties: `Mesh HeightmapMesh`, `Texture SplatMap0`, `Texture SplatMap1`, `IReadOnlyList<Mesh> LinearFeatureMeshes`, `TileOverlayData? Overlay`

### 3.7 — Remove Old Renderer

- [x] Delete `TileMapRendererScript.cs` (or the per-tile quad rendering code within it)
- [x] Delete `ChunkMeshBatcher.cs`
- [x] Delete `NeighborAnalyzer.cs`
- [x] Clean up any references in `ChunkStreamingProcessor`

### 3.8 — Integrate with ChunkStreamingProcessor

- [x] When a chunk enters the active grid, call `ChunkTerrainBuilder.Build()` → create Stride entities with the generated mesh + material
- [x] When a chunk exits, dispose the mesh + textures
- [x] Use a hardcoded test `ChunkData` set for now (a 3×3 grid of flat terrain with varied surfaces) until the procedural generator exists (Step 09)

### 3.9 — Test Scene Setup

- [x] Create a test scene that populates a 3×3 chunk grid (later 5×5) with hand-crafted `TileData` including:
  - Flat grass area
  - Height variation (hills)
  - Mixed surface types (road, dirt, concrete transitions)
  - A crater modifier
- [x] Wire the test scene as the default launch scene

### 3.10 — Unit Tests

File: `tests/Oravey2.Tests/Terrain/HeightmapMeshGeneratorTests.cs`

- [x] `FlatChunk_ProducesCorrectVertexCount_Low` — 17×17 = 289 vertices
- [x] `FlatChunk_ProducesCorrectVertexCount_Medium` — 33×33 = 1089 vertices
- [x] `FlatChunk_ProducesCorrectVertexCount_High` — 65×65 = 4225 vertices
- [x] `FlatChunk_AllNormalsPointUp` — all normals are (0, 1, 0) on flat terrain
- [x] `AdjacentFlatChunks_SeamVertices_HaveMatchingPositions` — shared edge vertices identical
- [x] `AdjacentFlatChunks_SeamVertices_HaveMatchingNormals` — shared edge normals identical
- [x] `SlopedChunk_Normals_AreNotAllUp` — heightmap with slope produces non-vertical normals
- [x] `CraterModifier_DepressesVertices_BelowOriginalHeight` — apply crater, check affected vertices are lower

File: `tests/Oravey2.Tests/Terrain/TerrainSplatBuilderTests.cs`

- [x] `UniformSurface_ProducesSingleChannelSplat` — all-grass chunk → splat0.A = 1.0 everywhere
- [x] `MixedSurface_ProducesBlendsAtBoundaries` — checker of Dirt/Asphalt → texels at boundaries have partial weights
- [x] `SplatTextureSize_Is32x32` — output texture dimensions

### 3.11 — UI Tests

File: `tests/Oravey2.UITests/Terrain/HeightmapRenderingTests.cs`

- [x] `TestScene_Launches_TerrainVisible` — game launches, take screenshot, verify non-black pixels in terrain area (basic smoke test)
- [x] `TestScene_MultipleChunks_NoGapsBetweenChunks` — visual check: screenshot at chunk boundary, verify continuous terrain (no black seam lines)

---

## Verify

```bash
dotnet build src/Oravey2.Core
dotnet build src/Oravey2.Windows
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Terrain."
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~HeightmapRendering"
```

**User test:** Launch the game. You see a continuous terrain surface with blended textures — grass, dirt, asphalt patches. Hills are visible. No grid lines, no individual tile quads. A crater depression is visible.
