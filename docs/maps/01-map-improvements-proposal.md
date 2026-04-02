# Map Improvements Proposal

> **Status:** Draft Proposal  
> **Date:** 2026-04-01  
> **Scope:** Terrain, height, water, buildings, and visual polish for the Oravey2 tile map system

---

## 1. Current State

The existing map system is a flat 2D tile grid rendered as colored cubes in Stride 3D:

| Aspect | Current | Limitation |
|--------|---------|------------|
| Tile geometry | Flat colored cubes (95% tile size) | No height variation, repetitive, visibly gridded |
| Tile types | 6 enum values (Empty, Ground, Road, Rubble, Water, Wall) | No sub-types, no biome textures |
| Height | `TileHeight = 0.1m`, `WallHeight = 1.0m` only | Binary flat-or-wall, no gradual elevation |
| Water | Single blue tile type, non-walkable | No flow, no depth, no shorelines |
| Buildings | Wall tiles arranged in rectangles | 2D footprints only, no vertical structure |
| Chunk size | 16×16 tiles, 3×3 streaming grid | Good foundation, no changes needed |

The system needs to evolve from flat colored blocks into a visually rich, multi-height terrain with water features and 3D structures — while preserving the existing chunk streaming and zone architecture.

---

## 2. Tile-Based Foundation (Preserve & Extend)

The tile-based approach stays. It aligns with the tactical RPG camera, chunk streaming, and pathfinding. What changes is **what each tile stores and how it renders**.

### 2.1 Extended Tile Data

Replace the flat `TileType` enum with a richer per-tile record:

```
TileData
├── SurfaceType     : enum (Dirt, Asphalt, Concrete, Grass, Sand, Mud, Rock, Metal)
├── HeightLevel     : int (0–255, each step = 0.25m → max 64m range)
├── WaterLevel      : int (0–255, water surface height, 0 = no water)
├── StructureId     : int (0 = none, references building/object definition)
├── Flags           : bitfield (Walkable, Irradiated, Burnable, Destructible, Indoor)
└── VariantSeed     : byte (per-tile randomization for visual variety)
```

**Backward compatibility:** `TileType` maps trivially into the new structure:
- `Ground` → `SurfaceType.Dirt, HeightLevel=1, WaterLevel=0`
- `Water` → `SurfaceType.Mud, HeightLevel=0, WaterLevel=2`
- `Wall` → `SurfaceType.Concrete, HeightLevel=1, StructureId=WallSegment`

### 2.2 Chunk Data Extension

Each `ChunkData` gains:
- **Height map:** baked into per-tile `HeightLevel`, no separate array needed
- **Water table:** baked into per-tile `WaterLevel`
- **Structure references:** `StructureId` pointing into a shared structure registry

---

## 3. Multi-Height Terrain

### 3.1 Height Model

Every tile has an integer height level (0–255). Adjacent tiles with different heights produce visible slopes or cliffs.

| Scenario | Height Δ | Visual Treatment |
|----------|----------|------------------|
| Flat ground | 0 | Standard tile surface |
| Gentle slope | 1–2 | Smoothly interpolated ramp between tiles |
| Small hill | 3–6 | Visible rise, walkable with movement cost penalty |
| Steep cliff | 7–12 | Cliff face rendered, not directly walkable (needs stairs/ladder) |
| Large mountain | 13+ | Impassable rock face, blocks line of sight |

### 3.2 Height Rendering

Each tile's mesh is a **column** from Y=0 up to `HeightLevel × 0.25m`. The top face is the walkable surface. Between adjacent tiles of different heights:

- **Δ ≤ 2:** Generate a **slope mesh** that smoothly connects the two heights. Use vertex interpolation across the shared edge to create a gentle ramp.
- **Δ 3–6:** Generate a **stepped slope** — visible incline but still navigable. Apply a movement cost multiplier (`1.5×` per height step).
- **Δ ≥ 7:** Generate a **cliff face** with a vertical wall texture. Impassable without a structure (stairs, ladder, rope).

### 3.3 Height–Pathfinding Integration

The A* pathfinder needs a height-aware cost function:

```
MovementCost(from, to) =
    baseCost
    + abs(from.Height - to.Height) × slopePenalty
    + (isCliff ? IMPASSABLE : 0)
```

Line-of-sight checks also need height: a unit on height 10 can see over a wall on height 5.

### 3.4 Terrain Examples

**Small hills:** A cluster of tiles at heights 2–4 surrounded by height-1 ground. Gently rolling post-apocalyptic wasteland.

**Ruined highway overpass:** A strip of Road tiles at height 8, with cliff edges. Ground level below at height 1. A ramp structure at one end (height 1→8 over 8 tiles).

**Mountain ridge:** Heights 15–20 along the map edge, acting as a natural barrier. A single pass at height 10 with a road through it.

**Crater (irradiated):** A bowl shape — heights descending from 5 at the rim to 0 at the center, with irradiated water pooling at the bottom.

---

## 4. Water System

### 4.1 Water Model

Each tile has a `WaterLevel` independent of its terrain `HeightLevel`.

- Water is present when `WaterLevel > HeightLevel` (the terrain is submerged)
- Water depth = `WaterLevel - HeightLevel`
- Water surface is rendered as a translucent plane at Y = `WaterLevel × 0.25m`

| Depth | Effect |
|-------|--------|
| 0 (dry) | Normal tile |
| 1–2 (shallow) | Walkable with speed penalty (0.5×), splashing effects |
| 3–4 (wading) | Walkable with heavy penalty (0.3×), equipment gets wet |
| 5+ (deep) | Non-walkable, requires swimming ability or boat |

### 4.2 Rivers

A river is a connected path of tiles with `WaterLevel > HeightLevel`:

- **Flow direction:** Stored per-chunk as a `Vector2` (rivers flow downhill). Used for animated surface UVs and gameplay (carried downstream).
- **Banks:** Tiles at the river edge where `HeightLevel ≈ WaterLevel` get shoreline blending (wet mud/sand surface type).
- **Width:** Typically 2–5 tiles wide. Narrow sections (1–2 tiles) are fordable.

### 4.3 Bridges

A bridge is a structure spanning water tiles:

```
BridgeDefinition
├── StartTile, EndTile     : coordinates
├── DeckHeight             : int (height of the walkable bridge surface)
├── Width                  : int (1–3 tiles)
├── BridgeType             : enum (Wooden, Concrete, Metal, Makeshift)
└── Condition              : float (0.0–1.0, affects walkability and appearance)
```

Bridge tiles override walkability: although the terrain below is water, the bridge deck provides a walkable surface at `DeckHeight`. A damaged bridge (`Condition < 0.3`) may have gaps.

### 4.4 Lakes

A lake is a contiguous region of tiles where `WaterLevel` is uniform and `HeightLevel` forms a bowl beneath:

- Lake surface is one flat water plane across all tiles (no per-tile wave offset)
- Depth varies with terrain underneath
- Shoreline tiles get a blended shore texture
- Can contain radiation (irradiated lake = glowing green tint)

### 4.5 Coastline / Seaside

Map edges can be sea-level water extending to the boundary:

- Edge chunks contain terrain sloping down to `HeightLevel = 0` with `WaterLevel = 3`
- Wave animation on the coastal edge (sinusoidal vertex displacement on the water mesh)
- Beach: a strip of `SurfaceType.Sand` tiles between land and water
- Tide system (optional): `WaterLevel` oscillates ±1 on a 2-hour in-game cycle, exposing or flooding the beach strip

---

## 5. 3D Buildings

### 5.1 Building Model

Replace the flat wall-tile rectangles with proper 3D structures:

```
BuildingDefinition
├── Id                : int
├── Name              : string ("Elder's House", "Ruined Office Tower")
├── Footprint         : TileCoord[] (occupied ground tiles)
├── Floors            : FloorDefinition[]
│   ├── FloorHeight   : float (3.0m typical)
│   ├── WallSegments  : WallSegment[] (exterior/interior walls with door/window openings)
│   ├── RoofType      : enum (Flat, Sloped, Collapsed, None)
│   └── Interactables : InteractableSpawn[]
├── Condition         : float (0.0 = rubble, 1.0 = intact)
├── Size              : enum (Small, Large)
├── InteriorChunkId   : int? (Large buildings only — separate interior map)
└── MeshAssetPath     : string (path to meshy.ai generated model)
```

### 5.2 Rendering Strategy — AI-Generated Models

All building models are generated using **[meshy.ai](https://www.meshy.ai/)** and placed as single entity meshes. There is no procedural assembly tier — every building gets a unique, authored 3D model.

**Workflow:**
1. Generate building mesh in meshy.ai (text-to-3D or image-to-3D)
2. Export as glTF/FBX, import into the Stride asset pipeline
3. Assign to a `BuildingDefinition` via `MeshAssetPath`
4. Runtime places the model entity at the footprint center, scaled to fit the tile footprint

**Benefits:** Unique visual identity per building, fast iteration, no modular piece authoring needed.

**Considerations:** Model file size budget (~1–5 MB per building), LOD variants for distant rendering, consistent art style across generated models (use meshy.ai style presets).

### 5.3 Building–Terrain Interaction

- Building footprint tiles are **non-walkable** — the player cannot walk through buildings
- Buildings occlude line of sight from ground level
- Upper floors provide height advantage for combat (effective height = terrain height + floor × 3m)
- Collapsed buildings partially revert to Rubble tiles

### 5.4 Interior Access Rules

**Small buildings** (1–4 tile footprint): **Not enterable.** The player interacts with them from the outside only (e.g., loot a window, talk to an NPC at the door). Keeps scope manageable and avoids interior authoring for every minor structure.

**Large buildings** (5+ tile footprint): **Always load a separate interior chunk** when the player enters a door tile. The exterior chunk remains loaded. The interior chunk contains its own tile grid, entities, lighting, and navigation — fully independent from the exterior.

```
Exterior Chunk (stays loaded)
  └── Door tile at (5, 3) → triggers ChunkTransitionEvent
        └── Loads InteriorChunkId = 42
              └── Interior Chunk 42 (own 16×16 tile grid, own entities)
                    └── Exit door tile → unloads interior, returns to exterior
```

---

## 6. Visual Tile Techniques — Breaking the Grid

This is the most critical visual improvement. Square tiles look like a chess board. Several complementary techniques eliminate the grid feel.

### 6.1 Sub-Tile Assembly (8-4-4 Method)

Based on [Fossil Hunters' dynamic tile system](https://www.gamedeveloper.com/programming/creating-a-dynamic-tile-system):

Instead of one mesh per tile, each tile is composed of **4 sub-tiles** (quadrants: NE, SE, SW, NW). Each sub-tile selects one of **4 mesh shapes** (Fill, Outer Corner, Inner Corner, Edge) based on the **8 surrounding neighbors**.

```
8 neighbor lookups  ×  4 art assets  ×  4 sub-tiles = seamless tiling

Sub-tile selection logic (for NE quadrant):
  Look at 3 neighbors: N, NE, E
  ├── All same type    → Fill
  ├── N or E different → Edge (rotated to match)
  ├── Both N+E same, NE different → Inner Corner
  └── Both N+E different → Outer Corner
```

**Why this works:** Only 4 mesh pieces are needed per surface type, yet the visual result has smooth organic edges with proper inner/outer corners. No visible grid.

**Stride implementation:** Each sub-tile is a child entity with a `ModelComponent` referencing one of the 4 shared meshes. Rotation is applied via `Entity.Transform.Rotation` (0°, 90°, 180°, 270°).

### 6.2 Triplanar Texture Mapping

Instead of UV-unwrapping each sub-tile mesh, use **triplanar mapping** in a custom Stride shader:

- Texture coordinates are derived from **world position** rather than mesh UVs
- Textures tile seamlessly across any mesh geometry regardless of shape
- Eliminates texture seams between sub-tiles and between adjacent tiles
- Different texture layers blend based on surface normal (top = ground texture, sides = cliff texture)

This is especially effective for cliff faces and sloped terrain where traditional UVs would stretch.

### 6.3 Edge Jittering

Apply slight random displacement to tile edge vertices using `VariantSeed`:

```
vertexOffset = noise(worldPos + variantSeed) × jitterAmount
```

Where `jitterAmount = 0.05–0.1` world units. This breaks the perfectly straight tile boundaries without affecting collision or pathfinding (which still uses the integer grid).

### 6.4 Multi-Texture Blending at Borders

Where two surface types meet (e.g., Dirt ↔ Asphalt), blend their textures over a small transition zone rather than a hard cut:

- Use a **splat map** approach: each vertex stores blend weights for up to 4 surface types
- At tile borders, vertices shared between different surface types get 50/50 blend
- The fragment shader mixes texture samples based on weights + a noise pattern for organic breakup

### 6.5 Vertex Color Variation

Subtle per-vertex color tinting driven by `VariantSeed`:

- Slight hue shift (±5%) and brightness variation (±10%) per tile
- Prevents the "wallpaper" effect of perfectly uniform ground
- Minimal cost — just vertex colors, no extra textures

### 6.6 Detail Scattering

Small decorative meshes (pebbles, cracks, grass tufts, debris) placed procedurally on tiles:

- Placement derived from `VariantSeed` — deterministic, no storage needed
- 2–5 detail objects per tile, instanced rendering for performance
- Biome-specific: grass in ForestOvergrown, rubble in RuinedCity, bones in Wasteland

---

## 7. Additional Map Considerations

### 7.1 Fog of War / Exploration

- Tiles start unexplored (black), become revealed (dimmed) when in range, and fully lit when in line of sight
- Explored tiles remember their last-seen state (enemy positions may have changed)
- Implemented as a per-tile `VisibilityState` enum: `Unexplored | Revealed | Visible`
- Rendered as a screen-space overlay blending to black/grey

### 7.2 Navigation Mesh Integration

The tile grid defines walkability, but smooth pathfinding and movement requires a **nav mesh**:

- Generate a simplified nav mesh from the tile walkability + height data
- Stride has `NavigationComponent` support — integrate with the chunk system
- Regenerate nav mesh per chunk on load (pre-baked preferred, runtime fallback)

### 7.3 Minimap

- Render the tile grid from above into an offscreen render target
- Color-code by surface type, overlay icons for structures and NPCs
- Fog of war applies to the minimap too
- Show the 3×3 active chunk area with the player centered

### 7.4 Tile Lighting

- Each tile can have a light level (0–15, Minecraft-style) from:
  - Ambient (day/night cycle baseline)
  - Point lights (campfires, street lamps, building interiors)
  - Radiation glow (irradiated tiles emit green light)
- Light propagation: flood-fill from light sources, attenuating per tile distance
- Affects gameplay: stealth mechanics, enemy detection ranges

### 7.5 Destructible Terrain

- Tiles with the `Destructible` flag can be damaged by explosions or heavy weapons
- Destruction reduces `Condition → 0`, converting the tile to Rubble
- Walls can be breached, creating alternate paths
- Building destruction follows the same system (structural integrity propagation)

### 7.6 Level-of-Detail (LOD) for Distant Chunks

Although only 3×3 chunks are fully loaded, rendering a flat horizon is ugly:

- Adjacent chunks (ring 2–3) rendered as simplified low-poly terrain with no entities
- Distant chunks (ring 4+) rendered as a flat colored heightmap or billboard
- Skybox and atmospheric haze blend the LOD boundary

### 7.7 Tile Animation

Some tiles need animation:
- **Water:** Scrolling UV offset + sine wave vertex displacement
- **Irradiated zones:** Pulsing green glow
- **Lava/fire:** (if added) Animated emissive texture
- **Vegetation:** Subtle wind sway on grass/tree detail objects

### 7.8 Sound Zones

Surface type drives footstep audio and ambient sounds:
- `Dirt` → soft crunch
- `Asphalt` → hard tap
- `Metal` → clang
- `Water (shallow)` → splash
- Ambient: wind for open wasteland, dripping for underground, waves for coastal

---

## 8. Performance Budget

| System | Target Budget |
|--------|---------------|
| Active tiles | 9 chunks × 256 tiles = 2,304 tiles |
| Sub-tile entities | 2,304 × 4 = 9,216 sub-tiles (instanced) |
| Water planes | ~200–500 water tiles per chunk max |
| Detail objects | ~5,000 instanced (across 9 chunks) |
| Building meshes | ~50–100 modular pieces per chunk |
| Draw calls | Target < 500 (heavy instancing) |
| Memory per chunk | ~16 KB tile data + mesh references |

**Key optimization strategies:**
- Mesh instancing for sub-tiles (only 4 unique meshes per surface type)
- Frustum culling at chunk level
- LOD switching for distant tiles
- Object pooling for chunk load/unload
- Texture atlases to minimize material switches

---

## 9. Implementation Priority

| Phase | Work | Depends On |
|-------|------|------------|
| **Phase 1** | Extended `TileData` structure, height levels, basic height rendering | — |
| **Phase 2** | Sub-tile assembly (8-4-4), triplanar shaders, edge jittering | Phase 1 |
| **Phase 3** | Water system (levels, rivers, shoreline blending) | Phase 1 |
| **Phase 4** | 3D building definitions, modular building meshes | Phase 1 |
| **Phase 5** | Bridges, building interiors, destructible terrain | Phases 3 + 4 |
| **Phase 6** | Detail scattering, animation, LOD, fog of war | Phase 2 |
| **Phase 7** | Sound zones, minimap, nav mesh integration | Phase 6 |

Each phase is independently testable and shippable. The existing flat renderer continues to work until Phase 2 replaces it.

---

## 10. Open Questions

1. **Tile size:** Stay at 1.0m or increase to 2.0m? Larger tiles = fewer draw calls but coarser height resolution.
2. **Indoor maps:** Separate chunks or in-place with transparent roofs? Both have tradeoffs (loading vs. rendering).
3. **Water physics:** Should flowing water push the player, or is it purely visual?
4. **Art pipeline:** Who creates the 4 sub-tile meshes per surface type? Procedural generation or hand-authored?
5. **Height map authoring:** In-game level editor or external tool (heightmap PNG import)?
