# Step 05 — Hybrid Mode (Towns)

**Work streams:** WS4 (Hybrid Mode / Tile Overlay)
**Depends on:** Step 03 (heightmap renderer)
**User-testable result:** Launch the game → town chunks show discrete floor tiles, walls, and structures on top of the heightmap surface.

---

## Goals

1. Overlay tile-resolution features onto the heightmap in `ChunkMode.Hybrid` chunks.
2. Snap structures to heightmap Y positions.
3. Blend transitions between Heightmap and Hybrid chunk zones.

---

## Tasks

### 5.1 — TileOverlayBuilder

- [ ] Create `World/Terrain/TileOverlayBuilder.cs`
- [ ] For each tile in a Hybrid chunk: generate a floor decal quad projected onto the heightmap surface
- [ ] Floor quads use `SurfaceType` + `VariantSeed` for texture selection
- [ ] Only generate overlays for tiles with structures or distinct floor types (skip pure natural terrain)

### 5.2 — GetSurfaceHeight

- [ ] Add `GetSurfaceHeight(Vector2 worldXZ)` to `HeightmapMeshGenerator` or a helper
- [ ] Find the triangle in the heightmap mesh containing the XZ point
- [ ] Barycentric interpolation to get exact Y at that point
- [ ] Used by overlay builder and structure placement

### 5.3 — Structure Placement

- [ ] Read `StructureId` per tile
- [ ] Instance wall/door/prop meshes snapped to heightmap Y via `GetSurfaceHeight`
- [ ] Walls placed at tile edges, doors in wall gaps, props at tile centres
- [ ] Use placeholder cube/box meshes for now (actual art comes later)

### 5.4 — Zone Transition Blending

- [ ] At Heightmap↔Hybrid chunk boundaries: 2-tile fade margin
- [ ] Overlay opacity reduces from 100% to 0% over the margin tiles
- [ ] Heightmap splat covers the transition gap

### 5.5 — ChunkTerrainBuilder Integration

- [ ] `Build()` branches on `ChunkMode`:
  - `Heightmap` → mesh + splat only (existing)
  - `Hybrid` → mesh + splat + tile overlay + structure placement
- [ ] Output `TileOverlayData` in `ChunkTerrainMesh.Overlay` (non-null for Hybrid)

### 5.6 — Test Scene Update

- [ ] Add a Hybrid chunk to the test scene with:
  - Paved floor tiles (concrete/asphalt)
  - Wall structures along tile edges
  - A door gap in one wall
  - Transition from Hybrid chunk to adjacent Heightmap chunk

### 5.7 — Unit Tests

File: `tests/Oravey2.Tests/Terrain/TileOverlayBuilderTests.cs`

- [ ] `HybridChunk_ProducesOverlay_NotNull` — Hybrid mode → overlay data returned
- [ ] `HeightmapChunk_ProducesOverlay_Null` — Heightmap mode → null overlay
- [ ] `OverlayQuads_SnapToHeightmapSurface` — floor quad Y matches `GetSurfaceHeight` for its tile centre
- [ ] `StructurePlacement_ReadsStructureId` — tile with `StructureId != 0` produces a structure entry

File: `tests/Oravey2.Tests/Terrain/GetSurfaceHeightTests.cs`

- [ ] `FlatTerrain_ReturnsConstantHeight` — all points on flat heightmap return same Y
- [ ] `SlopedTerrain_InterpolatesBetweenVertices` — point between two vertices returns intermediate Y
- [ ] `OutOfBounds_Clamps` — point outside chunk bounds returns edge height

### 5.8 — UI Tests

File: `tests/Oravey2.UITests/Terrain/HybridRenderingTests.cs`

- [ ] `HybridChunk_ShowsFloorTiles` — screenshot of Hybrid chunk shows distinct tile pattern (not just splat blend)
- [ ] `HybridToHeightmap_Transition_NoHardEdge` — screenshot at boundary shows smooth fade

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~TileOverlay|GetSurfaceHeight"
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~HybridRendering"
```

**User test:** Launch the game. One part of the map shows a "town" area with visible floor tiles and wall structures sitting on the terrain. Where the town meets wilderness, the overlay fades smoothly into natural terrain.
