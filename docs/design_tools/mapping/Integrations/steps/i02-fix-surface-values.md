# Step i02 — Fix Surface Value Mismatch

**Design doc:** QA-review §7, compatibility review
**Depends on:** None
**Deliverable:** MapGen condenser writes `SurfaceType` enum values
directly. Integer values in `TownLayout.Surface` match Core's
`SurfaceType`.

---

## Goal

The `TownMapCondenser` currently assigns surface integers with its
own numbering (1=Grass, 3=Gravel) which conflicts with Core's
`SurfaceType` enum (1=Asphalt, 3=Grass). Fix the condenser to use
`SurfaceType` values directly, eliminating the mismatch.

---

## Current mismatch

| Int | MapGen condenser | Core `SurfaceType` |
|-----|-----------------|-------------------|
| 0 | Dirt | Dirt ✓ |
| 1 | Grass | Asphalt ✗ |
| 2 | Concrete | Concrete ✓ |
| 3 | Gravel | Grass ✗ |

---

## Tasks

### i02.1 — Update `TownMapCondenser.BuildSurface()`

File: `src/Oravey2.MapGen/Generation/TownMapCondenser.cs`

- [ ] Replace magic integers with `(int)SurfaceType.Xxx` casts:
  - `0` → `(int)SurfaceType.Dirt`
  - Where currently `1` (Grass) → `(int)SurfaceType.Grass` (= 3)
  - Where currently `2` (Concrete) → `(int)SurfaceType.Concrete` (= 2)
  - Where currently `3` (Gravel) → pick closest: `(int)SurfaceType.Rock`
    (= 6) or add a `Gravel` value to `SurfaceType`
- [ ] Update `ApplyRoadTiles` to use `(int)SurfaceType.Asphalt` for
  paved roads or `(int)SurfaceType.Concrete` as appropriate

### i02.2 — Evaluate adding `Gravel` to `SurfaceType`

File: `src/Oravey2.Core/World/SurfaceType.cs`

- [ ] If Gravel is a distinct gameplay surface, add it:
  ```csharp
  public enum SurfaceType : byte
  {
      Dirt = 0, Asphalt = 1, Concrete = 2, Grass = 3,
      Sand = 4, Mud = 5, Rock = 6, Metal = 7,
      Gravel = 8,
  }
  ```
- [ ] If Gravel is just visual and behaves like Rock, map to `Rock`
  instead — no enum change needed

### i02.3 — Update any other MapGen surface writers

- [ ] Search for other places in MapGen that write integer surface
  values (e.g., `WildernessChunkGenerator`, `TownChunkGenerator`)
- [ ] These likely already use `SurfaceType` directly (they produce
  `TileData` objects) — confirm and fix if not

### i02.4 — Tests

- [ ] Unit test: `TownMapCondenser` output surface values are all
  valid `SurfaceType` enum members
- [ ] Unit test: round-trip — condense a layout, read back surface
  array, cast each to `SurfaceType` without `ArgumentOutOfRange`
- [ ] Build both projects

---

## Files changed

| File | Action |
|------|--------|
| `TownMapCondenser.cs` | **Modify** — use `SurfaceType` casts |
| `SurfaceType.cs` | **Possibly modify** — add `Gravel` |
| Surface-related tests | **New or extend** |
