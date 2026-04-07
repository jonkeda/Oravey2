# Step 04 — Roads, Rails, Rivers (Linear Features)

**Work streams:** WS5 (Linear Feature Renderer)
**Depends on:** Step 03 (heightmap renderer)
**User-testable result:** Launch the game → roads, rails, and rivers render as smooth continuous splines draped on terrain.

---

## Goals

1. Implement Catmull-Rom spline math.
2. Build the spline-to-ribbon mesh pipeline.
3. Render roads, rails, and rivers with distinct visual styles.
4. Bridge rendering for segments with `OverrideHeight`.

---

## Tasks

### 4.1 — Spline Math

- [ ] Create `World/LinearFeatures/SplineMath.cs`
- [ ] Catmull-Rom evaluate: given 4 control points + t ∈ [0,1], return interpolated position and tangent
- [ ] Arc-length parameterisation: uniform sampling along spline length
- [ ] Spline through N nodes: chain Catmull-Rom segments, duplicate endpoints for first/last

### 4.2 — Spline-to-Ribbon Mesh

- [ ] Create `World/LinearFeatures/RibbonMeshBuilder.cs`
- [ ] Sample spline at regular intervals (resolution based on quality preset)
- [ ] At each sample: project centre point onto heightmap to get Y (use `HeightmapMeshGenerator.GetSurfaceHeight`)
- [ ] Extrude left/right by half-width using the tangent perpendicular → vertex pairs
- [ ] Generate UV: U = 0 at left edge / 1 at right edge, V = cumulative arc length (tiled)
- [ ] Offset Y slightly above terrain (+0.01 m) to prevent z-fighting
- [ ] Build triangle strip index buffer

### 4.3 — Road Rendering

- [ ] Road surface material: asphalt/concrete texture, UV tiled along spline
- [ ] Shoulder decal strips at edges (blend road into terrain)
- [ ] On Medium/High: centre line markings as overlay texture
- [ ] Splat-map override: set surface type to Asphalt/Concrete for tiles under the road (via `TerrainSplatBuilder`)

### 4.4 — Rail Rendering

- [ ] Ballast ribbon: gravel texture, ~2.5 m wide
- [ ] Sleeper instances: placed at regular intervals along spline (skip on Low quality)
- [ ] Rail strips: two thin metal meshes offset from centre (skip on Low quality)

### 4.5 — River Rendering

- [ ] Channel cut: modify heightmap vertices within river width (push down by depth via `ChannelCut` terrain modifier)
- [ ] Riverbed uses mud/rock surface texture
- [ ] Water plane at original terrain height using existing water rendering
- [ ] Animated UV scrolling in flow direction (derived from spline direction)
- [ ] On Low: flat blue ribbon, no depth cut

### 4.6 — Bridge Rendering

- [ ] Detect segments where `LinearFeatureNode.OverrideHeight` is set
- [ ] Render deck ribbon at fixed Y height (not draped)
- [ ] Support pillar meshes: vertical from deck to heightmap surface
- [ ] Railing instances along deck edges
- [ ] Underside shadow quad

### 4.7 — Chunk Clipping

- [ ] Linear features are stored per-region (D6 decision)
- [ ] At chunk load time: clip each feature's spline to the chunk bounding rect
- [ ] Only build ribbon mesh for the clipped portion
- [ ] Ensure clipped ribbons align seamlessly at chunk boundaries

### 4.8 — Integration

- [ ] `ChunkTerrainBuilder` calls `RibbonMeshBuilder` for each linear feature in the chunk
- [ ] Output meshes added to `ChunkTerrainMesh.LinearFeatureMeshes`
- [ ] Test scene: add a road crossing 2+ chunks, a rail line, a river with channel cut

### 4.9 — Unit Tests

File: `tests/Oravey2.Tests/LinearFeatures/SplineMathTests.cs`

- [ ] `CatmullRom_Midpoint_IsAverage` — 4 collinear points, t=0.5 → midpoint of segment
- [ ] `CatmullRom_Endpoints_MatchControlPoints` — t=0 → P1, t=1 → P2
- [ ] `CatmullRom_Tangent_IsNonZero` — tangent at t=0.5 has magnitude > 0
- [ ] `ArcLengthSampling_UniformSpacing` — samples along a curved spline are approximately equidistant

File: `tests/Oravey2.Tests/LinearFeatures/RibbonMeshBuilderTests.cs`

- [ ] `StraightRoad_VertexCount_MatchesSamples` — N samples → 2N vertices
- [ ] `StraightRoad_UVs_TileCorrectly` — V coordinates increase monotonically
- [ ] `RibbonWidth_MatchesFeatureWidth` — left/right vertex pairs are separated by expected distance
- [ ] `BridgeSegment_VerticesAtOverrideHeight` — vertices on bridge portion at fixed Y, not terrain Y

### 4.10 — UI Tests

File: `tests/Oravey2.UITests/Terrain/LinearFeatureRenderingTests.cs`

- [ ] `Road_CrossingChunkBoundary_RendersContinuous` — screenshot at chunk boundary, road has no gap
- [ ] `River_HasWaterSurface` — screenshot shows blue/water colour along river path

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~LinearFeatures."
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~LinearFeatureRendering"
```

**User test:** Launch the game. A road crosses the terrain as a smooth ribbon. A river cuts into the terrain with water visible. A rail line with ballast is visible (sleepers and rails on Medium/High). A bridge segment floats above a river crossing with support pillars.
