# RCA-005: Tree Trunk Shadow Still Starts Partway Up (Not at Base)

**Date:** 2026-04-07  
**Severity:** Visual / Low  
**Affected:** Tree trunk shadow rendering (Step 07)  
**Supersedes:** RCA-004 (which correctly identified the shadow gap but proposed an incomplete fix)

---

## Symptom

After merging trunk + canopy into a single entity (RCA-004 fix), the trunk shadow still begins roughly 1/3 up the trunk instead of at the ground. There is a visible sunlit gap at the base of every trunk.

---

## Investigation

### Prior fixes applied (RCA-004)
- Increased `BaseTrunkRadius` from 0.08 to 0.15 — trunk is now wide enough for shadow map resolution. ✅
- Merged trunk + canopy into single entity — shadow caster is one unit. ✅

The shadow IS now visible on the trunk, but it still doesn't reach the ground. The fix increased trunk thickness and unified the shadow caster, but the fundamental geometry problem remained.

### Root cause: Open cylinder base allows light through

`AddCylinder()` generates **only side faces** — two rings of vertices connected by quads. There is no bottom cap (disc). The cylinder is a hollow tube.

From the **directional light's perspective** (looking down at ~30-60° angle), the light rays pass through the open bottom of the cylinder:

```
Light direction (30° elevation)
    ╲
     ╲    ┌──────┐  ← top ring (side faces block light here)
      ╲   │      │
       ╲  │      │  ← side faces cast shadow on ground
        ╲ │      │
         ╲└──────┘  ← bottom ring (NO bottom cap)
          ╲    ↗ light passes through open bottom
           ╲ ↗
────────────╳──────────── terrain surface
            ↑
     no shadow here — light entered
     through the open bottom
```

The shadow map renders geometry from the light's POV. Where the light can see through the open bottom tube, no depth is written to the shadow map, so no shadow appears on the ground at those positions.

The shadow only starts where the **side faces** of the cylinder actually occlude the light — which is offset from the base by:

```
offset ≈ trunk_diameter / tan(light_elevation_angle)
```

At diameter 0.23m and 30° elevation: `0.23 / tan(30°) ≈ 0.40m` — about 1/3 of the trunk height (0.94m), matching the screenshot exactly.

---

## Root Cause

**The trunk cylinder has no bottom cap face.** Light passes through the open tube base, preventing shadow map occlusion at the ground contact point.

---

## Fix

Add a bottom cap (triangle fan) to `AddCylinder()`. This closes the tube so the light cannot pass through the base:

```
Before (side faces only):     After (side faces + bottom cap):

    ┌──────┐                      ┌──────┐
    │      │                      │      │
    │      │                      │      │
    └──────┘                      └══════┘  ← cap blocks light
```

The cap adds 1 centre vertex + 8 triangles (one per segment) — negligible geometry cost.

---

## Verification

- `dotnet build src/Oravey2.Windows/Oravey2.Windows.csproj`
- Launch with `--scenario terrain_test`, confirm trunk shadow starts at ground level.
