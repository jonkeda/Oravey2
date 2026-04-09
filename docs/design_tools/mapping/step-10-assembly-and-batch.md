# Step 10 — Content Pack Assembly & Validation

## Goal

Tie everything together: generate metadata files, validate referential
integrity, and produce a complete content pack ready for game loading. Also
add batch-mode polish.

## Deliverables

### 10.1 `ContentPackAssembler` service

```csharp
public sealed class ContentPackAssembler
{
    // Generate or update the scenario file linking all towns
    public void GenerateScenario(string contentPackPath, List<CuratedTown> towns);

    // Rebuild catalog.json from all assets in the pack
    public void RebuildCatalog(string contentPackPath);

    // Update manifest.json with current version/metadata
    public void UpdateManifest(string contentPackPath, ManifestUpdate update);

    // Validate all cross-references
    public ValidationResult Validate(string contentPackPath);
}
```

### 10.2 Scenario generation

`scenarios/noord-holland.json`:

```json
{
  "id": "noord-holland",
  "name": "Noord-Holland Wastes",
  "description": "Survive the flooded polders and ruined cities of Noord-Holland...",
  "towns": ["havenburg", "marsdiep", ...],
  "playerStart": {
    "town": "haarlem-haven",
    "tileX": 5,
    "tileY": 5
  },
  "difficulty": 3,
  "tags": ["exploration", "coastal", "dutch"]
}
```

### 10.3 Validation checks

| Check | Rule |
|-------|------|
| Every town in `curated-towns.json` has a `towns/{name}/design.json` | All designed |
| Every designed town has `layout.json`, `buildings.json`, `props.json`, `zones.json` | All mapped |
| Every `meshAsset` reference in `buildings.json` and `props.json` exists as a `.glb` file | No broken mesh refs |
| Every `.glb` in `assets/meshes/` has a `.meta.json` | No orphan meshes |
| `catalog.json` includes all accepted assets | Catalog complete |
| `manifest.json` exists with valid `parent` chain | Pack hierarchy valid |
| `scenarios/*.json` references only existing towns | Scenario valid |

Returns `ValidationResult` with pass/warn/fail items.

### 10.4 `AssemblyStepView` / `AssemblyStepViewModel`

- **Checklist** — each validation item with status icon
- **[Generate Scenario]** / **[Rebuild Catalog]** / **[Update Manifest]**
  buttons
- **[Validate]** button — runs all checks, displays results
- **[Build Package]** button — runs `dotnet pack` on the `.csproj`, shows
  output
- **[Open in Explorer]** — opens the content pack folder in the file manager

### 10.5 Batch mode improvements (cross-step)

Now that all steps exist, add polish:

- Pipeline-wide **[Run All Remaining]** button that chains steps 05–08
  (parse → select → design all → generate all maps)
- Step 09 **[Generate All Pending]** remains separate (Meshy API is slow
  and expensive)
- Each batch shows: `Processing: 7/12 ████████░░░░ Marsdiep…`
- All batches are cancellable via CancellationToken

### 10.6 Tests

- `ContentPackAssembler.Validate` — set up a content pack with known missing
  files, verify correct warnings/errors
- `GenerateScenario` — verify output JSON structure
- `RebuildCatalog` — verify all `.glb` files are catalogued
- Integration test: full pipeline from test data → validated pack

## Dependencies

- All previous steps (this is the final step)

## Estimated scope

- New files: `ContentPackAssembler.cs`, `ValidationResult.cs`, view + VM
- Modified: possibly `PipelineWizardViewModel` for cross-step batch button
