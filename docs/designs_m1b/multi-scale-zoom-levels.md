# Multi-Scale World & Zoom Levels

**Status:** Draft  
**Milestone:** M1b  
**Depends on:** Heightmap–Tilemap Hybrid (heightmap-tilemap-hybrid.md), Special Surface Rendering (special-surface-liquid-rendering.md)

---

## Summary

The game world exists at four zoom levels. Levels 1–3 use seamless camera transitions — no loading screens — by LODing terrain, entities, and UI as the camera pulls in or out. Level 4 (globe) is a separate navigation screen accessed from travel hubs. Each level has its own rendering strategy, entity representation, and time scale.

| Level | Name | Scale | Camera Alt. | Rendering | Time |
|-------|------|-------|-------------|-----------|------|
| **1** | Local | Town / dungeon / building | ~10–30 m | Tilemap + heightmap (Hybrid) | 1× real-time |
| **2** | Regional | ~20–100 km area | ~200–800 m | Heightmap only | ~60× (1 min real ≈ 1 hr game) |
| **3** | Continental | Country / continent | ~2 000–10 000 m | Heightmap only (strategic) | ~1 440× (1 min real ≈ 1 day game) |
| **4** | Globe | Entire planet | N/A (UI scene) | 3D sphere + continent texture | N/A (menu) |

---

## Design Principles

1. **One `WorldMapData`** — all four levels share the same underlying data; zoom level controls which layers are visible and at what detail.
2. **Seamless transitions (L1–L3)** — camera altitude drives the LOD; no fade-to-black.
3. **Globe is a menu (L4)** — accessed from travel hubs (airports, ports); not a seamless zoom; uses cutscene transition.
4. **Combat is Level 1 only** — encounters at Level 2/3 pull the camera down to a Level 1 combat arena.
5. **Time accelerates with scale** — at higher zoom levels game time passes faster so travel doesn't bore the player.
6. **Vehicles are real** — the party can walk or drive at Level 2; vehicles have fuel, speed, and cargo mechanics.

---

## Data Model

### World Hierarchy

```
WorldMapData                          ← one per save
├── PlanetData                        ← Level 4 globe (sphere mesh + continent outlines)
├── ContinentMapData[]                ← Level 3 regions
│   ├── RegionMapData[]               ← Level 2 areas
│   │   ├── ChunkData[] (16×16)      ← Level 1 tile/heightmap chunks
│   │   ├── LinearFeature[]           ← roads, rivers, rails
│   │   └── PointOfInterest[]         ← towns, dungeons, landmarks
│   └── ContinentFeature[]            ← major rivers, borders, trade routes
└── GlobalState                        ← time, weather, factions
```

### Zoom-Level Tag on Data

```csharp
public enum ZoomLevel : byte
{
    Local = 1,       // Town/dungeon — tilemap + heightmap
    Regional = 2,    // Road map — heightmap only
    Continental = 3, // Strategic — heightmap only
    Globe = 4        // Planet view — navigation menu
}
```

Chunks already carry `ChunkMode` (Heightmap / Hybrid). Zoom level is **not** stored per chunk — it's a camera/viewport state. But some data only exists at certain levels:

| Data | Level 1 | Level 2 | Level 3 | Level 4 |
|------|---------|---------|---------|---------|
| `TileData` grid (full) | ✓ | — | — | — |
| Height samples (coarse) | — | ✓ | ✓ | — |
| `LinearFeature` splines | ✓ | ✓ | ✓ (simplified) | — |
| Individual entities (NPCs, props) | ✓ | — | — | — |
| `PointOfInterest` markers | — | ✓ | ✓ | — |
| Building footprints | ✓ | — | — | — |
| Town silhouette meshes | — | ✓ | — | — |
| Vehicle entities | ✓ (parked) | ✓ (driving) | — | — |
| Party icon | — | — | ✓ | — |
| Continent outlines on sphere | — | — | — | ✓ |
| City dots (major only) | — | — | — | ✓ |
| Faction territory colours | — | — | ✓ | ✓ |

---

## Level 1 — Local (Towns & Dungeons)

### Rendering

Uses the **Hybrid mode** from the heightmap–tilemap design:
- Heightmap mesh for terrain
- Tile overlay for floors, walls, structures
- Individual entity rendering (NPCs, trees, props, crates, furniture)
- Full liquid rendering (puddles, lava, etc.)

### Camera

- Isometric 45° pitch (existing)
- Altitude: 10–30 m above ground
- Rotation: 4-directional snap (N/E/S/W) or free rotation

### Entities

Full detail: animated character models, individual trees with canopy, readable signs, lootable containers. Party members are separate on-screen characters.

### Time Scale

**1× real-time.** 1 minute real = 1 minute game. All combat, dialogue, and exploration at normal pace.

---

## Level 2 — Regional (Road Map)

### Rendering

**Heightmap only** — no tile overlay:
- Terrain mesh at lower subdivision (larger tiles, ~50–100m per height sample)
- Texture splatting for biomes (forest, desert, grassland, urban, wasteland)
- Forests rendered as **canopy clusters** — a handful of instanced tree-clump meshes covering an area, not individual trees
- Towns rendered as **silhouette meshes** — low-poly building clusters sitting on the heightmap, visible from distance
- Roads, rivers, rails as `LinearFeature` splines (thicker lines at this scale)
- Points of interest: icon markers with labels (town names, dungeon entrances, landmarks)

### Biome Splatting

At Level 2 the splat map encodes biomes instead of individual surface types:

| Channel | Splat0 | Splat1 |
|---------|--------|--------|
| R | Grassland | Urban ruins |
| G | Forest | Wasteland |
| B | Desert / arid | Snow / tundra |
| A | Water (lake/ocean) | Mountain rock |

### Camera

- Higher pitch (~50–60°), looking more top-down
- Altitude: 200–800 m
- Free rotation, smooth zoom

### Entities

Most entities **not rendered** — only:
- The **party** (as a vehicle model if driving, or a walking-group sprite if on foot)
- Other **vehicle traffic** on roads (NPC convoys, traders)
- Major roaming threats (bandit camps, mutant herds) as **icon markers** with threat-radius circles

### Vehicle Mechanics

| Property | Effect |
|----------|--------|
| Speed | km/h — determines travel time between points |
| Fuel | Consumed per km; refuelled at settlements |
| Cargo capacity | Limits carried loot/supplies |
| Durability | Degrades on rough terrain; breaks down at 0 |
| Off-road penalty | Speed ×0.5 off paved roads |
| Type | Car, truck, motorcycle, on-foot — each has different stats |

The party can travel **on foot** at Level 2, but it's slow (5 km/h vs 60 km/h by car). Walking drains food/water supplies over time.

### Time Scale

**~60× acceleration.** 1 minute real ≈ 1 hour game time. A 60 km drive at 60 km/h takes ~1 minute real-time. Day/night cycle visibly advances. Weather changes are noticeable.

### Encounters

Random encounters trigger while travelling at Level 2. When one fires:

1. Camera smoothly zooms to Level 1 at the encounter location.
2. A small Level 1 arena is generated or loaded (roadside ambush, checkpoint, breakdown).
3. Combat and interaction play out at Level 1.
4. On resolution, camera pulls back to Level 2; party resumes travel.

---

## Level 3 — Continental (Strategic Map)

### Rendering

**Heightmap only**, very coarse:
- Terrain mesh at ~500 m–1 km per height sample
- Biome splatting at continent scale (large colour regions)
- Major geographic features only: mountain ranges, coastlines, major rivers, deserts
- No individual trees, buildings, or small features
- Towns/cities as **labelled dots** with size/faction indicators
- Major roads as **thick lines** connecting cities
- Borders between faction territories rendered as tinted overlays

### Camera

- Near-vertical (~70–80° pitch), map-like view
- Altitude: 2 000–10 000 m
- Can pan across the continent freely

### Entities

- Party shown as a **single icon** (faction emblem or vehicle silhouette) with a trail line showing recent path
- No individual NPCs, vehicles, or threats visible
- **Faction territory** overlays with colour tinting
- Event markers (war fronts, disasters, quest markers)

### Gameplay

Level 3 is a **strategic/planning map**:
- Select a destination city/landmark → route auto-calculated along roads
- Travel happens semi-automatically — time bar shows progress
- Random events interrupt travel (shown as pop-up encounter cards)
- Encounters that require combat pull camera to Level 1

### Time Scale

**~1 440× acceleration.** 1 minute real ≈ 1 day game time. Cross-continent travel (1 000 km) at road speed takes a few minutes real-time. Seasons can visibly change on long journeys.

---

## Seamless Zoom Transitions

The camera zoom is continuous. As altitude changes, rendering layers crossfade.

### Transition Zones

```
Alt (m)     Level active    Crossfade
──────────────────────────────────────
0–30        Level 1 (100%)
30–50       L1 fading out ↔ L2 fading in
50–200      Level 2 (100%)
200–400     Level 2 (100%)
400–600     L2 fading out ↔ L3 fading in
600–2000    Level 3 (100%)
```

### What Crossfades

| Transition | Fading Out | Fading In |
|------------|-----------|-----------|
| L1 → L2 | Individual entities, tile overlay, detailed terrain | Silhouette meshes, biome splat, POI markers |
| L2 → L3 | Silhouette meshes, vehicles, fine roads | Territory overlays, city dots, thick route lines |

### Transition Steps (L1 → L2 Example)

1. **Camera begins pulling out** (player scrolls zoom or triggers travel).
2. At 30 m altitude: individual NPCs start fading (alpha → 0 over 20 m range).
3. At 35 m: tile overlay decals fade; heightmap splat transitions from surface-type to biome.
4. At 40 m: individual trees replaced by canopy cluster LODs.
5. At 45 m: buildings replaced by town silhouette mesh (pre-computed from footprints).
6. At 50 m: transition complete — Level 2 rendering active.

Reverse for zooming in. The key is that **both levels briefly render simultaneously** during the crossfade, blended by alpha. The heightmap mesh is shared (just different subdivision/splat), so there's no terrain pop.

### Terrain LOD During Transition

```
Level 1 chunks (16×16, 1m tiles)
    ↓ merge 4×4 chunks → Level 2 tile (64×64m region)
        ↓ merge 16×16 L2 tiles → Level 3 tile (~1km region)
```

The heightmap mesh dynamically adjusts vertex density based on camera altitude. Distant Level 1 chunks that are visible during the L1→L2 transition use their Medium/Low quality subdivision, then swap to the coarser Level 2 mesh.

---

## Terrain Data at Each Scale

### Level 1 — Full Resolution

Existing `TileData[16,16]` per chunk, 1 tile = 1m (or chosen scale). Full heightmap mesh as per hybrid design.

### Level 2 — Sampled from Level 1

Level 2 height data is **derived** from Level 1, not stored separately:

```csharp
public float GetLevel2Height(int regionX, int regionY)
{
    // Each Level 2 cell covers N×N Level 1 chunks
    // Average the height of all tiles in that area
    // Cache the result for the session
}
```

Biome type is determined by the **dominant surface type** in the covered Level 1 chunks. If 60% of tiles are `Grass` → biome is Grassland. Thresholds:

| Dominant Surface | Biome |
|-----------------|-------|
| Grass | Grassland |
| Sand | Desert |
| Rock (+ high height) | Mountain |
| Mud + Water | Swamp |
| Concrete + Asphalt | Urban |
| Dirt (+ radiation flags) | Wasteland |
| Grass + many trees (StructureId) | Forest |

### Level 3 — Sampled from Level 2

Same principle — Level 3 cells average over Level 2 regions. Biome is the dominant biome of covered Level 2 cells.

### Pre-computation

On world generation or save load, Level 2 and Level 3 data is **pre-computed and cached**:

```csharp
public class WorldLodCache
{
    public RegionHeightMap Level2Heights { get; }  // float[,]
    public BiomeMap Level2Biomes { get; }          // BiomeType[,]
    public ContinentHeightMap Level3Heights { get; }
    public BiomeMap Level3Biomes { get; }

    public void Rebuild(WorldMapData world) { /* sample + cache */ }
    public void InvalidateRegion(int regionX, int regionY) { /* partial rebuild */ }
}
```

---

## Points of Interest

POIs bridge the zoom levels — they're visible as markers at Level 2/3 and expand to full detail at Level 1.

```csharp
public record PointOfInterest(
    string Name,
    PoiType Type,               // Town, Dungeon, Landmark, Camp, Ruin, etc.
    Vector2 WorldPosition,      // Level 2 coordinates
    int Level1ChunkIndex,       // Which chunk(s) contain the full Level 1 data
    FactionId? ControllingFaction,
    PoiSize Size,               // Hamlet, Village, Town, City
    bool Discovered);           // Fog-of-war
```

| POI at Level 2 | Visual |
|----------------|--------|
| Town / City | Silhouette mesh + label + faction colour ring |
| Dungeon | Skull icon + label |
| Landmark | Diamond icon + label |
| Camp | Tent icon (small) |
| Ruin | Broken building icon |

At Level 3, only Towns/Cities of `PoiSize.Town` or larger are visible as dots.

---

## Day/Night and Weather Across Levels

Time acceleration means visual cycles are faster at higher zoom:

| Level | Day/Night Cycle | Weather |
|-------|----------------|---------|
| 1 | Natural — sun moves across sky over real-time minutes | Real-time rain, fog, dust storms |
| 2 | Accelerated — full day in ~1 min real | Weather zones visible as cloud shadows sweeping across the map |
| 3 | Fast — seasons shift visibly | Biome tinting changes (green → brown → white for seasons) |

The lighting system interpolates sun position based on `gameTime × timeScale`. Ambient light colour shifts smoothly through the day cycle regardless of speed.

---

## Fog of War

| Level | Fog Behaviour |
|-------|--------------|
| 1 | Line-of-sight per character (existing `HeightHelper.HasLineOfSight`) |
| 2 | Circular reveal around party position (~5 km radius); unexplored areas greyed |
| 3 | Regions revealed when any POI in them is discovered; unknown regions fully dark |

Fog state persists across zoom transitions — area revealed at Level 1 stays revealed at Level 2/3.

---

## Level 4 — Globe (Planet Navigation)

### Purpose

Level 4 is **not a gameplay zoom level** — it's a navigation menu for inter-continent travel. The player accesses it from travel hubs (airports, seaports, radio towers) or the pause menu once unlocked.

### Rendering

A 3D sphere with a procedurally generated planet texture:

| Component | Implementation |
|-----------|---------------|
| Sphere mesh | Standard UV sphere, ~64 segments — trivial for Stride |
| Continent shapes | Projected from Level 3 continent outlines onto sphere surface |
| Ocean | Base sphere colour (dark blue) with subtle animated normal map |
| Biome colouring | Continent areas tinted by dominant biome (green, tan, white for ice caps) |
| Clouds | Semi-transparent cloud layer sphere slightly larger than planet, slow rotation |
| Atmosphere | Rim-glow shader (fresnel) for atmospheric haze at planet edge |
| City lights | On the night side: emissive dots for major cities (PoiSize.City) |

### Camera

- Fixed orbital camera, player can rotate the globe by dragging.
- Scroll wheel zooms in slightly (but never transitions to Level 3 — that's a separate action).
- Camera auto-rotates slowly when idle.

### UI Overlay

- Discovered continents: labelled, clickable, lit.
- Undiscovered continents: dark silhouette, name hidden, not clickable.
- Hovering a continent shows: name, dominant faction, danger level, travel cost/time.
- Clicking a discovered continent → confirmation dialog:

```
┌──────────────────────────────────────┐
│  Travel to: Eurasia Wastes           │
│  Distance: ~8,200 km                 │
│  Travel time: 3 days (game time)     │
│  Fuel cost: 120 units                │
│  Requires: Aircraft / Sea vessel     │
│                                      │
│  [Confirm]              [Cancel]     │
└──────────────────────────────────────┘
```

- On confirm: cutscene (aircraft taking off / ship leaving port) → screen fades → arrive at Level 3 of destination continent.

### Planet Data

```csharp
public record PlanetData(
    string Name,
    float Radius,                              // km
    int Seed,
    IReadOnlyList<ContinentOutline> Continents);

public record ContinentOutline(
    int ContinentId,
    string Name,
    IReadOnlyList<Vector2> BoundaryPolygon,    // Lat/lon points defining shape
    BiomeType DominantBiome,
    FactionId? DominantFaction);
```

The globe texture is generated once at world creation:
1. Project continent boundary polygons onto an equirectangular texture.
2. Fill continent interiors with biome colours.
3. Fill ocean.
4. Add ice caps at poles based on world seed.
5. Store as a single texture in `world.db` (`planet_texture` BLOB).

Regenerating the texture is cheap (~100 ms) so it can also be rebuilt when new continents are discovered.

### Travel Hubs

The globe is only accessible from specific locations or once the player acquires certain capabilities:

| Hub Type | Requirements | Destinations |
|----------|-------------|-------------|
| Airport | Functional aircraft or ticket | Any discovered continent with airport |
| Seaport | Functional ship or ticket | Coastal continents |
| Radio tower | Working radio + faction contact | Request pickup — slower, random delay |
| Unlocked menu | Late-game perk / technology | Any discovered continent |

### Fog of War on Globe

- Continents start as **dark silhouettes** — shape is visible but name and details are hidden.
- Discovering any POI on a continent reveals the continent (name, biome, faction).
- The starting continent is always revealed.

---

## Party Representation Summary

| Level | On Foot | In Vehicle |
|-------|---------|------------|
| 1 | Individual character models | Vehicle model (parked or slow-driving) + dismounted characters |
| 2 | Walking-group sprite (slow, ~5 km/h) | Vehicle model driving on road/terrain |
| 3 | Faction icon at location | Faction icon moving along route |
| 4 | N/A (menu — party not visible) | N/A |

---

## Performance Considerations

| Level | Active Terrain | Entity Count | Draw Calls |
|-------|---------------|-------------|------------|
| 1 | 3×3 chunks (9 × heightmap + overlay) | 50–200 | ~100–300 |
| 2 | ~100 coarse cells visible | 5–20 (vehicles, markers) | ~30–60 |
| 3 | Entire continent mesh | 0 (icons are UI) | ~10–20 |
| 4 | Sphere + cloud layer | 0 (UI only) | ~5–8 |

Zoom-in is the expensive direction — Level 1 chunks stream in as the camera descends. The `ChunkStreamingProcessor` already handles this. During L2→L1 transition, high-priority load the chunk(s) under the camera.

### Memory

| Data | Size |
|------|------|
| Level 2 cache (100×100 regions) | ~200 KB (heights + biomes) |
| Level 3 cache (continent) | ~10 KB |
| Level 1 chunks (3×3 active) | Existing budget — unchanged |

---

## Files to Create / Modify

| Action | File | Notes |
|--------|------|-------|
| Create | `World/ZoomLevel.cs` | Enum |
| Create | `World/Scale/WorldLodCache.cs` | Level 2/3 height + biome pre-computation |
| Create | `World/Scale/BiomeClassifier.cs` | Surface type → biome mapping |
| Create | `World/Scale/BiomeType.cs` | Biome enum (Grassland, Forest, Desert, etc.) |
| Create | `World/Scale/PointOfInterest.cs` | POI record |
| Create | `World/Scale/RegionalRenderer.cs` | Level 2 terrain + silhouettes + feature lines |
| Create | `World/Scale/ContinentalRenderer.cs` | Level 3 terrain + territory overlays + city dots |
| Create | `World/Scale/ZoomTransitionController.cs` | Camera altitude → crossfade logic |
| Create | `World/Scale/TownSilhouetteBuilder.cs` | Builds LOD silhouette from Level 1 building footprints |
| Create | `World/Vehicles/VehicleData.cs` | Speed, fuel, cargo, durability |
| Create | `World/Vehicles/TravelController.cs` | Route following, fuel consumption, breakdown |
| Create | `World/Encounters/EncounterTrigger.cs` | Random encounter detection during L2/L3 travel |
| Create | `World/Scale/PlanetData.cs` | Planet + continent outline records |
| Create | `World/Scale/GlobeRenderer.cs` | Sphere mesh, texture projection, cloud layer |
| Create | `World/Scale/GlobeNavigationController.cs` | Globe rotation, continent selection, travel UI |
| Create | `World/Scale/PlanetTextureGenerator.cs` | Continent outlines → equirectangular texture |
| Modify | `World/ChunkStreamingProcessor.cs` | Priority loading during zoom transitions |
| Modify | `World/WorldMapData.cs` | Add `WorldLodCache`, POI list, `ZoomLevel` state |
| Modify | `World/Rendering/QualitySettings.cs` | Add Level 2/3 terrain subdivision settings |

---

## Acceptance Criteria

1. Camera smoothly transitions between all three zoom levels with no loading screen.
2. At Level 1 — individual characters, buildings, and trees are visible; tile overlay renders.
3. At Level 2 — towns appear as silhouette meshes; roads/rivers/rails draw as splines; party renders as vehicle or walking group.
4. At Level 3 — continent terrain with biome tinting; cities as labelled dots; faction territories as tinted overlays.
5. During L1↔L2 crossfade, both rendering layers blend smoothly over the 30–50 m altitude range.
6. Time acceleration applies correctly: ~60× at Level 2, ~1 440× at Level 3.
7. Random encounters at Level 2 zoom the camera to Level 1 for combat, then return to Level 2.
8. Vehicles consume fuel proportional to distance; party can travel on foot at reduced speed.
9. Fog of war state is consistent across zoom levels.
10. Level 2/3 height and biome data is derived from Level 1 tiles, not stored separately.
11. Globe (Level 4) renders a 3D planet with continent outlines, biome colouring, and ocean.
12. Clicking a discovered continent on the globe triggers travel cutscene and arrives at Level 3.
13. Undiscovered continents appear as dark silhouettes on the globe.
14. Globe is accessible only from travel hubs or with late-game unlock.
