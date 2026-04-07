# Heightmap–Tilemap Hybrid Terrain System

**Status:** Draft  
**Milestone:** M1b  
**Depends on:** TileData (Phase 1 ✓), Height system (Phase 3 ✓), Water system (Phase 4 ✓)

---

## Problem

The current tile-based renderer places discrete quads at integer grid positions, producing a visibly "grid-locked" landscape. Individual tile meshes are expensive to batch and hard to blend at boundaries. Heightmaps solve both problems — a single mesh per chunk with smooth vertex-interpolated terrain — but they lack the discrete semantic data (surface type, flags, structure references) that towns/dungeons need for gameplay logic.

We need **two rendering modes** that share the same underlying `TileData` model:

| Mode | Use Case | Visual | Data |
|------|----------|--------|------|
| **Heightmap-only** | Wilderness, wasteland, overworld travel | Continuous mesh, texture-splatted | Height + surface weights per vertex |
| **Hybrid (heightmap + tilemap overlay)** | Towns, dungeons, settlements, interiors | Continuous base mesh + discrete tile features on top | Full `TileData` fidelity: flags, structures, variant seeds |

---

## Goals

1. Replace per-tile quad rendering with a **heightmap mesh** for all terrain.
2. In town/dungeon zones, overlay **tile-resolution features** (walls, floors, props) onto the heightmap surface.
3. Render **roads, rails, and rivers** as continuous spline-projected decals/meshes that work in both modes.
4. Keep the existing `TileData` / `ChunkData` / `WorldMapData` model unchanged — rendering is a view concern.
5. Stay within the existing quality-preset budget (Low / Medium / High).

---

## Design Overview

```
┌─────────────────────────────────────────────────────────┐
│                    WorldMapData                          │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐              │
│  │ ChunkData│  │ ChunkData│  │ ChunkData│  ...          │
│  │ (16×16)  │  │ (16×16)  │  │ (16×16)  │              │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘              │
│       │              │              │                    │
│       ▼              ▼              ▼                    │
│  ┌─────────────────────────────────────────────┐        │
│  │           ChunkTerrainBuilder                │        │
│  │  Reads TileData[,] → builds mesh + textures │        │
│  └────┬──────────────────────────┬─────────────┘        │
│       │                          │                       │
│       ▼                          ▼                       │
│  Heightmap Mode            Hybrid Mode                   │
│  ┌──────────────┐     ┌──────────────────────┐          │
│  │ TerrainMesh  │     │ TerrainMesh (base)   │          │
│  │ + SplatMap   │     │ + SplatMap            │          │
│  │ + Decals     │     │ + TileOverlayPass    │          │
│  └──────────────┘     │ + StructurePlacement │          │
│                        │ + Decals             │          │
│                        └──────────────────────┘          │
│                                                          │
│  ┌─────────────────────────────────────────────┐        │
│  │        LinearFeatureRenderer                 │        │
│  │  Roads / Rails / Rivers (spline decals)      │        │
│  └─────────────────────────────────────────────┘        │
└─────────────────────────────────────────────────────────┘
```

---

## 1. Heightmap Mesh Generation

Each 16×16 chunk produces a **17×17 vertex grid** (one vertex per tile corner). Heights are sourced from `TileData.HeightLevel` and interpolated at vertices by averaging the surrounding tiles.

### Vertex Height Calculation

```
Vertex(x,y) height = average of up to 4 adjacent TileData.HeightLevel values

  Tile(x-1,y-1) ── Vertex(x,y) ── Tile(x,y-1)
        │                │
  Tile(x-1,y)   ──      ── Tile(x,y)
```

At chunk edges, sample the neighbour chunk's border tiles for seamless stitching.

### Mesh Detail

| Quality | Vertices per Chunk | Subdivision | Notes |
|---------|--------------------|-------------|-------|
| Low     | 17×17 (289)        | None        | Direct tile-corner vertices |
| Medium  | 33×33 (1 089)      | 1× midpoint | Smoother slopes |
| High    | 65×65 (4 225)      | 2× midpoint | Near-smooth terrain |

Subdivision uses **midpoint displacement** with a small noise offset seeded by `VariantSeed` to avoid mechanical regularity.

### Normals

Per-vertex normals computed from the cross product of adjacent edge vectors. Shared vertices at chunk borders use averaged normals from both chunks to prevent visible seams.

---

## 2. Texture Splatting

Instead of one texture per tile, the heightmap uses a **splat map** — an RGBA texture where each channel encodes the blend weight of a surface type at that point.

### Splat Map Encoding

A single chunk needs up to 8 surface types (`SurfaceType` enum). We use **two RGBA textures** (8 channels total):

| Texture | R | G | B | A |
|---------|---|---|---|---|
| Splat0  | Dirt | Asphalt | Concrete | Grass |
| Splat1  | Sand | Mud | Rock | Metal |

Each texel corresponds to a tile centre. The GPU shader bilinearly samples between texels, producing smooth blends at tile boundaries.

### Shader Pseudocode

```hlsl
float4 splat0 = SplatMap0.Sample(uv);
float4 splat1 = SplatMap1.Sample(uv);

float3 color =
    splat0.r * DirtAlbedo.Sample(worldUV) +
    splat0.g * AsphaltAlbedo.Sample(worldUV) +
    splat0.b * ConcreteAlbedo.Sample(worldUV) +
    splat0.a * GrassAlbedo.Sample(worldUV) +
    splat1.r * SandAlbedo.Sample(worldUV) +
    splat1.g * MudAlbedo.Sample(worldUV) +
    splat1.b * RockAlbedo.Sample(worldUV) +
    splat1.a * MetalAlbedo.Sample(worldUV);
```

On **Low** quality, skip bilinear splat sampling — use flat per-tile surface type with hard edges (cheaper, still better than individual quads).

### Triplanar Mapping

Steep slopes (cliff faces) stretch UV-mapped textures. Triplanar mapping projects textures from X/Y/Z axes and blends by normal direction. Already planned in `QualitySettings` — `TriplanarMode.Fast` for Medium, `TriplanarMode.Full` for High.

---

## 3. Hybrid Mode: Tile Overlay

In town/dungeon zones the chunk is tagged with `ChunkMode.Hybrid`. The heightmap mesh still forms the **ground surface**, but an additional pass renders discrete tile features on top.

### What the Overlay Adds

| Feature | Implementation | Source Data |
|---------|---------------|-------------|
| Floor tiles (paved, wood, tiled) | Decal quads projected onto heightmap surface | `SurfaceType` + `VariantSeed` |
| Walls / fences | Instanced meshes placed at tile edges | `StructureId` + `TileFlags` |
| Props (barrels, furniture) | Entity spawns positioned at tile Y-height | `ChunkData.EntitySpawns` |
| Grid-aligned features (doors, hatches) | Positioned at tile centre, snapped to heightmap Y | `StructureId` |

### Overlay Rendering Order

```
1. Heightmap mesh        (opaque, depth-write)
2. Floor decals          (alpha-blend, depth-test, no depth-write)
3. Structure meshes      (opaque, depth-write)
4. Props / entities      (opaque/alpha as needed)
5. Linear features       (decals — roads, rails)
6. Water pass            (translucent, animated)
```

### Heightmap-to-Tile Snap

Overlay elements sample the heightmap mesh to find their Y position:

```csharp
float GetSurfaceHeight(Vector2 worldXZ)
{
    // Find the triangle in the heightmap mesh containing worldXZ
    // Barycentric interpolation of the 3 vertex heights
    // Returns exact Y at that point on the mesh surface
}
```

This avoids floating or sunken tiles — every overlay element sits precisely on the terrain.

---

## 4. Zone Tagging

Each chunk carries a `ChunkMode` that controls which renderer path to use:

```csharp
public enum ChunkMode : byte
{
    Heightmap,   // Wilderness, wasteland — heightmap + splat only
    Hybrid       // Town, dungeon, interior — heightmap + tile overlay
}
```

Zone boundaries between Heightmap and Hybrid chunks blend over a 2-tile margin. The border tiles in a Hybrid chunk gradually reduce overlay opacity, and the heightmap splat handles the rest.

### Transition Rules

- **Heightmap → Hybrid:** Overlay fades in over 2 tiles from the chunk edge.
- **Hybrid → Heightmap:** Discrete tile features fade out; splat map covers the gap.
- **Hybrid → Hybrid (different tileset):** Surface type transitions handled by splat blending as normal.

---

## 5. Linear Features: Roads, Rails, Rivers

Roads, rails, and rivers are **not encoded per-tile**. They are represented as **spline paths** stored alongside chunk data, rendered as projected geometry that drapes over the heightmap.

### Data Model

```csharp
public record LinearFeature(
    LinearFeatureType Type,       // Road, Rail, River, Path, Wall
    IReadOnlyList<Vector2> Nodes, // Control points in world XZ
    float Width,                  // Metres
    LinearFeatureStyle Style);    // Visual variant (dirt road, highway, narrow gauge, etc.)

public enum LinearFeatureType : byte
{
    Path,       // Narrow dirt track
    Road,       // Paved road
    Highway,    // Wide multi-lane
    Rail,       // Railway track
    River,      // Water channel
    Stream,     // Narrow water
    Wall,       // Linear wall / barrier
    Pipeline    // Pipe / cable run
}
```

### Spline to Mesh

1. **Catmull-Rom spline** through the node list → smooth curve.
2. **Sample** the spline at regular intervals (resolution depends on quality).
3. At each sample, project the centre point onto the heightmap → get Y.
4. Extrude a **ribbon mesh** of the given width, vertices draped onto terrain.
5. Offset Y slightly above terrain to avoid z-fighting (+0.01m for decal roads, +0.0m for rivers which cut into terrain).

```
   Control points:  A ────── B ────── C ────── D
                        ↓ Catmull-Rom
   Sampled curve:   ·····•····•····•····•····•·····
                        ↓ Drape onto heightmap
   Ribbon mesh:     ╔═══╦═══╦═══╦═══╦═══╗
                    ║   ║   ║   ║   ║   ║  ← width
                    ╚═══╩═══╩═══╩═══╩═══╝
```

### Road Rendering

| Layer | Material | Notes |
|-------|----------|-------|
| Base cut | Flattens heightmap vertices under road | Optional — only for highways/rail |
| Surface | Road texture, UV along spline length | Tiled seamlessly |
| Edges | Shoulder/kerb decal strip | Blends road into terrain |
| Markings | Centre line, lane dash overlays | Medium/High quality only |

Roads also modify the **splat map** underneath — tiles under a road get their surface type overridden to `Asphalt` or `Concrete` so the blend is consistent if the road decal is LOD'd away at distance.

### Rail Rendering

Rails are rendered as:
1. A **ballast ribbon** (gravel texture, ~2.5m wide) draped on heightmap.
2. **Sleeper instances** placed at regular intervals along the spline.
3. **Rail meshes** — two thin metal strips offset from centre.

On Low quality, only the ballast ribbon is rendered (sleepers and rails skipped).

### River / Stream Rendering

Rivers differ from roads:

1. They **cut into** the heightmap — vertices within the river width are pushed down by `depth` metres.
2. The riverbed uses its own surface texture (mud/rock).
3. A **water plane** sits at the original terrain height, using the existing `WaterHelper` water rendering system.
4. Flow direction is derived from the spline direction → used for animated UV scrolling.

```
   Terrain surface  ─────╲         ╱─────
                          ╲ water ╱
   Riverbed          ──────╲─────╱──────  (vertices pushed down)
```

| Quality | River Rendering |
|---------|----------------|
| Low     | Flat blue ribbon, no depth cut |
| Medium  | Depth cut + animated flat water + flow direction |
| High    | Depth cut + reflection/refraction water + foam at banks |

Rivers also set `TileData.WaterLevel` on affected tiles so that gameplay systems (pathfinding, radiation spread, AI) correctly detect water without knowing about the spline.

---

## 6. Heightmap Terrain Modification at Runtime

Certain features need to **modify heightmap vertices** rather than just overlay:

| Feature | Modification |
|---------|-------------|
| Rivers | Cut channel into terrain |
| Highways / Rail | Flatten strip to reduce slope |
| Building foundations | Level a rectangular area |
| Craters (explosions) | Depress a circular area |

These modifications are applied during `ChunkTerrainBuilder.Build()` after the initial height sampling but before normal calculation. They are stored as **terrain modifiers** on the chunk:

```csharp
public abstract record TerrainModifier;

public record FlattenStrip(
    IReadOnlyList<Vector2> CentreLine,
    float Width,
    float TargetHeight) : TerrainModifier;

public record ChannelCut(
    IReadOnlyList<Vector2> CentreLine,
    float Width,
    float Depth) : TerrainModifier;

public record LevelRect(
    Vector2 Min, Vector2 Max,
    float TargetHeight) : TerrainModifier;

public record Crater(
    Vector2 Centre,
    float Radius,
    float Depth) : TerrainModifier;
```

### Modification Pipeline

```
TileData heights → base vertex grid → apply TerrainModifiers → subdivide → compute normals → upload mesh
```

---

## 7. Chunk Build Pipeline

```csharp
public class ChunkTerrainBuilder
{
    public ChunkTerrainMesh Build(ChunkData chunk, ChunkMode mode, QualityPreset quality,
        IReadOnlyList<LinearFeature> features, IChunkNeighborProvider neighbors)
    {
        // 1. Sample TileData heights → 17×17 base vertex grid
        // 2. Stitch edges with neighbor chunks
        // 3. Apply TerrainModifiers (flatten, cut, level, crater)
        // 4. Subdivide if quality > Low
        // 5. Compute normals
        // 6. Build splat map from TileData.Surface
        // 7. Build linear feature meshes (roads, rails, rivers)
        // 8. If Hybrid: build tile overlay data
        // 9. Return combined mesh + textures + overlay + feature meshes
    }
}
```

### Output

```csharp
public class ChunkTerrainMesh
{
    public Mesh HeightmapMesh { get; init; }
    public Texture SplatMap0 { get; init; }
    public Texture SplatMap1 { get; init; }
    public IReadOnlyList<Mesh> LinearFeatureMeshes { get; init; }
    public TileOverlayData? Overlay { get; init; }  // null in Heightmap mode
}
```

---

## 8. Integration with Existing Systems

### ChunkStreamingProcessor

No changes to streaming logic. When a chunk enters the 3×3 active grid, `ChunkTerrainBuilder.Build()` is called instead of the current per-tile mesh placement. When a chunk leaves, dispose the heightmap mesh + textures.

### Pathfinding / Movement

Pathfinding continues to use `TileData.HeightLevel` and `HeightHelper` — the heightmap mesh is a visual concern only. Movement cost, slope type, line-of-sight all work from the integer tile grid as today.

### Water System

`WaterHelper` is unchanged. Rivers additionally write `WaterLevel` into affected tiles during chunk build so gameplay systems pick up river water. Visual water rendering uses the existing water pass, with river-specific UV animation layered on.

### Map Generation

`MapGeneratorService` generates `TileData` grids + `LinearFeature` lists. The LLM prompt schema adds a `linear_features` array alongside the existing tile grid.

---

## 9. Performance Budget

| Component | Per Chunk | Notes |
|-----------|-----------|-------|
| Heightmap mesh | 1 draw call | Single mesh, single material (splat shader) |
| Splat textures | 2 × 16×16 RGBA | 512 bytes per chunk per splat (tiny) |
| Linear features | 1–3 draw calls | Batched by type (all roads, all rails, all rivers) |
| Tile overlay (Hybrid) | 2–8 draw calls | Batched by surface type, instanced structures |
| **Total (Heightmap)** | **3–5 draws** | Down from 256 (16×16 individual tiles) |
| **Total (Hybrid)** | **5–13 draws** | Still well below current per-tile approach |

### Memory

| Item | Size | Notes |
|------|------|-------|
| Heightmap vertices (High) | ~100 KB | 65×65 × 24 bytes (pos+normal+uv) |
| Splat textures | ~1 KB | 2 × 16×16 × 4 bytes |
| Linear feature mesh | ~5–20 KB | Depends on feature count/length |
| Tile overlay | ~10–30 KB | Only in Hybrid chunks |

---

## Scope

### In Scope

- Heightmap mesh generation from `TileData` height
- Texture splatting with 8 surface types
- Hybrid overlay rendering for towns/dungeons
- `ChunkMode` enum on `ChunkData`
- Linear feature data model and spline-to-mesh conversion
- Road, rail, river rendering with quality presets
- Terrain modifiers (flatten, cut, level, crater)
- Chunk edge stitching

### Out of Scope (Deferred)

- LOD system for distant chunks (further milestone)
- Vegetation/foliage scattering on heightmap (separate design)
- Interior dungeon rendering with ceiling/multi-floor (separate design)
- Procedural road network generation (MapGen concern)
- Destructible terrain at runtime beyond simple craters

---

## Files to Create / Modify

| Action | File | Notes |
|--------|------|-------|
| Create | `World/Terrain/ChunkTerrainBuilder.cs` | Heightmap mesh builder |
| Create | `World/Terrain/TerrainSplatBuilder.cs` | Splat map generation |
| Create | `World/Terrain/TerrainModifier.cs` | Modifier records |
| Create | `World/Terrain/HeightmapMeshGenerator.cs` | Vertex grid + subdivision |
| Create | `World/Terrain/TileOverlayBuilder.cs` | Hybrid mode overlay |
| Create | `World/Terrain/ChunkTerrainMesh.cs` | Output data class |
| Create | `World/LinearFeatures/LinearFeature.cs` | Data model |
| Create | `World/LinearFeatures/LinearFeatureType.cs` | Type enum |
| Create | `World/LinearFeatures/LinearFeatureRenderer.cs` | Spline → ribbon mesh |
| Create | `World/LinearFeatures/SplineMath.cs` | Catmull-Rom utilities |
| Create | `World/ChunkMode.cs` | Enum (Heightmap / Hybrid) |
| Create | `Rendering/TerrainSplatEffect.sdsl` | Stride splat shader |
| Modify | `World/ChunkData.cs` | Add `ChunkMode`, `TerrainModifiers`, `LinearFeatures` |
| Modify | `World/TileMapRendererScript.cs` | Delegate to `ChunkTerrainBuilder` |
| Modify | `World/ChunkStreamingProcessor.cs` | Pass mode to builder |
| Modify | `World/Rendering/QualitySettings.cs` | Add heightmap subdivision + river quality |

---

## Acceptance Criteria

1. A 3×3 chunk wilderness area renders as continuous heightmap terrain with smooth blended surface types.
2. A town chunk (`ChunkMode.Hybrid`) renders terrain + discrete floor tiles, walls, and props sitting on the heightmap surface.
3. A road spline crossing multiple chunks renders as a continuous draped ribbon with no gaps at chunk boundaries.
4. A river spline cuts into the terrain and displays animated water.
5. Rail tracks render with ballast + sleepers + rail meshes on High quality.
6. Chunk edges are seamless — no visible cracks between adjacent heightmap meshes.
7. All three quality presets (Low / Medium / High) produce correct output at their budgeted draw-call counts.
8. Pathfinding, movement cost, and line-of-sight continue to use `TileData` and remain unaffected by the rendering change.
9. Existing unit tests and UI tests pass without modification.
