# Special Surface & Liquid Rendering

**Status:** Draft  
**Milestone:** M1b  
**Depends on:** Heightmap–Tilemap Hybrid (heightmap-tilemap-hybrid.md), Water system (Phase 4 ✓)

---

## Problem

The current water system (`WaterHelper`) handles a single water type — clean water with shore detection and connected-region flood-fill. A post-apocalyptic world needs a much wider variety of ground liquids and hazardous surfaces: radioactive puddles, lava flows, stagnant waste pools, waterfalls cascading off height cliffs, and frozen/dried-out lake beds. Each type has distinct visual behaviour, gameplay effects, and rendering requirements.

This document defines how each special surface type is represented in data, rendered on the heightmap terrain, and integrated with gameplay systems.

---

## Surface & Liquid Taxonomy

| Category | Types | Motion | Emissive | Gameplay Effect |
|----------|-------|--------|----------|-----------------|
| **Water** | Lake, pond, ocean | Gentle wave | No | Slows movement, extinguishes fire |
| **Flowing water** | River, stream, waterfall | Directional flow | No | Pushes entities in flow direction |
| **Toxic liquid** | Waste puddle, acid pool, sewage | Slow bubble | Faint green glow | Radiation/poison damage over time |
| **Lava** | Lava pool, lava flow | Slow churn | Bright orange | Lethal damage, ignites nearby |
| **Frozen** | Ice sheet, frozen lake | Static | No | Low friction, can crack under weight |
| **Dry hazard** | Tar pit, quicksand, oil slick | Very slow | No | Immobilises, slows, flammable |
| **Magical/anomaly** | Anomaly pool, void puddle | Swirl | Variable | Zone-specific effects |

---

## Data Model

### Extended SurfaceType

The existing `SurfaceType` enum covers solid terrain (Dirt, Asphalt, Concrete, Grass, Sand, Mud, Rock, Metal). Liquid surfaces are **not** a surface type — they sit above the terrain. They are represented via `WaterLevel` + a new `LiquidType` field.

```csharp
public enum LiquidType : byte
{
    None,           // No liquid
    Water,          // Clean water (existing behaviour)
    Toxic,          // Radioactive / chemical waste
    Acid,           // Corrosive — damages equipment
    Sewage,         // Contaminated — disease risk
    Lava,           // Molten rock — lethal
    Oil,            // Flammable surface liquid
    Frozen,         // Solid ice layer (renders as surface, not liquid)
    Anomaly         // Supernatural / zone-specific
}
```

### TileData Extension

```csharp
public readonly record struct TileData(
    SurfaceType Surface,
    byte HeightLevel,
    byte WaterLevel,
    LiquidType Liquid,         // NEW — type of liquid when WaterLevel > HeightLevel
    int StructureId,
    TileFlags Flags,
    byte VariantSeed);
```

When `WaterLevel > HeightLevel`, the tile has liquid of type `Liquid`. When `Liquid == LiquidType.None` and `WaterLevel > HeightLevel`, default to `Water` for backward compatibility.

### Liquid Properties Lookup

```csharp
public static class LiquidProperties
{
    public static LiquidInfo Get(LiquidType type) => type switch
    {
        LiquidType.Water   => new(Opacity: 0.6f, FlowSpeed: 0.3f, Emissive: false,
                                   Color: new Color3(0.1f, 0.3f, 0.5f), DamagePerSecond: 0f),
        LiquidType.Toxic   => new(Opacity: 0.8f, FlowSpeed: 0.1f, Emissive: true,
                                   Color: new Color3(0.2f, 0.6f, 0.1f), DamagePerSecond: 5f),
        LiquidType.Acid    => new(Opacity: 0.7f, FlowSpeed: 0.15f, Emissive: true,
                                   Color: new Color3(0.5f, 0.7f, 0.0f), DamagePerSecond: 10f),
        LiquidType.Sewage  => new(Opacity: 0.9f, FlowSpeed: 0.05f, Emissive: false,
                                   Color: new Color3(0.3f, 0.25f, 0.1f), DamagePerSecond: 2f),
        LiquidType.Lava    => new(Opacity: 1.0f, FlowSpeed: 0.08f, Emissive: true,
                                   Color: new Color3(1.0f, 0.3f, 0.0f), DamagePerSecond: 50f),
        LiquidType.Oil     => new(Opacity: 0.85f, FlowSpeed: 0.02f, Emissive: false,
                                   Color: new Color3(0.05f, 0.05f, 0.05f), DamagePerSecond: 0f),
        LiquidType.Frozen  => new(Opacity: 0.3f, FlowSpeed: 0f, Emissive: false,
                                   Color: new Color3(0.7f, 0.85f, 0.95f), DamagePerSecond: 0f),
        LiquidType.Anomaly => new(Opacity: 0.5f, FlowSpeed: 0.6f, Emissive: true,
                                   Color: new Color3(0.4f, 0.0f, 0.6f), DamagePerSecond: 15f),
        _ => LiquidInfo.Empty
    };
}

public record struct LiquidInfo(
    float Opacity, float FlowSpeed, bool Emissive,
    Color3 Color, float DamagePerSecond);
```

---

## Rendering Architecture

All liquids share a common rendering pipeline with type-specific shader parameters.

```
┌──────────────────────────────────────────────────┐
│              LiquidRenderer                       │
│                                                    │
│  1. Group contiguous liquid tiles by LiquidType   │
│  2. For each group → build liquid mesh            │
│  3. Select shader variant per LiquidType          │
│  4. Submit draw calls                             │
│                                                    │
│  ┌────────────┐ ┌────────────┐ ┌────────────┐    │
│  │ WaterShader│ │ LavaShader │ │ ToxicShader│... │
│  └────────────┘ └────────────┘ └────────────┘    │
└──────────────────────────────────────────────────┘
```

### Liquid Mesh

Each connected region of a single `LiquidType` produces one flat mesh at `WaterLevel × 0.25f`. The mesh outline follows tile boundaries with optional edge smoothing on Medium/High quality (Catmull-Rom on the shoreline vertices).

Shore tiles (detected via existing `WaterHelper.IsShore()`) get **foam/edge decals** — the style depends on the liquid type (white foam for water, bubbling crust for lava, scum for sewage).

---

## Per-Type Rendering Details

### 1. Clean Water (Lakes, Ponds, Ocean)

The baseline liquid. Uses the existing water pass extended to the heightmap system.

| Quality | Technique |
|---------|-----------|
| Low | Flat tinted plane, animated UV scroll |
| Medium | Normal-mapped waves (2-octave sine), depth-based alpha fade |
| High | Screen-space reflection/refraction, foam particles at shore, caustics on lake bed |

**Depth colouring:** Deeper water is darker. Shader samples `HeightLevel` (lake bed) vs `WaterLevel` (surface) to compute depth, then tints toward deep blue/green.

```hlsl
float depth = (waterLevel - terrainHeight) * 0.25;
float3 shallowColor = float3(0.2, 0.5, 0.6);
float3 deepColor    = float3(0.02, 0.08, 0.15);
float3 waterColor   = lerp(shallowColor, deepColor, saturate(depth / 4.0));
```

**Shore foam:** A scrolling noise texture masked by distance-to-shore. Shore config from `WaterHelper.GetShoreConfig()` determines which edges get foam.

---

### 2. Toxic Waste Puddles

Stagnant pools of radioactive or chemical waste found near ruined facilities.

| Feature | Implementation |
|---------|---------------|
| Surface colour | Yellow-green tint, opaque |
| Bubbles | Shader-driven: random bubble circles expand and pop (noise-based displacement) |
| Glow | Emissive term pulsing slowly (sin wave, 0.5–1.0 intensity, ~3s period) |
| Edge | Crusty residue decal ring around shore — uses `GetShoreConfig()` bitmask |
| Particles | On High quality: rising green mist particle emitter over the surface |

**Shader parameters:**

```hlsl
float bubbleNoise = VoronoiNoise(worldUV * 3.0 + time * 0.1);
float glow = 0.75 + 0.25 * sin(time * 2.0);
float3 color = float3(0.2, 0.6, 0.1) * glow;
float alpha = 0.8 + 0.1 * bubbleNoise;
```

**Gameplay integration:** Tiles with `LiquidType.Toxic` automatically set `TileFlags.Irradiated`. Standing in toxic liquid applies radiation damage per second from `LiquidProperties`.

---

### 3. Lava (Pools and Flows)

Molten rock in volcanic or bombed-out areas.

| Feature | Implementation |
|---------|---------------|
| Surface | Bright orange/red, fully opaque |
| Crust | Dark cooling crust pattern that drifts slowly — Perlin noise mask |
| Cracks | Bright emissive lines between crust plates |
| Glow | Strong emissive — lights nearby terrain and entities (point light per region) |
| Heat distortion | On High quality: screen-space heat haze above surface (post-process distortion) |
| Edge | Charred/blackened terrain ring — modifies splat map in 1-tile border |

**Shader approach:**

```hlsl
// Crust pattern: dark plates with bright cracks
float crust = smoothstep(0.4, 0.6, PerlinNoise(worldUV * 2.0 + time * 0.02));
float3 hotColor  = float3(1.0, 0.4, 0.0);   // Bright orange
float3 crustColor = float3(0.15, 0.05, 0.02); // Dark cooled rock
float3 color = lerp(hotColor, crustColor, crust);
float emissive = lerp(3.0, 0.1, crust);  // Bright in cracks, dim on crust
```

**Dynamic lighting:** Each lava region spawns a point light entity at region centroid. Light colour matches lava, intensity scales with region area (clamped). Flickers via subtle noise on intensity.

**Gameplay:**
- Lethal damage (50 hp/s from `LiquidProperties`)
- Sets `TileFlags.Burnable` false on adjacent tiles (already charred)
- Ignites entities/props within 1 tile radius

---

### 4. Waterfalls

Waterfalls occur where a river or lake drops sharply — specifically, where liquid tiles have a `HeightHelper.GetSlopeType() == SlopeType.Cliff` (delta ≥ 7) between two liquid tiles at different heights.

**Detection:**

```csharp
bool IsWaterfall(TileData upper, TileData lower)
{
    return upper.WaterLevel > upper.HeightLevel
        && lower.WaterLevel > lower.HeightLevel
        && HeightHelper.GetHeightDelta(upper, lower) >= 7
        && upper.Liquid is LiquidType.Water or LiquidType.None;
}
```

**Rendering:**

| Component | Implementation |
|-----------|---------------|
| Cascade plane | Vertical quad(s) from upper water surface to lower water surface |
| Texture | Scrolling water texture, UV.y animated downward at flow speed |
| Foam at base | Particle emitter + animated foam decal at impact zone |
| Mist | On High quality: translucent particle cloud rising from base |
| Sound zone | Ambient waterfall sound source at cascade location |

The cascade is built as a vertical ribbon mesh along the cliff edge, with width matching the water extent at that edge. Multiple tiles of cliff edge merge into a single wide waterfall.

```
  Upper water   ════════════
                ║ cascade  ║   ← vertical ribbon, UV scrolls down
                ║  texture ║
  Lower water   ════════════
                  ↕ foam ↕
```

**Lava falls:** Same detection but for `LiquidType.Lava`. Cascade texture becomes bright emissive lava flow. No mist — instead, ember particles rise from the base.

---

### 5. Frozen Surfaces (Ice)

`LiquidType.Frozen` represents ice on top of what would otherwise be water. The water level indicates the ice surface; the terrain below is the lake bed.

| Feature | Implementation |
|---------|---------------|
| Surface | Semi-transparent, slightly reflective |
| Texture | Tiled ice texture with subtle crack pattern |
| Reflection | On High quality: environment reflection (same as water reflection, but sharper) |
| Snow cover | Optional: if adjacent tiles have `SurfaceType.Dirt` and weather is cold, snow decal on top |

**Rendering:** Ice is **not** animated — it uses a static mesh at `WaterLevel` height. The crack pattern is a tiled normal map; intensity controlled by `VariantSeed` so some patches look more cracked.

**Gameplay:**
- Reduced friction: movement speed ×1.3 but direction change cost ×2.0
- Can crack: entities above weight threshold break through → become water tiles
- No radiation/damage

---

### 6. Oil Slicks and Tar Pits

Dark, viscous surfaces that are flammable.

| Feature | Implementation |
|---------|---------------|
| Surface | Very dark, high specular (mirror-like oil sheen) |
| Rainbow sheen | On Medium/High: thin-film interference effect (iridescent colour shift based on view angle) |
| Motion | Near-static with very slow ripple |
| Tar pits | Thicker, no sheen — occasional bubble pop (reuse toxic bubble shader, slower) |

**Oil sheen shader:**

```hlsl
// Thin-film interference approximation
float filmThickness = 0.5 + 0.3 * sin(worldUV.x * 10.0 + worldUV.y * 7.0);
float3 sheenColor = float3(
    0.5 + 0.5 * cos(filmThickness * 6.28 + 0.0),
    0.5 + 0.5 * cos(filmThickness * 6.28 + 2.09),
    0.5 + 0.5 * cos(filmThickness * 6.28 + 4.19));
float3 baseColor = float3(0.02, 0.02, 0.02);
float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), 3.0);
color = lerp(baseColor, sheenColor * 0.3, fresnel);
```

**Gameplay:**
- Oil: Flammable — fire propagation ignites oil tiles, which burn for 10s then become `SurfaceType.Dirt`
- Tar: Immobilises entities for 1 turn, movement cost ×3.0 through tar

---

### 7. Anomaly Pools

Supernatural / sci-fi hazard zones. Visual style is highly distinct to signal danger.

| Feature | Implementation |
|---------|---------------|
| Surface | Dark purple/void colour, swirling pattern |
| Swirl | Animated UV distortion — spiral pattern rotating around pool centre |
| Edge distortion | On High quality: screen-space warp effect near pool edges |
| Particles | Floating specks rising from surface, dissolving upward |
| Glow | Pulsing purple emissive (faster pulse than toxic — ~1.5s period) |

**Gameplay:** Zone-specific — effects configured per map region. Examples: teleport to random tile, reverse controls for 3 turns, halve max HP while in pool.

---

## Quality Presets Summary

| Feature | Low | Medium | High |
|---------|-----|--------|------|
| Water surface | Flat tinted | Normal-mapped waves | Reflection + refraction |
| Water depth tint | No | Yes | Yes + caustics |
| Shore foam | Decal only | Animated decal | Decal + particles |
| Toxic bubbles | Static tint | Animated noise | Animated + mist particles |
| Lava crust | Static texture | Animated drift | Animated + heat haze |
| Lava lighting | None | Single point light | Point light + flicker |
| Waterfall | Scrolling quad | Scrolling + foam | Scrolling + foam + mist |
| Ice reflection | None | Blurred reflection | Sharp reflection |
| Oil sheen | Flat dark | Thin-film sheen | Sheen + ripple |
| Anomaly swirl | Tinted plane | UV distortion | UV distortion + screen warp |

---

## Rendering Order

Liquids render after the heightmap terrain and before translucent props:

```
1. Heightmap terrain         (opaque)
2. Floor decals / overlay    (alpha blend)
3. Structure meshes          (opaque)
4. Road / rail decals        (alpha blend)
5. ── Liquid surfaces ──     (translucent, sorted back-to-front by type)
   a. Opaque liquids first   (lava, sewage, tar)
   b. Translucent liquids    (water, toxic, oil, anomaly)
   c. Ice                    (semi-transparent, renders with terrain)
6. Waterfall cascades        (translucent)
7. Liquid particles          (additive blend)
8. Props / entities          (opaque + alpha)
9. Post-process              (heat haze, screen warp)
```

---

## Liquid Region Detection

Connected liquid tiles of the same `LiquidType` are grouped into **regions** using `WaterHelper.FindConnectedWater()` (already implemented). Each region gets:

- One liquid mesh (merged tile outlines)
- One optional point light (lava, anomaly)
- One particle emitter origin (centroid)
- Shore data for edge effects

Regions are recalculated when chunks load or when tiles mutate (e.g., oil ignites → becomes dirt).

---

## Gameplay Integration

### Damage System

```csharp
public void ApplyLiquidDamage(Entity entity, TileData tile, float deltaTime)
{
    if (tile.WaterLevel <= tile.HeightLevel) return;

    var info = LiquidProperties.Get(tile.Liquid);
    if (info.DamagePerSecond <= 0f) return;

    var health = entity.Get<HealthComponent>();
    health.TakeDamage(info.DamagePerSecond * deltaTime, DamageSource.Environment);
}
```

### Movement Modifiers

| Liquid | Speed Multiplier | Special |
|--------|-----------------|---------|
| Water  | ×0.6 | — |
| Toxic  | ×0.5 | Radiation tick |
| Acid   | ×0.5 | Equipment durability loss |
| Sewage | ×0.4 | Disease chance on exit |
| Lava   | ×0.3 | Lethal — rarely survivable |
| Oil    | ×0.8 | Slip: random direction offset |
| Frozen | ×1.3 | Momentum: can't stop in 1 tile |
| Anomaly| ×0.7 | Zone-specific |

### Fire Propagation

Oil and certain props are flammable. When fire reaches an oil tile:

1. Oil tile ignites → emissive fire overlay for burn duration.
2. Adjacent oil tiles ignite after 1–2s delay (chain reaction).
3. After burn completes, `Liquid` resets to `None`, `WaterLevel` drops to `HeightLevel`, `Surface` becomes `Dirt`.

Lava adjacent to water produces steam particles and can solidify into `SurfaceType.Rock` if water volume is large enough (deferred — advanced interaction).

---

## Files to Create / Modify

| Action | File | Notes |
|--------|------|-------|
| Create | `World/LiquidType.cs` | Liquid type enum |
| Create | `World/LiquidProperties.cs` | Per-type property lookup |
| Create | `World/LiquidInfo.cs` | Property record struct |
| Create | `World/Terrain/LiquidRenderer.cs` | Region mesh builder + draw |
| Create | `World/Terrain/LiquidShaderParams.cs` | Per-type shader parameter sets |
| Create | `World/Terrain/WaterfallDetector.cs` | Cliff-edge waterfall detection |
| Create | `World/Terrain/WaterfallRenderer.cs` | Vertical cascade mesh builder |
| Create | `Rendering/LiquidSurface.sdsl` | Base liquid Stride shader |
| Create | `Rendering/LavaEffect.sdsl` | Lava crust + emissive shader |
| Create | `Rendering/ToxicEffect.sdsl` | Toxic bubble + glow shader |
| Create | `Rendering/OilSheenEffect.sdsl` | Thin-film interference shader |
| Modify | `World/TileData.cs` | Add `LiquidType Liquid` field |
| Modify | `World/WaterHelper.cs` | Respect `LiquidType` in region grouping |
| Modify | `World/Terrain/ChunkTerrainBuilder.cs` | Build liquid meshes per chunk |
| Modify | `World/Rendering/QualitySettings.cs` | Add liquid quality tiers |

---

## Acceptance Criteria

1. Clean water lakes render with depth-based tinting and shore foam.
2. Toxic puddles glow green and display bubble animation.
3. Lava pools show drifting crust with bright emissive cracks and illuminate nearby terrain.
4. Waterfalls render as vertical cascading ribbons between height-different water tiles.
5. Oil slicks display iridescent sheen on Medium/High quality.
6. Frozen surfaces render as semi-transparent ice with crack texture.
7. Anomaly pools show swirling purple animation.
8. All liquid types apply correct movement speed modifiers and damage.
9. Oil ignites and chain-propagates fire to adjacent oil tiles.
10. Quality presets correctly scale liquid rendering complexity.
11. Liquid regions group correctly — adjacent toxic tiles form one mesh, not per-tile draws.
12. Existing `WaterHelper` unit tests pass; new tests cover `LiquidType` region grouping.
