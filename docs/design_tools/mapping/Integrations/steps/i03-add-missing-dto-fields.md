# Step i03 — Add Missing DTO Fields

**Design doc:** Compatibility review
**Depends on:** None
**Deliverable:** MapGen DTOs include all fields Core expects.
No data loss when pipeline JSON files are loaded by the game.

---

## Goal

Close the field gaps identified in the compatibility review. Three
MapGen DTOs are missing fields that Core expects. Add them as
nullable/optional so existing pipeline output stays valid.

---

## Tasks

### i03.1 — Add `InteriorChunkId` to `BuildingFile`

File: `src/Oravey2.MapGen/Generation/TownMapFiles.cs`

- [ ] Add `public string? InteriorChunkId { get; set; }` to
  `BuildingFile`
- [ ] Default to `null` — the pipeline doesn't generate interiors yet
- [ ] Add `string? InteriorChunkId` to `PlacedBuilding` record if
  it flows through the pipeline

### i03.2 — Add `Footprint` to `PropFile`

File: `src/Oravey2.MapGen/Generation/TownMapFiles.cs`

- [ ] Add `public int[][]? Footprint { get; set; }` to `PropFile`
- [ ] Default to `null`
- [ ] If `PlacedProp` should carry it, add there too

### i03.3 — Make `BuildingFile.Footprint` non-nullable

File: `src/Oravey2.MapGen/Generation/TownMapFiles.cs`

- [ ] Change `int[][]?` → `int[][]` (Core expects non-nullable)
- [ ] Ensure all code paths that create `BuildingFile` supply a
  footprint (even if `[[]]` empty)

### i03.4 — Add `Style` to `LinearFeatureData`

File: `src/Oravey2.MapGen/Generation/RegionGenerator.cs` (or
wherever `LinearFeatureData` is defined)

- [ ] Add `string Style` field, default `"default"`
- [ ] Pass through to `LinearFeature` constructor in
  `WorldGenerator` instead of hardcoding `"default"`

### i03.5 — Add `Author`, `EngineVersion`, `Parent` to Core's `ContentManifest`

File: `src/Oravey2.Core/Content/ContentJsonModels.cs`

- [ ] Add nullable fields to match MapGen's `ManifestFile`:
  ```csharp
  record ContentManifest(
      string Id, string Name, string Version,
      string? Description = null,
      string? DefaultScenario = null,
      string[]? Tags = null,
      string? Author = null,
      string? EngineVersion = null,
      string? Parent = null);
  ```
- [ ] These are informational — no runtime behavior change

### i03.6 — Align `ScenarioDefinition` with pipeline's `ScenarioFile`

File: `src/Oravey2.Core/Content/ContentPackService.cs`

- [ ] Add `string[]? Towns`, `PlayerStartInfo? PlayerStart` to
  `ScenarioDefinition`
- [ ] Keep `Map` and `Features` (game may use them for non-pipeline
  scenarios)
- [ ] The definition becomes a superset of both

### i03.7 — Tests

- [ ] Unit test: serialize `BuildingFile` with `InteriorChunkId` →
  deserialize as `BuildingJson` → field present
- [ ] Unit test: serialize `PropFile` with `Footprint` → deserialize
  as `PropJson` → field present
- [ ] Unit test: serialize `ManifestFile` → deserialize as
  `ContentManifest` → all fields survive
- [ ] Build both projects

---

## Files changed

| File | Action |
|------|--------|
| `TownMapFiles.cs` | **Modify** — add fields to `BuildingFile`, `PropFile` |
| `TownMapResult.cs` | **Possibly modify** — add fields to records |
| `RegionGenerator.cs` | **Modify** — add `Style` to `LinearFeatureData` |
| `ContentJsonModels.cs` | **Modify** — add fields to `ContentManifest` |
| `ContentPackService.cs` | **Modify** — add fields to `ScenarioDefinition` |
| DTO round-trip tests | **New or extend** |
