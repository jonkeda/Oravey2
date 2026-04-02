# Tile Rendering Techniques — Deep Dive

> **Companion to:** [01-map-improvements-proposal.md](01-map-improvements-proposal.md)  
> **Focus:** Detailed technical approaches for breaking the grid look in tile-based 3D games

---

## 1. The Grid Problem

When every tile is a uniform square, the human eye instantly perceives the repetitive grid pattern. This destroys immersion regardless of texture quality. The goal is to make the world look organic while keeping the underlying data model tile-based for gameplay, pathfinding, and streaming.

There are **six complementary layers** that stack to eliminate grid perception. No single technique is sufficient alone.

---

## 2. Technique 1 — Sub-Tile Assembly (8-4-4 Method)

### Source

[Creating a Dynamic Tile System](https://www.gamedeveloper.com/programming/creating-a-dynamic-tile-system) — Ryan Miller / Reptoid Games (Fossil Hunters), 2017.

### Concept

Each tile is divided into 4 **quadrants** (NE, SE, SW, NW). Each quadrant independently selects one of 4 mesh shapes based on the 8 surrounding neighbor tiles. The quadrant meshes are joined seamlessly.

```
┌───────┬───────┐
│  NW   │  NE   │
│       │       │
├───────┼───────┤
│  SW   │  SE   │
│       │       │
└───────┴───────┘
    One Tile
```

### The 4 Mesh Shapes

```
┌─────┐  ┌─────┐  ┌─────┐  ┌─────┐
│█████│  │████ ░│  │███░░│  │░████│
│█████│  │█████│  │██░░░│  │░████│
│█████│  │█████│  │█░░░░│  │█████│
└─────┘  └─────┘  └─────┘  └─────┘
 Fill      Edge     Outer    Inner
                    Corner   Corner
```

- **Fill:** Solid — all 3 relevant neighbors are the same type
- **Edge:** One adjacent cardinal neighbor is different
- **Outer Corner:** Both cardinal neighbors are different
- **Inner Corner:** Both cardinals are same, but the diagonal is different

### Neighbor Lookup per Quadrant

| Quadrant | Cardinal 1 | Cardinal 2 | Diagonal |
|----------|-----------|-----------|----------|
| NE | North | East | NorthEast |
| SE | South | East | SouthEast |
| SW | South | West | SouthWest |
| NW | North | West | NorthWest |

### Decision Logic (per quadrant)

```
if (cardinal1 != self AND cardinal2 != self):
    shape = OuterCorner
elif (cardinal1 != self OR cardinal2 != self):
    shape = Edge
    rotation = toward the different neighbor
elif (diagonal != self):
    shape = InnerCorner
else:
    shape = Fill
```

### Asset Count

Per surface type you need exactly **4 mesh pieces** (Fill, Edge, OuterCorner, InnerCorner). With 8 surface types that's 32 small meshes total — very manageable.

Compare: classic 16-tile approach needs 16 meshes per type without inner corners, or 81 with inner corners.

### Stride Implementation Notes

```
Per tile → 1 parent Entity (position at tile center)
  ├── Child Entity "NE" → ModelComponent (one of 4 meshes, rotated)
  ├── Child Entity "SE" → ModelComponent
  ├── Child Entity "SW" → ModelComponent
  └── Child Entity "NW" → ModelComponent
```

Meshes are shared (instanced) across all tiles of the same type. Only the parent entity position and child rotation vary.

**Memory:** 4 child entities × 2,304 tiles = 9,216 entities. Stride handles this fine with instanced rendering.

---

## 3. Technique 2 — Triplanar Texture Mapping

### Problem

UV-unwrapping sub-tile meshes creates seams wherever two pieces meet. Authoring seamless UVs across all 4 shapes for all rotations is painful and limits texture variety.

### Solution

Map textures using **world-space coordinates** instead of mesh UVs. The shader projects texture along X, Y, and Z world axes, then blends based on the surface normal direction.

### Shader Pseudocode (SDSL for Stride)

```hlsl
shader TriplanarMapping : ShadingBase
{
    Texture2D TextureX;
    Texture2D TextureY;  // top-down (most tiles use this)
    Texture2D TextureZ;
    float TilingScale = 1.0;

    override float4 Shading()
    {
        float3 worldPos = streams.PositionWS.xyz * TilingScale;
        float3 normal = abs(streams.NormalWS);
        
        // Normalize blending weights
        float3 blend = normal / (normal.x + normal.y + normal.z);
        
        float4 texX = TextureX.Sample(worldPos.yz);
        float4 texY = TextureY.Sample(worldPos.xz);  // top face
        float4 texZ = TextureZ.Sample(worldPos.xy);
        
        return texX * blend.x + texY * blend.y + texZ * blend.z;
    }
}
```

### Benefits

- Zero UV seams between any meshes
- Textures tile infinitely and seamlessly
- Cliff sides automatically get the side texture, flat surfaces get the top texture
- Adding height variation later doesn't break texturing

### Considerations

- Slightly more expensive than standard UV mapping (3 texture samples instead of 1)
- Mitigated by: most tiles are flat (Y normal dominant) → can early-out and sample only TextureY
- Need to handle texture tiling scale carefully to avoid visible repetition

---

## 4. Technique 3 — Edge Jittering (Vertex Displacement)

### Problem

Even with sub-tile assembly, edges between different surface types are perfectly aligned to the grid. Nature doesn't have straight edges.

### Solution

Offset vertices along tile boundaries by a small noise-based displacement:

```
displacement = perlinNoise(worldPos.xz * frequency + seed) × amplitude
```

Parameters:
- `frequency = 4.0` — variation within a tile
- `amplitude = 0.08` — max 8cm displacement (subtle but effective)
- `seed` = tile's `VariantSeed` — deterministic per tile

### Rules

- Only displace vertices on the **edge** between two different surface types
- Never displace corner vertices that are shared by 3+ tiles (causes T-junctions)
- Interior fill vertices: no displacement (they're already seamless)
- Collision and pathfinding still use the integer grid — jitter is purely visual

### Stride Implementation

Apply displacement in a vertex shader or during mesh generation (bake into vertex buffer at chunk load time). Baking is cheaper at runtime.

---

## 5. Technique 4 — Multi-Texture Splatting at Borders

### Problem

Adjacent tiles of different types (Dirt next to Asphalt) still have an abrupt transition even with correct sub-tile shapes.

### Solution

At tile borders, blend the textures of adjacent types using a **splat map** technique:

1. Each vertex stores **blend weights** for up to 4 surface types (as vertex colors RGBA)
2. Interior vertices: weight = 1.0 for the tile's own type
3. Border vertices: weight = 0.5 for own type + 0.5 for neighbor type
4. Fragment shader samples all relevant textures and blends by weight

### Enhanced Blending with Height Maps

For more natural transitions, use **height-map based blending** instead of linear interpolation:

```hlsl
float blend = smoothstep(0.4, 0.6, heightA / (heightA + heightB) + noise * 0.3);
```

This makes dirt "creep into" cracks in asphalt, grass "grow over" road edges — much more natural than a 50/50 fade.

### Texture Atlas

Pack all surface textures into a single **texture array** or **atlas** to minimize material switches. The vertex color channels encode which texture indices to sample.

---

## 6. Technique 5 — Per-Tile Visual Variation

### Problem

Even with good textures, large areas of the same type look like wallpaper.

### Solution: Multi-Layered Variation

**A) Color tinting (cheapest):**
- Per-vertex color modulation: `finalColor *= lerp(0.9, 1.1, hash(variantSeed))`
- Gives ±10% brightness variation per tile
- No extra textures needed

**B) Texture rotation/offset:**
- Rotate the texture sampling UV by 0°, 90°, 180°, or 270° per tile (based on `VariantSeed % 4`)
- Breaks repetitive patterns without any extra art

**C) Detail texture overlay:**
- A secondary high-frequency detail texture multiplied on top of the base
- Different detail texture per biome (cracks for urban, roots for forest)

**D) Macro variation:**
- Low-frequency world-space noise modulating the base color
- Creates large-scale color gradients (darker patches, lighter patches across many tiles)
- Prevents the "uniform sea of brown" problem

---

## 7. Technique 6 — Detail Object Scattering

### Problem

Flat textured surfaces still look flat. Real terrain has 3D debris, vegetation, and detail.

### Solution

Scatter small instanced meshes on tile surfaces procedurally:

```
Per tile:
  rng = SeededRandom(tile.VariantSeed)
  count = biome.detailDensity × rng.Range(0.5, 1.5)
  for i in 0..count:
      position = tile.center + rng.Offset(-0.4, 0.4)
      rotation = rng.Angle(0, 360)
      scale = rng.Range(0.8, 1.2)
      meshIndex = rng.Choose(biome.detailMeshes)
      → spawn instanced detail at (position, rotation, scale, meshIndex)
```

### Detail Meshes per Biome

| Biome | Detail Objects |
|-------|---------------|
| Wasteland | Small rocks, dried bones, spent casings, dead grass tufts |
| RuinedCity | Concrete chunks, rebar, broken glass, paper debris |
| ForestOvergrown | Grass clumps, mushrooms, fallen branches, ferns |
| Coastal | Shells, seaweed, driftwood, smooth pebbles |
| Industrial | Pipes, rusty bolts, oil stains (decal), wire tangles |

### Performance

- All detail objects of the same mesh type rendered as **GPU instanced** draw calls
- Typically 5–10 unique detail meshes per biome × 1 draw call each = 5–10 extra draw calls total
- LOD: details only rendered for tiles within 20m of camera, fade out at 15–20m
- Budget: ~5,000 instances across 9 loaded chunks

---

## 8. Putting It All Together — Render Stack

From bottom to top, the full tile render pipeline:

```
Layer 0: Height Column
  └── Tile mesh extruded to HeightLevel, cliff faces on sides

Layer 1: Sub-Tile Assembly (8-4-4)
  └── 4 quadrant meshes per tile, shaped by neighbor analysis

Layer 2: Triplanar Texturing
  └── World-space UV mapping, no seams

Layer 3: Border Splatting
  └── Multi-texture blending at surface type transitions

Layer 4: Edge Jitter
  └── Vertex displacement at borders (baked)

Layer 5: Per-Tile Variation
  └── Color tint, UV rotation, macro noise

Layer 6: Water Plane
  └── Translucent animated surface at WaterLevel

Layer 7: Detail Scattering
  └── Instanced 3D debris/vegetation

Layer 8: Structures
  └── Building meshes, bridges, props
```

Each layer is independent and can be developed/tested in isolation. The existing colored-cube renderer maps to skipping layers 1–5 and 7.

---

## 9. Alternative Approaches Considered

### Marching Cubes / Voxel Mesh Generation

Generates smooth organic terrain from volumetric data. Produces beautiful results but:
- Very expensive at runtime for large worlds
- Difficult to control artistic direction
- Doesn't align with tile-based gameplay
- **Verdict:** Not suitable for this project's tile-based tactical gameplay

### Hex Tiles

Hexagonal grids eliminate the 4-directional bias of square grids and look less "griddy" by default. However:
- Movement in 6 directions is less intuitive for tactical RPG
- Existing code, chunk system, and coordinate math all assume square grid
- Building placement on hex grids is awkward
- **Verdict:** Too much rework for marginal visual benefit when sub-tile assembly solves the visual problem

### Wang Tiles

A mathematical tiling system where tile edges are labeled with colors, and tiles are placed such that adjacent edges always match. Produces non-repeating patterns from a small tile set.
- Very elegant for 2D, complex to extend to 3D
- Requires careful art authoring of many edge-matching tiles
- **Verdict:** Worth investigating for ground texture variety, but sub-tile assembly + triplanar mapping achieves similar results with less art effort

### Terrain Mesh with Heightmap (traditional 3D approach)

A single continuous mesh deformed by a heightmap texture:
- Beautiful rolling terrain, standard approach in 3D games
- Loses the tile-based structure (tiles become just data, not geometry)
- Harder to do per-tile gameplay effects (damage, type changes)
- **Verdict:** Could be used for distant LOD chunks, but active chunks should stay tile-based for gameplay alignment

---

## 10. Recommended Reading & References

1. **Sub-tile / 8-4-4 Method:** [Creating a Dynamic Tile System](https://www.gamedeveloper.com/programming/creating-a-dynamic-tile-system) — Ryan Miller, Reptoid Games
2. **Triplanar Mapping:** Standard technique documented in GPU Gems and many shader tutorials. Stride supports custom SDSL shaders where this can be implemented.
3. **Terrain Splatting:** GPU Gems 3, Ch. 1 — "Generating Complex Procedural Terrains Using the GPU"
4. **Wang Tiles:** "An Alternative for Wang Tiles" — Kopf et al., ACM SIGGRAPH 2006
5. **Detail Scattering / GPU Instancing:** Stride Engine docs on `InstancingComponent` and hardware instancing
6. **Height-Based Texture Blending:** "Advanced Terrain Texture Splatting" — common technique where blend weights are modulated by a height texture to create natural-looking borders
