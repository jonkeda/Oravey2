# Map Implementation — Decisions & Scope

> **Status:** Decision Record  
> **Date:** 2026-04-02  
> **Input:** Discussion on docs 01–03, current codebase state

---

## 1. Scope Boundary — Map = Static World Only

The map file defines **terrain, water, structures, and static props**. Nothing else.

| In Scope (map file) | Out of Scope (separate files) |
|---------------------|-------------------------------|
| Tile surface types | Enemies / enemy groups |
| Height levels | NPCs / dialogue |
| Water levels | Quests / quest hooks |
| Building footprints + mesh refs | Containers / loot tables |
| Static props (lamp posts, rubble piles, benches) | Factions |
| Roads | Spawn rules / respawn timers |
| Zone boundaries + biome assignment | Day/night schedules |
| Walkability flags | Player start (goes in world.json) |

The map compiler outputs **only** terrain chunks. Entity placement is a separate pass driven by separate data files that *reference* the map (e.g., "spawn radrats in zone_downtown_east"). This keeps the map file clean, the LLM prompt focused, and the systems independently versionable.

The `entities`, `containers`, and `questHooks` sections from doc 03 move to their own files (entity-layer, quest-layer). The map blueprint shrinks to terrain + water + roads + buildings + props + zones.

---

## 2. Performance & Quality Settings

### 2.1 Target Platforms

The renderer must run on:

| Platform | GPU Budget | Target FPS |
|----------|-----------|------------|
| Desktop (mid-range) | GTX 1060 / RX 580 | 60 fps |
| Desktop (low-end) | Intel UHD 630 | 30 fps |
| Mobile (high-end) | Adreno 740 / Apple A17 | 30 fps |
| Mobile (mid-range) | Adreno 619 / Apple A14 | 30 fps |

Mobile is a hard constraint. Every rendering technique must have a cheaper fallback.

### 2.2 Quality Presets

Three presets, selectable at runtime:

| Setting | Low (Mobile) | Medium (Desktop) | High (Desktop) |
|---------|-------------|-------------------|----------------|
| Sub-tile assembly | OFF — 1 mesh per tile | ON — 4 quadrants/tile | ON |
| Triplanar mapping | OFF — standard UV | ON — 2-sample fast path | ON — 3-sample full |
| Edge jittering | OFF | ON (baked) | ON (baked) |
| Border splatting | OFF — hard edge | 2-texture blend | 4-texture + height blend |
| Detail scattering | OFF | 50% density, 10m range | 100% density, 20m range |
| Water | Flat color plane | Scrolling UV + transparency | UV + vertex waves + reflections |
| Per-tile variation | Color tint only | Tint + UV rotation | Tint + UV rotation + detail overlay |
| LOD rings | Ring 1 only (3×3) | Ring 1 + simplified ring 2 | Ring 1 + ring 2 + distant billboard |
| Shadow quality | No shadows | Low-res shadow map | Full shadow cascade |
| Draw call budget | < 200 | < 500 | < 800 |

### 2.3 Settings System

```
QualitySettings
├── Preset        : enum { Low, Medium, High, Custom }
├── SubTileAssembly : bool
├── TriplanarMode   : enum { Off, Fast, Full }
├── EdgeJitter      : bool
├── BorderBlend     : enum { None, Simple, Full }
├── DetailDensity   : float (0.0–1.0)
├── DetailRange     : float (meters, 0–20)
├── WaterQuality    : enum { Flat, Animated, FullReflection }
├── TileVariation   : enum { TintOnly, TintRotation, Full }
├── LodRings        : int (1–3)
└── ShadowQuality   : enum { Off, Low, High }
```

Auto-detect on first launch: read GPU info, pick Low for mobile / integrated GPU, Medium for discrete, High for high-end. User can override in the settings menu (already exists via `SettingsMenuScript`).

### 2.4 Performance Budgets per Platform

**Mobile (Low preset):**
- Active tiles: 9 chunks × 256 = 2,304 tiles → 2,304 simple meshes (no sub-tiles)
- No detail objects
- Flat water planes (1 draw call per water region)
- Target: < 200 draw calls, < 50 MB GPU memory
- Key optimization: merge all tiles of the same surface type in a chunk into a single combined mesh (1 draw call per surface type per chunk instead of 1 per tile)

**Desktop (Medium/High preset):**
- Active tiles: 2,304 × 4 quadrants = 9,216 sub-tile meshes (instanced)
- Detail objects: 2,500–5,000 instances
- Animated water
- Target: < 500–800 draw calls

### 2.5 Chunk Mesh Batching

The biggest win for mobile: instead of 256 individual entity meshes per chunk, **batch all tiles into a single mesh per surface type per chunk**.

```
Chunk (0,0):
  Mesh "dirt"     → 1 draw call (180 tiles merged)
  Mesh "asphalt"  → 1 draw call (60 tiles merged)
  Mesh "rubble"   → 1 draw call (16 tiles merged)
  Water plane     → 1 draw call
  ─────────────────────────────
  Total: 4 draw calls (vs 256 naive)
```

Batched meshes are rebuilt when a chunk loads. On desktop with sub-tile assembly, the same batching applies but to sub-tile quadrants.

---

## 3. Static Props (Art Integration)

### 3.1 What Are Props?

Props are non-interactive decorative objects placed on the map. They're not enemies, NPCs, or containers — they're scenery.

| Category | Examples |
|----------|---------|
| Street furniture | Lamp posts, benches, mailboxes, fire hydrants |
| Debris | Rubble piles, car wrecks, fallen signs, collapsed scaffolding |
| Nature | Dead trees, boulders, grass clumps, shrubs |
| Infrastructure | Power lines, chain-link fence segments, barricades |
| Atmosphere | Trash bags, shopping carts, tires, barrels |

### 3.2 Prop Placement in the Map Blueprint

Props are part of the map (static world), not the entity layer:

```jsonc
"props": [
  {
    "id": "prop_lamppost_01",
    "meshAsset": "props/lamp_post_rusty",
    "placement": { "chunkX": 2, "chunkY": 3, "localTileX": 5, "localTileY": 8 },
    "rotation": 0,
    "scale": 1.0,
    "blocksWalkability": false
  },
  {
    "id": "prop_car_wreck_03",
    "meshAsset": "props/car_wreck_sedan",
    "placement": { "chunkX": 1, "chunkY": 2, "localTileX": 10, "localTileY": 4 },
    "rotation": 45,
    "scale": 1.0,
    "blocksWalkability": true,
    "footprint": [[10,4], [11,4]]
  }
]
```

### 3.3 Props vs Detail Scattering

These are different systems:

| | Props | Detail Scatter |
|-|-------|---------------|
| Placement | Authored (in blueprint) | Procedural (from VariantSeed) |
| Size | Medium–large (0.5–5m) | Small (< 0.3m) |
| Collision | Optional (blocksWalkability) | Never |
| Uniqueness | Each has identity + position | Anonymous visual filler |
| Asset source | meshy.ai / hand-modeled | Simple low-poly meshes |
| LOD | Yes (distant simplification) | Fade-out by distance |

Both are purely visual map content. Neither carries gameplay logic.

### 3.4 Prop Art Pipeline

Same as buildings: generate via meshy.ai, export glTF/FBX, import to Stride asset pipeline, reference by `meshAsset` path. Keep a shared prop library — most props are reused across maps.

### 3.5 Performance Considerations

- Props with the same mesh and material → GPU instanced (1 draw call per unique prop type)
- Budget: ~200 unique prop instances per chunk in High, ~50 in Low
- Low preset: skip props entirely, or show only large ones (car wrecks, trees) within 15m
- Props are loaded/unloaded with their chunk

---

## 4. Backward Compatibility — Not Required, But UI Tests Must Work

### 4.1 What Breaks

The new `TileData` record replaces the `TileType` enum as the per-tile data structure. This breaks:

| Component | Change |
|-----------|--------|
| `TileType` enum | Replaced by `SurfaceType` enum + `TileData` record |
| `TileMapData.Tiles[,]` | Changes from `TileType[,]` to `TileData[,]` |
| `TileMapData.IsWalkable()` | Now checks `TileData.Flags.HasFlag(Walkable)` instead of hardcoded enum list |
| `TileMapRendererScript` | Consumes `TileData` instead of `TileType`, renders sub-tiles or batched meshes |
| `TownMapBuilder` / `WastelandMapBuilder` | Produce `TileData` arrays instead of `TileType` arrays |
| `GetTileAtWorldPos` handler | Returns `SurfaceType` name instead of `TileType` name |
| `ChunkData` | Extended with height/water layers |

### 4.2 What UI Tests Actually Depend On

Reviewing all UI test dependencies on the map system:

| UI Test | What It Uses | What Must Survive |
|---------|-------------|-------------------|
| `SpatialMovementTests` | `GetTileAtWorldPos` → asserts tile is not "Wall" | Walkable tiles must still report as walkable |
| `WallCollisionTests` | Teleport near walls, verify player stops | Wall tiles must still block movement at same positions |
| `TownTests` | Teleport to NPC positions | Town layout must keep NPCs at compatible positions |
| `WastelandTests` | Zone exit trigger at (-15.5, 0.5, 1.5) | West gate must remain at same world position |
| `GameWorldTests` | `GetTileAtWorldPos` → asserts "Ground" / "Road" | Tile type names in automation responses |
| `CombatTriggerTests` | Teleport near enemies | Enemy positions (separate from map, but layout matters) |
| `DeathRespawnUITests` | Zone transitions | Zone infrastructure must still work |

### 4.3 Migration Strategy

1. **Keep `TileType` as a derived property.** `TileData` has a `SurfaceType`, but `TileMapData.GetLegacyTileType(x, y)` maps it back:
   - `Dirt/Grass/Sand/Mud` → `"Ground"` 
   - `Asphalt/Concrete` → `"Road"`
   - `Rock` → `"Rubble"`
   - `Metal` → `"Wall"` (when structure present)
   - Water present → `"Water"`

2. **Keep map dimensions and coordinate math identical.** `TileToWorld` and `WorldToTile` formulas don't change. Tile size stays 1.0m. Chunk size stays 16×16.

3. **Keep town + wasteland builders producing equivalent layouts.** The builders switch to `TileData` internally but produce maps with the same walkable/wall pattern at the same positions. Gate exits stay at the same world coordinates.

4. **`TileInfoResponse` gains new fields but keeps old ones.** The `TileType` string field stays (mapped from legacy), plus new fields (`SurfaceType`, `HeightLevel`, `WaterLevel`).

5. **Run all 114 UI tests after migration.** No test file changes needed. The automation layer provides backward-compatible responses.

---

## 5. Revised Map Blueprint — Static World Only

Trimmed version of doc 03's schema with entities/quests removed:

```jsonc
{
  "$schema": "map-blueprint-v2",
  "name": "Sector 7 — Downtown Portland Ruins",
  "description": "...",
  "source": { "realWorldLocation": "...", "notes": "..." },
  "dimensions": { "chunksWide": 8, "chunksHigh": 6 },

  "terrain": {
    "baseElevation": 3,
    "regions": [ /* elevation, river, crater, lake, coastline */ ],
    "surfaces": [ /* surface type assignment per region */ ]
  },

  "water": {
    "rivers": [ /* path, width, depth, bridges */ ],
    "lakes": [ /* center, radius, depth */ ]
  },

  "roads": [ /* path, width, surfaceType, condition */ ],

  "buildings": [
    {
      "id": "bld_elder_house",
      "meshAsset": "buildings/elder_house",
      "position": { "chunkX": 3, "chunkY": 5, "localTileX": 4, "localTileY": 6 },
      "footprint": [[4,6],[5,6],[4,7],[5,7]],
      "floors": 1,
      "size": "Small",
      "condition": 0.8
    }
  ],

  "props": [ /* static decorative objects */ ],

  "zones": [
    {
      "id": "zone_downtown_east",
      "name": "East Side Ruins",
      "biome": "RuinedCity",
      "chunkRange": { "startX": 0, "startY": 0, "endX": 2, "endY": 5 },
      "ambientAudioId": "amb_ruined_city"
    }
  ]
}
```

Entity placement, quests, and loot are loaded from separate files that reference zone IDs and tile coordinates. This separation means:
- Changing enemy placement doesn't regenerate the map
- Changing the map terrain doesn't invalidate quest definitions (as long as zone IDs are stable)
- The LLM prompt for map generation is smaller and more focused

---

## 6. Decisions (Resolved)

| # | Question | Decision |
|---|----------|----------|
| 1 | Tile size: 1.0m or 2.0m? | **Keep 1.0m.** Precision matters more than the mobile draw-call saving. Optimize via chunk mesh batching instead. |
| 2 | Building interiors: in scope? | **Deferred.** Buildings are exterior-only for now. Footprint blocks walkability, mesh renders, player interacts at door. Interior chunks are a future phase. |
| 3 | Sub-tile mesh authoring? | **Meshy.ai.** Generate the 4 sub-tile shapes (Fill, Edge, OuterCorner, InnerCorner) per surface type via meshy.ai, same pipeline as buildings and props. |
| 4 | Water interaction? | **Visual only.** Water tiles remain non-walkable (same as today). No wading/swimming mechanics. Adds animated surface rendering but no pathfinding changes. |
| 5 | Real geography input format? | **Text description only.** Drop the `latitude`, `longitude`, `radiusKm` fields from the blueprint `source` block. The LLM prompt uses a plain-text description of the location and landmarks. |

---

## 7. Open Items (Remaining)

These still need decisions or are deferred for later phases:

1. **Shader pipeline.** Stride uses SDSL (custom shader language). Triplanar mapping and border splatting need custom shaders. This is doable but is separate work from the tile data model. Plan: Phase 1 builds the new data model with the existing colored-cube renderer, Phase 2 adds shaders.

2. **Save game interaction.** If the player destroys a tile or a building collapses, that's modified state that must persist in saves. The `ChunkData.ModifiedState` dictionary handles this, but it needs to expand from entity IDs to tile coordinates. Design this before implementing destructible terrain.

3. **Map editor.** Right now maps are code-built (`TownMapBuilder`). The new system loads from JSON. But iterating on map JSON by hand is painful. A simple in-game debug overlay that shows tile data + lets you paint tiles would accelerate testing. Not needed immediately, but worth planning the hook points.

4. **Fog of war / visibility.** Doc 01 proposed per-tile visibility states. This is pure rendering + game logic — no impact on the tile data model. Can be layered on later without changing `TileData`. Cheap to plan for now (reserve a field), expensive to retrofit.

5. **Audio zones.** Surface-type-driven footstep sounds are essentially free once the surface type is available per tile. Worth wiring early — even placeholder sounds improve feel dramatically.
