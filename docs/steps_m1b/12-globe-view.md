# Step 12 — Globe View (L4)

**Work streams:** WS7b (Globe View)
**Depends on:** Step 11 (zoom levels L1–L3)
**User-testable result:** Open globe UI → see the planet with continents, biome tinting, known regions highlighted. Click a distant region → travel dialog estimates journey time.

---

## Goals

1. UV sphere mesh with heightmap-derived continent shapes.
2. Orbital camera: click-drag to rotate, scroll to zoom.
3. Known regions highlighted, unknown regions dark.
4. Travel dialog with distance estimation and ETA.

---

## Tasks

### 12.1 — GlobeMesh Builder

- [ ] Create `Globe/GlobeMesh.cs`
- [ ] Generate a UV sphere (40 lat × 80 lon segments ≈ 3200 quads)
- [ ] Vertex displacement from a low-resolution world heightmap (L3 data)
- [ ] Per-vertex biome colour from `WorldLodCache` L3 biome data
- [ ] Coastlines: draw edge lines where altitude transitions from sea to land
- [ ] Ocean: separate semi-transparent sphere beneath for water

### 12.2 — Orbital Camera

- [ ] Create `Camera/OrbitalCameraController.cs`
- [ ] Click-drag rotates the globe (lat/lon rotation)
- [ ] Scroll-wheel adjusts distance from centre
- [ ] Double-click snaps to nearest known region
- [ ] Smooth damping on all rotations

### 12.3 — L3 ↔ L4 Transition

- [ ] At L3 max altitude (~5 km): terrain rendering fades
- [ ] Camera detaches and transitions to orbital mode
- [ ] Globe fades in during transition
- [ ] Reverse: zoom in from globe → snap to the region under cursor → transition back to L3

### 12.4 — Region Overlay

- [ ] Known regions: full colour, clickable with label
- [ ] Partially explored: dimmed colour, visible but no label
- [ ] Unknown: dark/greyed hemisphere
- [ ] Player's current region: pulsing outline indicator
- [ ] Region tooltips on mouse hover

### 12.5 — Travel Dialog

- [ ] Create `UI/GlobeTravelDialog.cs` (Stride UI)
- [ ] Shows: origin region, destination region, distance (km), estimated time (hours/days at current vehicle speed)
- [ ] Confirm → initiates fast-travel sequence (loading screen)
- [ ] Cancel returns to globe view
- [ ] Travel requires discovered route or road connection (otherwise "no known route")

### 12.6 — Fast-Travel Sequence

- [ ] On confirm: loading screen shown
- [ ] Time advances by travel duration (using `GameTimeService`)
- [ ] Resources consumed (fuel, food, water proportional to distance)
- [ ] Random encounter chance scaled by distance and danger level
- [ ] On arrival: camera transitions back from globe → L3 → L1 at destination

### 12.7 — Globe HUD Elements

- [ ] Compass rose
- [ ] Scale indicator (approximate distances)
- [ ] Legend: biome colours, faction colours
- [ ] Button to return to previous zoom level

### 12.8 — Unit Tests

File: `tests/Oravey2.Tests/Globe/GlobeMeshTests.cs`

- [ ] `GenerateSphere_CorrectVertexCount` — 40 × 80 + poles ≈ expected vertex count
- [ ] `GenerateSphere_AllNormalsPointOutward` — every vertex normal has positive dot with position vector
- [ ] `HeightDisplacement_OceanAtZero` — sea-level vertices not displaced
- [ ] `HeightDisplacement_MountainAboveZero` — high-altitude vertices displaced outward

File: `tests/Oravey2.Tests/Globe/TravelEstimatorTests.cs`

- [ ] `EstimateTime_100km_OnFoot_Returns20Hours` — walk speed ~5 km/h
- [ ] `EstimateTime_100km_Vehicle_Returns2Hours` — vehicle ~50 km/h
- [ ] `NoKnownRoute_ReturnsNull` — unconnected regions return no estimate
- [ ] `FuelConsumption_ProportionalToDistance`

### 12.9 — UI Tests

File: `tests/Oravey2.UITests/Globe/GlobeViewTests.cs`

- [ ] `OpenGlobe_ShowsPlanet` — zoom to L4, screenshot confirms globe mesh rendered
- [ ] `ClickRegion_ShowsTravelDialog` — click a known region, verify travel dialog appears with distance
- [ ] `GlobeRotation_DragChangesView` — mouse-drag changes the visible hemisphere

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Globe."
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~GlobeView"
```

**User test:** Zoom all the way out past the continental map. The camera transitions to an orbital view showing the entire planet. Known regions glow with biome colours; unknown regions are dark. Click a distant discovered region → a travel dialog shows distance and estimated time. Drag to rotate the globe.
