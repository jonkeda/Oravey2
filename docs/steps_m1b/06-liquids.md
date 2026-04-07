# Step 06 — Liquid Rendering

**Work streams:** WS6 (Liquid Rendering)
**Depends on:** Step 03 (heightmap renderer)
**User-testable result:** Launch the game → water lakes, lava pools, and toxic puddles are visible with distinct shader effects.

---

## Goals

1. Render liquid surfaces at `WaterLevel` height for each `LiquidType`.
2. Per-type shaders: water, lava, toxic, oil, frozen, anomaly.
3. Waterfall detection and cascade rendering.
4. Shore edge effects.

---

## Tasks

### 6.1 — LiquidRenderer

- [ ] Create `World/Liquids/LiquidRenderer.cs`
- [ ] Use `WaterHelper.FindConnectedWater()` to group contiguous liquid tiles by `LiquidType`
- [ ] For each region: build flat mesh at `WaterLevel × 0.25f` height
- [ ] Shore data from `WaterHelper.IsShore()` for edge effects
- [ ] Select shader variant per `LiquidType`

### 6.2 — LiquidProperties

- [ ] Create `World/Liquids/LiquidProperties.cs`
- [ ] Lookup per `LiquidType`: opacity, flow speed, emissive flag, colour, damage per second

### 6.3 — Water Shader

- [ ] Create `Rendering/Shaders/WaterShader.sdsl`
- [ ] Low: flat tinted plane with animated UV scroll
- [ ] Medium: normal-mapped waves (2-octave sine), depth-based alpha
- [ ] High: screen-space reflection/refraction, foam at shore, caustics

### 6.4 — Lava Shader

- [ ] Create `Rendering/Shaders/LavaShader.sdsl`
- [ ] Crust pattern: Perlin noise for dark cooling plates with bright orange cracks
- [ ] Emissive in cracks, dim on crust
- [ ] Spawn point light per lava region (colour orange, intensity from area)

### 6.5 — Toxic Shader

- [ ] Create `Rendering/Shaders/ToxicShader.sdsl`
- [ ] Voronoi noise bubbles expanding and popping
- [ ] Green emissive pulse (sin wave, ~3s period)
- [ ] On High: rising green mist particle emitter

### 6.6 — Other Liquid Shaders

- [ ] `OilShader.sdsl` — thin-film interference rainbow sheen + dark base
- [ ] `FrozenShader.sdsl` — ice texture + crack normal map, static (no animation)
- [ ] `AnomalyShader.sdsl` — spiral UV distortion + pulsing purple emissive

### 6.7 — Waterfall Detection & Rendering

- [ ] Detect where liquid tiles have cliff-edge height difference (delta ≥ 7 between adjacent tiles)
- [ ] Build vertical cascade ribbon mesh from upper to lower water surface
- [ ] Scrolling water/lava texture on cascade
- [ ] Foam decal at base (water) or embers (lava)

### 6.8 — Shore Effects

- [ ] Per liquid type: shore decal ring around liquid edge
- [ ] Water: white foam, Lava: charred ring, Toxic: crusty residue, Oil: dark slick edge

### 6.9 — Integration

- [ ] `ChunkTerrainBuilder` calls `LiquidRenderer` after heightmap build
- [ ] Liquid meshes added to entity scene alongside terrain
- [ ] Test scene: add a water lake, a lava pool, a toxic puddle, and a waterfall edge

### 6.10 — Unit Tests

File: `tests/Oravey2.Tests/Liquids/LiquidRegionTests.cs`

- [ ] `ConnectedWaterTiles_GroupedIntoOneRegion` — 4 adjacent water tiles → 1 region
- [ ] `DisjointWaterTiles_GroupedIntoTwoRegions` — 2 separate water bodies → 2 regions
- [ ] `DifferentLiquidTypes_NotGroupedTogether` — adjacent water + lava → separate regions
- [ ] `ShoreDetection_IdentifiesEdgeTiles` — liquid tile adjacent to non-liquid is shore

File: `tests/Oravey2.Tests/Liquids/LiquidPropertiesTests.cs`

- [ ] `AllLiquidTypes_HaveProperties` — every `LiquidType` except `None` has a valid entry
- [ ] `Lava_IsEmissive` — lava property has `Emissive = true`
- [ ] `Water_NoDamage` — water damage per second is 0

### 6.11 — UI Tests

File: `tests/Oravey2.UITests/Terrain/LiquidRenderingTests.cs`

- [ ] `WaterLake_IsVisible` — screenshot shows blue/water area where lake tiles exist
- [ ] `LavaPool_HasGlow` — screenshot of lava area has bright orange/red pixels (emissive check)

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Liquids."
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~LiquidRendering"
```

**User test:** Launch the game. A water lake is visible with shore foam. A lava pool glows orange with dark crust. A small toxic puddle pulses green. Where the lake drops over a cliff, a waterfall cascades down.
