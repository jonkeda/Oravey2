# Step i01 — Unify LinearFeatureType Enum

**Design doc:** QA-review §6
**Depends on:** None
**Deliverable:** Single `LinearFeatureType` enum used by both MapGen
and Core. `RoadClass` enum deleted.

---

## Goal

Eliminate the `RoadClass` ↔ `LinearFeatureType` mapping layer.
Both the pipeline tool and the game should use one enum with gapped
numeric values for future extensibility.

---

## Tasks

### i01.1 — Extend `LinearFeatureType` with road classification

File: `src/Oravey2.Core/World/LinearFeatureType.cs`

- [ ] Replace the current enum with gapped values:
  ```csharp
  public enum LinearFeatureType : byte
  {
      Path = 0,
      Residential = 1,
      Tertiary = 2,
      Secondary = 3,
      Primary = 4,
      Trunk = 5,
      Motorway = 6,
      Rail = 10,
      Stream = 20,
      River = 21,
      Canal = 22,
      Pipeline = 30,
  }
  ```
- [ ] Delete the old values `DirtRoad`, `Road`, `Highway` (breaking
  change — no backward compat needed)

### i01.2 — Delete `RoadClass` enum

File: `src/Oravey2.MapGen/RegionTemplates/RoadSegment.cs` (or
wherever `RoadClass` is defined)

- [ ] Delete the `RoadClass` enum
- [ ] Change `RoadSegment.RoadClass` property type to
  `LinearFeatureType`

### i01.3 — Update all MapGen references

- [ ] `RoadSelector` — use `LinearFeatureType` instead of `RoadClass`
- [ ] `OverworldFiles.RoadFile` — change `RoadClass` string to
  `LinearFeatureType`, keep `JsonStringEnumConverter`
- [ ] `CullSettings.RoadMinClass` — change type to
  `LinearFeatureType`
- [ ] Any OSM parser code that creates `RoadClass` values

### i01.4 — Update all Core references

- [ ] `WorldGenerator` — already uses `LinearFeatureType`, update
  values (`DirtRoad` → `Residential`, `Road` → `Secondary`,
  `Highway` → `Motorway`)
- [ ] `ChunkStreamingProcessor` or terrain renderer if they read
  the type
- [ ] DB serialization in `WorldMapStore` — stores as integer,
  no schema change needed

### i01.5 — Tests

- [ ] Unit test: verify all enum values serialize/deserialize via
  `JsonStringEnumConverter`
- [ ] Unit test: verify `LinearFeatureType.Motorway > LinearFeatureType.Residential`
  (ordering makes sense for road importance filtering)
- [ ] Build both projects with zero errors

---

## Files changed

| File | Action |
|------|--------|
| `LinearFeatureType.cs` | **Modify** — new values with gaps |
| `RoadSegment.cs` | **Modify** — delete `RoadClass`, use `LinearFeatureType` |
| `RoadSelector.cs` | **Modify** — update type references |
| `OverworldFiles.cs` | **Modify** — change DTO type |
| `CullSettings.cs` | **Modify** — change property type |
| `WorldGenerator.cs` | **Modify** — update enum values |
| OSM parser files | **Modify** — update enum values |
| `LinearFeatureTypeTests.cs` | **New** — serialization + ordering |
