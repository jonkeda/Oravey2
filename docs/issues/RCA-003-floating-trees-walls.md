# RCA-003: Trees and Walls Appear to Float Above Terrain

**Date:** 2026-04-07  
**Severity:** Visual / Low  
**Affected:** Trees (new, Step 07), Walls (pre-existing, Step 06)  

---

## Symptoms

1. **Trees**: Trunk bases show a visible gap above the grass surface when viewed from the isometric camera.
2. **Walls**: Wall bottoms appear to hover above the concrete overlay floor in Hybrid chunks.

---

## Investigation

### Hypothesis 1: Height mismatch between terrain mesh and placed objects

Diagnostic tests compared the Y position of tree trunk bases and wall bases against the terrain height grid (`GetSurfaceHeight`) and the nearest terrain vertex heights.

**Results (TreeHeightDiagnosticTests):**

| Object | Position | Object Y | Surface Y | Nearest Vertex Y | Delta |
|--------|----------|----------|-----------|-------------------|-------|
| Tree 0 | (1.22, 0.76) | 1.0000 | 1.0000 | 1.0000 | 0.000000 |
| Tree 1 | (0.94, 2.09) | 1.0000 | 1.0000 | 1.0000 | 0.000000 |
| Wall (3,4) | (3.00, 4.00) | 1.2500 | 1.2500 | 1.2500 | 0.000000 |
| Wall (26,7) | (26.00, 7.00) | 1.2500 | 1.2500 | 1.2500 | 0.000000 |

**All deltas are exactly zero.** The height grid, `GetSurfaceHeight`, and object placement all agree perfectly. The objects are at the correct Y position mathematically.

**Verdict: RULED OUT.** The positions are correct. The issue is visual.

### Hypothesis 2: Visual perception artifact from geometry + camera angle

**CONFIRMED as root cause.** Two factors combine to create the floating illusion:

#### Factor A — Open cylinder base (trees)

`TreeRenderer.AddCylinder()` generates only the **side faces** (two rings of vertices connected by quads). There is no bottom cap. From the isometric camera angle (~30-45° down), the viewer can see slightly under the open cylinder base. The terrain surface is visible through the open bottom, creating a visual gap.

```
Side view:           Isometric view:

 ┌──┐                  ╱──╲
 │  │                 ╱    ╲  ← can see under the open base
 │  │                ╱  ──  ╲
 └──┘               ▔▔▔▔▔▔▔▔▔▔  ← terrain
─────               (gap appears here)
```

The trunk radius is tiny (0.04–0.08 world units after scaling), so the terrain is fully visible around the base on all sides. Combined with the shadow casting to one side, the brain interprets this as "hovering."

#### Factor B — Overlay offset vs. wall base (walls)

In `TileOverlayBuilder`, the Hybrid chunk overlay floor is rendered at `GetSurfaceHeight + OverlayOffset` where `OverlayOffset = 0.02f`. But `CreateStructurePlaceholder` places walls at `GetSurfaceHeight + sizeY/2` — the raw terrain surface, NOT the overlay surface.

Result: the wall base sits **0.02 units below** the visible floor. The overlay renders on top of the wall's bottom face, but from an angle the thin gap is visible.

```
Y = 1.27  ─── overlay floor (visible surface)
Y = 1.25  ─── wall base / terrain surface
           ↑ 0.02 gap
```

This 0.02 gap (~2cm) is normally invisible head-on, but from the isometric view angle it appears as a visible floating effect, especially when shadows emphasize the contrast.

---

## Root Cause

**The Y positions are correct.** The floating appearance is caused by:

| Object | Cause | Fix |
|--------|-------|-----|
| Trees | Open cylinder bottom + thin trunk → can see terrain through/under base from isometric angle | Embed trunk base 0.05 units into terrain |
| Walls | Wall base at raw terrain Y, overlay floor at terrain Y + 0.02 → wall bottom below visible floor | Offset wall Y by `OverlayOffset` to sit on the overlay |

---

## Proposed Fixes

### Fix 1 — Trees: Embed trunk base into terrain

In `TreeRenderer.AddCylinder`, offset the bottom ring downward by a small amount so the trunk is partially buried in the terrain:

```csharp
// Before:
float y = baseCenter.Y + ring * height;

// After: embed bottom ring 0.05 below surface
float y = baseCenter.Y + ring * height - (ring == 0 ? 0.05f : 0f);
```

### Fix 2 — Walls: Account for overlay offset

In `TileOverlayBuilder`, when computing structure Y positions, add the `OverlayOffset`:

```csharp
// Before:
float cy = GetSurfaceHeight(...);

// After:
float cy = GetSurfaceHeight(...) + OverlayOffset;
```

---

## Verification

After applying fixes:
- `dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Vegetation."`
- Launch game with `--scenario terrain_test`, visually confirm trees sit flush with grass and walls sit flush with overlay floor.
