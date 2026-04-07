# Map & Terrain Completeness Review

**Status:** Draft  
**Milestone:** M1b  
**Purpose:** Gap analysis — everything needed for maps/terrain in the RPG, what's already designed, and what's missing.

---

## Coverage Matrix

| System | Status | Document |
|--------|--------|----------|
| Heightmap terrain mesh | ✅ Designed | heightmap-tilemap-hybrid.md |
| Texture splatting (8 surfaces) | ✅ Designed | heightmap-tilemap-hybrid.md |
| Tile overlay for towns | ✅ Designed | heightmap-tilemap-hybrid.md |
| Roads, rails, rivers (linear features) | ✅ Designed | heightmap-tilemap-hybrid.md |
| Terrain modifiers (flatten, crater) | ✅ Designed | heightmap-tilemap-hybrid.md |
| Water, lava, toxic, oil, ice, anomaly | ✅ Designed | special-surface-liquid-rendering.md |
| Waterfalls | ✅ Designed | special-surface-liquid-rendering.md |
| Three zoom levels | ✅ Designed | multi-scale-zoom-levels.md |
| Seamless camera transitions | ✅ Designed | multi-scale-zoom-levels.md |
| Vehicle travel | ✅ Designed | multi-scale-zoom-levels.md |
| Fog of war | ✅ Designed | multi-scale-zoom-levels.md |
| Chunk streaming | ✅ Implemented | ChunkStreamingProcessor.cs |
| Height / slope / LOS | ✅ Implemented | HeightHelper.cs |
| Water / shore detection | ✅ Implemented | WaterHelper.cs |
| **Underground / caves / multi-layer** | ❌ Missing | This document §1 |
| **Bridges & overpasses** | ❌ Missing | This document §2 |
| **Building interiors** | ❌ Missing | This document §3 |
| **Vegetation (trees)** | ❌ Missing | This document §4 |
| **Visual weather on terrain** | ❌ Missing | This document §5 |
| **Cover system (combat)** | ❌ Missing | This document §6 |
| **Scavenging / resource nodes** | ❌ Missing | This document §7 |
| **Height advantage in combat** | ❌ Missing | This document §8 |
| **Minimap & navigation HUD** | ❌ Missing | This document §9 |
| **Fast travel** | ❌ Missing | This document §10 |
| **Map persistence & state** | ❌ Missing | This document §11 |
| **Destructible terrain** | ❌ Missing | This document §12 |
| **Map generation pipeline** | ⚠️ Partial | MapGeneratorService exists; needs schema updates |

---

## §1 — Underground & Multi-Layer Maps

### Problem

The current design assumes a single heightmap surface. Post-apocalyptic settings need caves, sewers, metro tunnels, bunkers, basements, and multi-floor structures — all of which are **below** or **overlapping** the surface map.

### Proposal: Layer Stack

Each chunk can have **multiple vertical layers** in addition to its surface:

```csharp
public enum MapLayer : byte
{
    DeepUnderground,   // Caves, mines, deep bunkers
    Underground,       // Sewers, metro, basements
    Surface,           // Default — the heightmap
    Elevated           // Rooftops, overpasses (see §2)
}

public class LayeredChunkData
{
    public ChunkData Surface { get; }                          // Always present
    public Dictionary<MapLayer, ChunkData>? SubLayers { get; } // Optional extra layers
}
```

### Underground Rendering

Underground layers are **separate tilemaps** (Hybrid mode) that render when the player is in that layer. The surface heightmap is hidden or rendered as a ceiling with transparency/cutaway.

```
Surface heightmap ─────────────────────────  ← visible from above
                                              ← gap (soil/rock — not rendered)
Underground tilemap ───┬───┬───┬───┬───────  ← visible when player is underground
     ceiling           │   │   │   │
     floor  ───────────┴───┴───┴───┴───────
```

### Transition Points

Entrances connect layers:

```csharp
public record LayerTransition(
    Vector2Int TilePosition,
    MapLayer FromLayer,
    MapLayer ToLayer,
    TransitionType Type);     // Stairs, Ladder, Elevator, Hole, Cavemouth

public enum TransitionType : byte
{
    Stairs,
    Ladder,
    Elevator,
    Hatch,
    CaveMouth,
    Sewer grate,
    Collapsed floor
}
```

### Camera Behaviour

When the player descends underground:
1. Surface terrain fades out (alpha → 0 over 1s).
2. Underground layer fades in.
3. Camera lowers to underground ceiling height.
4. A subtle vignette or colour grade signals underground.

When near a cave mouth / open entrance, the underground is visible from the surface (cutaway rendering).

### Generation

LLM/procedural generator creates underground layers as additional `ChunkData` with `MapLayer` tag. NOT every chunk needs underground — only where designed.

---

## §2 — Bridges & Overpasses

### Problem

Roads can cross rivers, railways can bridge valleys, collapsed highways span gaps. The current `LinearFeature` drapes onto the heightmap — it can't **float above** terrain.

### Proposal: Elevated Linear Segments

Add elevation data to `LinearFeature` control points:

```csharp
public record LinearFeatureNode(
    Vector2 Position,          // XZ world position
    float? OverrideHeight);    // If set: absolute Y height (bridge deck)
                                // If null: drape onto heightmap (default)
```

When a segment has `OverrideHeight` on its nodes, it renders at that fixed Y instead of sampling the heightmap. The terrain below is **not** modified.

### Bridge Components

| Component | Mesh |
|-----------|------|
| Deck | Flat ribbon at `OverrideHeight` — road/rail surface texture |
| Supports | Vertical pillars from deck down to heightmap surface |
| Railings | Instanced railing meshes along deck edges |
| Underside | Dark flat quad (shadow/underside texture) |

```
   ─── railing ───╔══════════════════╗─── railing ───
                   ║   bridge deck    ║
         pillar ─→ ║                  ║ ←─ pillar
                   ╚══════════════════╝
   ──── river ─────────────────────────── river ────
   ═════ terrain ═══════════════════════ terrain ═══
```

### Collapsed Bridges

A bridge can be flagged as `collapsed`:
- Deck mesh uses a ruined variant (broken concrete, rebar).
- Deck section mid-span drops to terrain height (rubble pile).
- Impassable by vehicles; traversable on foot with climbing.

### Pathfinding

Bridge tiles exist on the `Elevated` layer. Pathfinding considers them as walkable at their deck height. Entities on the surface below are not blocked by the bridge (different layer).

---

## §3 — Building Interiors

### Problem

At Level 1, buildings are structure meshes on the tile overlay. When the player enters, we need to see inside. Decision: **separate interior maps** loaded as distinct scenes.

### Proposal: Interior as Linked Map

```csharp
public record BuildingInterior(
    string InteriorId,            // Unique id, maps to an interior ChunkData set
    Vector2Int EntryTile,         // Tile on the surface map where door is
    MapLayer InteriorLayer,       // Usually Underground or Surface (ground floor)
    int FloorCount,               // Multi-story
    Vector2Int InteriorSize);     // Tilemap dimensions per floor
```

### Transition Flow

```
1. Player interacts with door tile on surface map.
2. Screen does a brief iris/fade transition (not a loading screen — brief visual break).
3. Surface chunks unload; interior ChunkData loads.
4. Camera repositions inside the interior.
5. Interior renders as Hybrid tilemap (floor, walls, furniture, rooms).
6. Player exits via door → reverse transition back to surface.
```

### Interior Rendering

- Each floor is a Hybrid tilemap.
- Walls are full-height tile structures (not just overlay decals).
- Ceiling is either not rendered (top-down camera sees floor plan) or rendered with partial transparency if the camera angle is shallow.
- Stairs/elevators connect floors using the same `LayerTransition` system from §1.

### Large vs Small Buildings

| Building Size | Approach |
|--------------|----------|
| Small (1–4 rooms) | Single-floor interior, loads instantly |
| Medium (houses, shops) | 1–2 floor interior |
| Large (malls, hospitals, bunkers) | Multi-floor, may stream floors like chunks |

---

## §4 — Vegetation (Trees)

### Problem

Forests, parks, overgrown ruins need trees. No grass detail — trees only, rendered as entities at Level 1 and canopy clusters at Level 2.

### Proposal: Tree Placement via Entity Spawns

Trees are stored in `ChunkData.EntitySpawns` with a `TreeData` component:

```csharp
public record TreeSpawn(
    Vector2 Position,           // XZ within chunk
    TreeSpecies Species,        // DeadOak, CharredPine, Mutant willow, etc.
    byte GrowthStage,           // 0 = sapling, 255 = full grown
    bool IsDead);               // Standing dead trunk (no canopy)

public enum TreeSpecies : byte
{
    DeadOak,
    CharredPine,
    MutantWillow,
    RustedMetal,       // Weird post-apocalyptic "iron trees"
    Scrub,             // Low desert bush
    Palm,
    Birch
}
```

### Rendering

| Level | Representation |
|-------|---------------|
| L1 near | Full tree mesh (trunk + canopy), shadow casting |
| L1 far | Billboard sprite (camera-facing quad) |
| L2 | Canopy cluster meshes (one mesh per ~20 trees in an area) |
| L3 | Forest biome colour in splat map |

### Tree LOD Transition

At Level 1, trees beyond ~50 m from camera switch from mesh to billboard. During L1→L2 transition, individual billboards fade and canopy clusters fade in. This is handled by the same crossfade system from the zoom-level design.

### Forest Density

Tiles with `SurfaceType.Grass` or `Dirt` may have `TileFlags` extended with a `Forested` flag. Tree spawn density is driven by the generator:

| Density | Trees per tile | Visual |
|---------|---------------|--------|
| Sparse | 0–1 | Occasional tree, visible ground |
| Medium | 1–3 | Scattered forest, broken canopy |
| Dense | 3–6 | Full canopy, ground in shadow |

---

## §5 — Visual Weather Effects on Terrain

### Problem

Rain, snow, dust storms should be visible on the terrain to sell the atmosphere. Terrain data does **not change** — this is purely visual.

### Proposal: Weather Overlay Shader

A global weather state drives shader parameters on the terrain material:

```csharp
public record WeatherState(
    WeatherType Type,       // Clear, Rain, Snow, DustStorm, Fog, AcidRain
    float Intensity,        // 0.0–1.0
    Vector2 WindDirection,
    float Temperature);
```

### Terrain Visual Effects

| Weather | Terrain Shader Effect |
|---------|----------------------|
| Rain | Wet darkening on surfaces (albedo × 0.7), specular increase, puddle reflections in low areas |
| Snow | White overlay blended by `Intensity` on upward-facing surfaces (dot(normal, up) > 0.7) |
| Dust storm | Sand-colour tint, reduced visibility (fog), wind-streak particle overlay |
| Fog | Distance fog colour/density adjusted; no terrain change |
| Acid rain | Green-tinted rain streaks, wet surface like rain but with faint green emissive |

### Implementation

A single `WeatherOverlay` compute pass modifies the splat map blending at the shader level (not the actual splat texture). On Low quality, only fog distance changes — no surface wetness/snow.

---

## §6 — Cover System (Combat)

### Problem

Tactical turn-based combat needs cover. Walls, wrecked cars, rubble piles should provide directional protection from ranged attacks.

### Proposal: Cover Data per Tile Edge

Cover is defined on **tile edges**, not centres — a wall on the north side of a tile provides cover from the north.

```csharp
[Flags]
public enum CoverEdges : byte
{
    None  = 0,
    North = 1,
    East  = 2,
    South = 4,
    West  = 8
}

public enum CoverLevel : byte
{
    None,       // No cover
    Half,       // Low wall, debris (+25% defence)
    Full        // High wall, solid vehicle (+50% defence, blocks LOS to body)
}
```

### Data Storage

Add to `TileData`:

```csharp
public readonly record struct TileData(
    ...,
    CoverEdges HalfCover,    // Edges with half cover
    CoverEdges FullCover);   // Edges with full cover
```

An edge can be half, full, or none. If both `HalfCover` and `FullCover` have the same edge set, full takes precedence.

### Integration

- **LOS check:** `HeightHelper.HasLineOfSight()` already traces tiles — extend to check `FullCover` edges along the ray.
- **Combat modifier:** When an entity is attacked, check which edges of their tile face the attacker direction. Apply defence bonus.
- **Visual:** Cover objects (walls, cars) are placed by structure meshes in the tile overlay — cover data is the *logical* complement.

### Destructible Cover

Cover can degrade: `FullCover → HalfCover → None` when taking enough damage. This ties into §12 (destructible terrain) if implemented.

---

## §7 — Scavenging & Resource Nodes

### Problem

Post-apocalyptic exploration revolves around scavenging. The map needs searchable objects, loot containers, and gathering points.

### Proposal: Scavenge Points

```csharp
public record ScavengeNode(
    Vector2Int TilePosition,
    ScavengeType Type,
    LootTableId LootTable,
    byte SearchDifficulty,        // 0–255, higher = harder / needs tools
    ScavengeState State);         // Unsearched, Partial, Depleted

public enum ScavengeType : byte
{
    Container,         // Cabinet, chest, trunk
    Vehicle,           // Wrecked car, truck
    Rubble,            // Dig through debris (slow)
    Corpse,            // Body search (may be trapped)
    Terminal,          // Hackable electronics
    WaterSource,       // Collect water
    FuelReservoir,     // Siphon fuel
    Vegetation,        // Edible plants, herbs
    Deposit            // Metal scrap, chemical cache
}
```

### Map Representation

- Scavenge nodes are entity spawns in `ChunkData`, rendered as props (crate, wrecked car mesh, rubble pile).
- At Level 2, concentrated scavenge areas show as an icon (supply depot, junkyard marker).
- `SearchDifficulty` gates loot behind tools (crowbar for containers, hacking kit for terminals, fuel siphon for reservoirs).

### Generation

The LLM/procedural generator places scavenge nodes based on location type:
- Urban ruins → vehicles, containers, rubble, terminals
- Wilderness → vegetation, water sources, deposits
- Military → high-value containers (locked), fuel

---

## §8 — Height Advantage in Combat

### Problem

Rooftops, elevated positions, and bridges should give combat bonuses. The height system exists but isn't connected to combat modifiers.

### Proposal: Height Differential Combat Modifiers

```csharp
public static class CombatHeightBonus
{
    public static float GetAccuracyModifier(int attackerHeight, int defenderHeight)
    {
        int delta = attackerHeight - defenderHeight;
        return delta switch
        {
            >= 4 => 1.20f,   // Significant high ground: +20% accuracy
            >= 2 => 1.10f,   // Moderate advantage: +10%
            >= -1 => 1.00f,  // Same level or slight low: no change
            >= -3 => 0.90f,  // Shooting uphill: -10%
            _ => 0.80f       // Far below: -20%
        };
    }

    public static float GetDamageModifier(int attackerHeight, int defenderHeight)
    {
        int delta = attackerHeight - defenderHeight;
        if (delta >= 3) return 1.15f;  // Plunging fire: +15% damage
        return 1.00f;
    }
}
```

### Integration

- Attacker/defender height read from `TileData.HeightLevel`.
- Bridges/elevated structures use their deck height.
- Underground layers have their own height within the layer.
- Height advantage stacks with cover — high ground behind full cover is a strong position.
- UI shows height indicator arrows (↑ advantage, ↓ disadvantage) on the targeting reticle.

---

## §9 — Minimap & Navigation HUD

### Proposal: Dual Navigation

**Corner minimap** — always visible:
- Circular rotating minimap in the bottom-right.
- Terrain rendered as a simplified top-down view using the splat map colours.
- Icons for: party members, enemies (if detected), quest markers, POIs, scavenge nodes.
- Fog of war respected — unexplored areas dark.
- Zoom level of minimap adjusts with the game zoom level.

**Zoom-out as full map:**
- Scrolling the camera out to Level 2/3 IS the full map.
- At Level 2/3, the UI overlays route planning, POI details, quest tracker.

### Minimap Data Source

The minimap renders from `WorldLodCache` (already designed in multi-scale-zoom-levels.md). At Level 1 it shows a small radius of the Level 1 splat map. At Level 2 it shows the biome map.

### Player Markers

```csharp
public record MapMarker(
    Vector2 WorldPosition,
    MapMarkerType Type,
    string? Label,
    Color4 Color);

public enum MapMarkerType : byte
{
    QuestObjective,
    QuestOptional,
    PartyMember,
    Enemy,
    Vendor,
    FastTravel,
    CustomPin,       // Player-placed
    Danger
}
```

Players can place custom pins at any zoom level (persisted in save).

---

## §10 — Fast Travel

### Proposal

Fast travel between **discovered** locations that have a `FastTravel` tag:

```csharp
public record FastTravelPoint(
    string PointId,
    string DisplayName,
    Vector2 WorldPosition,
    ZoomLevel MinLevel,         // Level at which the point exists
    bool RequiresVehicle,       // Some remote points need a vehicle
    bool Discovered);
```

### Rules

| Rule | Detail |
|------|--------|
| Must be discovered first | Visit the POI to unlock |
| Time passes | Game time advances proportional to travel distance at L2/L3 time scales |
| Fuel consumed | If party has a vehicle, fuel is deducted based on route distance |
| Random encounter chance | Roll for encounters during fast travel — interrupted if triggered |
| Not available in combat or dungeons | Can only fast travel from "safe" locations |
| Exit point | Player appears at the `FastTravelPoint` tile, camera at Level 1 |

### UI

At Level 2/3, discovered fast-travel points show a special icon. Clicking one opens a confirmation dialog showing: travel time, fuel cost, encounter risk rating.

---

## §11 — Map Persistence & State

### Problem

When the player leaves an area and returns, what is remembered? This affects save file size and world believability.

### Proposal: Tiered Persistence

Not all map state matters equally. Use three tiers:

| Tier | What | Persistence | Storage |
|------|------|-------------|---------|
| **Permanent** | Quest-critical state (doors opened, bosses killed, key items looted) | Always saved | Small — flag list per chunk |
| **Session** | Enemy kills, container loot, cover destruction | Saved during session, resets on game reload | Medium — chunk delta |
| **Transient** | Particle effects, weather puddles, bullet holes | Never saved, regenerated | None |

### Implementation

```csharp
public class ChunkPersistenceState
{
    public HashSet<string> PermanentFlags { get; }        // "boss_dead", "gate_opened"
    public Dictionary<Vector2Int, TileData> TileOverrides { get; }  // Modified tiles
    public HashSet<int> DepletedScavengeNodes { get; }    // Looted nodes
    public HashSet<int> DestroyedEntities { get; }        // Killed enemies, broken props
}
```

### Save File Impact

Only chunks the player has visited and modified get persistence entries. Unvisited chunks have zero save cost. A typical playthrough might modify ~200 chunks → ~50 KB of persistence data.

### Respawn Option (If Desired Later)

Non-permanent entities could respawn after N game-days (configurable per area). This keeps the world feeling populated on return visits without losing quest progress.

---

## §12 — Destructible Terrain (Proposal — Undecided)

Since this is undecided, here are two options:

### Option A: Limited Scripted Destruction

- Certain objects are flagged `TileFlags.Destructible`.
- When destroyed, they swap to a "destroyed" mesh variant (wall → rubble, car → wreck).
- `CoverEdges` degrade (`Full → Half → None`).
- Terrain height does NOT change — craters from the heightmap design are the only terrain deformation.
- **Pro:** Simple, predictable, low save cost.
- **Con:** Less emergent gameplay.

### Option B: Freeform Destruction

- Any wall/structure can be destroyed given enough damage.
- Destroyed tiles update `TileData.Surface`, `TileFlags`, and `CoverEdges`.
- Buildings can partially collapse (remove upper tiles, add rubble at base).
- Chain reactions: explosions near fuel/oil cause fire propagation (already designed in liquid rendering).
- **Pro:** Highly emergent, satisfying tactical play.
- **Con:** More complex, larger save deltas, harder to balance.

### Recommendation

**Start with Option A** (safe, shippable) with the data model supporting Option B. The `TileFlags.Destructible` flag and `CoverEdges` degradation work in both options. The only extra work for Option B is the damage propagation and mesh-swap system.

---

## Summary of New Data Model Changes

All proposed additions to existing types:

```csharp
// ChunkData additions
public ChunkData
{
    // Existing...
    public MapLayer Layer { get; init; }                           // §1
    public List<LayerTransition> Transitions { get; }              // §1
    public List<ScavengeNode> ScavengeNodes { get; }               // §7
    public ChunkPersistenceState? Persistence { get; set; }        // §11
}

// TileData additions
public readonly record struct TileData(
    SurfaceType Surface,
    byte HeightLevel,
    byte WaterLevel,
    LiquidType Liquid,           // From special-surface-liquid-rendering.md
    int StructureId,
    TileFlags Flags,
    byte VariantSeed,
    CoverEdges HalfCover,       // §6
    CoverEdges FullCover);      // §6

// TileFlags additions
[Flags]
public enum TileFlags : ushort   // Expanded from byte to ushort
{
    Walkable      = 1,
    Irradiated    = 2,
    Burnable      = 4,
    Destructible  = 8,
    Forested      = 16,          // §4
    Interior      = 32,          // §3
    FastTravel    = 64,          // §10
    Searchable    = 128          // §7
}

// LinearFeatureNode updated (§2)
public record LinearFeatureNode(
    Vector2 Position,
    float? OverrideHeight);     // Bridge/overpass support

// WorldMapData additions
public WorldMapData
{
    // Existing...
    public List<FastTravelPoint> FastTravelPoints { get; }         // §10
    public WeatherState CurrentWeather { get; set; }               // §5
    public List<MapMarker> PlayerMarkers { get; }                  // §9
}
```

---

## Generation Pipeline Updates

The `MapGeneratorService` LLM prompt schema needs to support:

| Feature | Schema Addition |
|---------|----------------|
| Underground layers | `layers[]` array per chunk |
| Bridges | `override_height` on linear feature nodes |
| Interiors | `interior` object on buildings |
| Trees | `tree_spawns[]` per chunk |
| Scavenge nodes | `scavenge_nodes[]` per chunk |
| Cover | `half_cover` / `full_cover` edge flags per tile |
| Fast travel points | `fast_travel` flag on POIs |

---

## Recommended Implementation Order

| Priority | Feature | Reason |
|----------|---------|--------|
| 1 | Vegetation (§4) | Forests are visible at all zoom levels; large visual impact |
| 2 | Cover system (§6) | Required for combat — core gameplay |
| 3 | Height advantage (§8) | Small addition on top of existing HeightHelper |
| 4 | Scavenging (§7) | Core exploration loop |
| 5 | Building interiors (§3) | Needed for dungeon/town gameplay |
| 6 | Underground (§1) | Extends §3 concept; needed for caves/sewers |
| 7 | Bridges (§2) | Visual/exploration feature |
| 8 | Minimap (§9) | QoL — can use zoom-out as substitute early on |
| 9 | Fast travel (§10) | QoL — not needed until world is large |
| 10 | Visual weather (§5) | Polish — atmosphere only |
| 11 | Map persistence (§11) | Needed when save/load is implemented |
| 12 | Destructible terrain (§12) | Start with Option A when combat ships |

---

## Acceptance Criteria

1. Underground layers load and render, with transition points connecting to the surface.
2. Bridges render as elevated deck + pillars; pathfinding treats them as separate elevated walkable area.
3. Entering a building loads the interior map with correct door-tile connection.
4. Trees render as meshes at Level 1, billboards at distance, canopy clusters at Level 2.
5. Weather overlays (rain wetness, snow accumulation) are visible on terrain without changing tile data.
6. Cover edges are exposed per tile; combat system applies directional defence bonuses.
7. Scavenge nodes are interactive; loot tables resolve correctly; depleted state persists.
8. Height advantage modifiers apply during ranged combat.
9. Corner minimap displays terrain, icons, and fog of war at all zoom levels.
10. Fast travel deducts time and fuel, with encounter interruption chance.
11. Map persistence saves quest-critical state permanently, session state until reload.
12. Destructible objects swap to ruined variants and degrade cover.
