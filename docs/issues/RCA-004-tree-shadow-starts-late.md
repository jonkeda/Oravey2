# RCA-004: Tree Shadows Appear to Start Too Late (Disconnected from Trunk Base)

**Date:** 2026-04-07  
**Severity:** Visual / Low  
**Affected:** Tree rendering (Step 07)  

---

## Symptom

Tree shadows on the ground do not begin at the trunk base. There is a lit gap between where the trunk meets the terrain and where the shadow starts. The shadow appears to "start too late," creating a floating/disconnected look.

---

## Investigation

### Rendering setup

Trees are rendered as two separate entities per chunk:

| Layer | Entity name | Geometry | Material colour |
|-------|-------------|----------|-----------------|
| Trunks | `TreeTrunks_{cx}_{cy}` | Cylinder (8 segments, 2 rings) | Brown (0.40, 0.28, 0.16) |
| Canopies | `TreeCanopies_{cx}_{cy}` | Sphere (6×6 segments) | Green (0.25, 0.48, 0.18) |

Both use the default `ModelComponent` which has `IsShadowCaster = true`.

### Geometry dimensions (at typical GrowthStage ~200, scale ≈ 0.78)

| Part | Dimension | Value |
|------|-----------|-------|
| Trunk radius | `0.08 × 0.78` | **0.062 m** (6.2 cm) |
| Trunk height | `1.2 × 0.78` | **0.94 m** |
| Canopy radius | `0.6 × 0.78` | **0.47 m** |
| Canopy centre Y | `surface + 0.94 + 0.47` | **1.41 m above ground** |

### Shadow map resolution analysis

Stride's default PCF shadow map is typically 1024×1024 or 2048×2048 pixels covering the visible scene. For a scene spanning ~96 m (48 tiles × 2 m/tile), each shadow map texel covers:

- At 1024: `96 / 1024 ≈ 0.094 m/texel`  
- At 2048: `96 / 2048 ≈ 0.047 m/texel`

The trunk diameter is `0.062 × 2 = 0.124 m`, which is only **1.3 texels** at 1024 resolution or **2.6 texels** at 2048. With PCF filtering and shadow bias, this becomes sub-pixel or washes out entirely.

---

## Root Cause

**The trunk cylinder is too thin to produce a visible shadow in the shadow map.**

The canopy sphere (diameter ~0.94 m, ~10–20 shadow texels) casts a clearly visible shadow. The trunk (diameter ~0.12 m, ~1–2 shadow texels) does not survive shadow map filtering.

The directional light projects the canopy shadow onto the ground at the light angle, displaced from the trunk base by:

```
displacement = trunk_height × tan(light_elevation_angle)
```

At a typical 30° light elevation: `0.94 × tan(60°) ≈ 1.6 m` horizontal displacement.

This creates a **1.6 m lit gap** between the trunk base and the visible canopy shadow — exactly what's visible in the screenshot.

```
Light (30° from horizon)
  ╲
   ╲    canopy ●
    ╲          │ trunk (shadow invisible — too thin)  
     ╲         │
      ╲        ▼ trunk base on ground
───────────────┊─────────[canopy shadow]────── terrain
               ↑                ↑
          no visible       shadow starts
          shadow here      here (1.6 m away)
```

---

## Proposed Fix

Increase `BaseTrunkRadius` from `0.08` to `0.15` so the trunk diameter at typical scale becomes ~0.23 m (~2.5–5 shadow texels). This is thick enough to survive shadow map filtering while still looking proportional as a placeholder tree trunk.

**Before:** `BaseTrunkRadius = 0.08` → 12.4 cm diameter → invisible shadow  
**After:** `BaseTrunkRadius = 0.15` → 23.4 cm diameter → visible shadow that connects trunk base to canopy shadow

This also improves the overall visual proportions — the current trunks look like thin sticks relative to the canopy size.

---

## Verification

- `dotnet build src/Oravey2.Windows/Oravey2.Windows.csproj`
- Launch with `--scenario terrain_test`, visually confirm trunk shadows connect from base to canopy shadow on the ground.
