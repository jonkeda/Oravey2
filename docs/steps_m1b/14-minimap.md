# Step 14 — Minimap & HUD

**Work streams:** WS13 (Minimap & HUD)
**Depends on:** Step 03 (heightmap renderer), Step 10 (chunk streaming), Step 11 (zoom levels)
**User-testable result:** Corner minimap shows surrounding terrain, party icon, POI markers, and fog-of-war mask. Click the minimap to jump the camera to that location.

---

## Goals

1. Render-to-texture minimap with terrain colour, entities, and fog of war.
2. POI icons and map markers on the minimap.
3. Click-to-navigate and minimap zoom controls.
4. HUD status bar with location name, coordinates, time, weather icon.

---

## Tasks

### 14.1 — Minimap Render Target

- [ ] Create `UI/MinimapRenderer.cs`
- [ ] Secondary camera looking straight down at player position
- [ ] Renders to a 256×256 `RenderTexture`
- [ ] Updates every 0.5 s (not every frame — performance budget)
- [ ] Terrain colours simplified: height → green/brown/grey, water → blue

### 14.2 — Minimap Terrain Colouring

- [ ] Convert `SurfaceType` to colour: Grass:green, Concrete:grey, Sand:tan, Water:blue, Dirt:brown, Snow:white, Swamp:olive, Gravel:light grey
- [ ] Heightmap shading: simple hillshade (NW light source) for topographic feel
- [ ] Linear features: roads as dark lines, rivers as blue lines

### 14.3 — Minimap Icons & Markers

- [ ] Player party icon: arrow showing facing direction
- [ ] NPCs within range: small dots (colour by faction)
- [ ] POI icons: town, dungeon, landmark, quest marker
- [ ] Custom user markers: player-placed pins (right-click minimap → place marker)
- [ ] Quest destination marker with distance readout

### 14.4 — Fog of War Overlay

- [ ] Explored areas fully visible
- [ ] Visited but not currently visible: dimmed (50% opacity dark overlay)
- [ ] Never visited: fully dark
- [ ] Fog mask stored as a bitmap per-region in `SaveStateStore`
- [ ] Revealed circle around party based on line-of-sight range

### 14.5 — Minimap Interaction

- [ ] Click on minimap: snap camera to that world position
- [ ] Scroll on minimap: zoom minimap in/out (separate from main camera zoom)
- [ ] Drag on minimap: pan the minimap view independently
- [ ] Minimap zoom range: 50 m – 500 m radius around player
- [ ] Toggle minimap size: small corner → large overlay (keyboard shortcut M)

### 14.6 — HUD Status Bar

- [ ] Create `UI/HudStatusBar.cs`
- [ ] Top-left: current location name (region, sub-area)
- [ ] Top-left below location: world coordinates (x, y) in tile units
- [ ] Top-right: current game time (HH:MM, day count)
- [ ] Top-right below time: weather icon + temperature
- [ ] Semi-transparent background bar, auto-hides after 5 s of no change

### 14.7 — Compass

- [ ] Horizontal compass strip at top centre of screen
- [ ] Cardinal directions (N, E, S, W) + ordinals
- [ ] POI bearing indicators on the compass strip (nearby quest targets, towns)
- [ ] Rotates with camera facing direction

### 14.8 — Minimap Settings

- [ ] Settings panel: minimap size, opacity, update frequency
- [ ] Toggle elements: fog of war, NPC dots, POI icons, roads
- [ ] Corner position: top-left, top-right, bottom-left, bottom-right

### 14.9 — Unit Tests

File: `tests/Oravey2.Tests/Minimap/MinimapRendererTests.cs`

- [ ] `SurfaceToColour_Grass_IsGreen` — Grass → expected green colour
- [ ] `SurfaceToColour_Water_IsBlue` — Water → blue
- [ ] `SurfaceToColour_AllTypesHaveColour` — no unmapped surface types

File: `tests/Oravey2.Tests/Minimap/FogOfWarTests.cs`

- [ ] `NewRegion_FullyDark` — fresh region has no revealed cells
- [ ] `RevealRadius_MarksExplored` — calling reveal at position clears fog in radius
- [ ] `VisitedButNotVisible_IsDimmed` — previously explored but out of range = dimmed
- [ ] `FogMask_SerializesToBitmap` — round-trip save/load of fog bitmap
- [ ] `FogMask_Merge_CombinesTwoRegions` — merging two fog masks unions revealed areas

File: `tests/Oravey2.Tests/Minimap/CompassTests.cs`

- [ ] `FacingNorth_NorthCentred` — camera facing north → N at centre
- [ ] `FacingEast_EastCentred` — camera facing east → E at centre
- [ ] `POIBearing_East_ShowsRight` — POI to the east appears right of centre

### 14.10 — UI Tests

File: `tests/Oravey2.UITests/Minimap/MinimapTests.cs`

- [ ] `MinimapVisible_InCorner` — minimap texture present in expected screen corner
- [ ] `MinimapClick_MovesCamera` — click a point on the minimap, camera world position changes to match
- [ ] `ToggleMinimap_SizeChanges` — press M key, minimap toggles between small and large

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Minimap."
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~Minimap"
```

**User test:** Start the game and look at the bottom-right corner. A minimap shows the surrounding terrain with your party as an arrow. POI icons (towns, dungeons) appear as small symbols. Dark areas represent unexplored regions. Click a visible spot on the minimap — the camera jumps there. Press M to enlarge the minimap to a half-screen overlay. A compass strip at the top shows N/E/S/W and nearby quest markers.
