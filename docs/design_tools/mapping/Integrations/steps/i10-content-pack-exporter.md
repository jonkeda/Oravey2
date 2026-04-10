# Step i10 — ContentPackExporter (Tool-Side)

**Design doc:** 02
**Depends on:** i09 (ContentPackImporter — shares the same JSON
format and DB writing logic)
**Deliverable:** "Export to Game DB" button in the pipeline tool's
Assembly step.

---

## Goal

Add a one-click export from the MapGen pipeline tool that writes the
content pack directly into `world.db`. This is a convenience for
developers — it runs the same logic as `ContentPackImporter` but is
triggered from the tool UI rather than the game startup.

---

## Tasks

### i10.1 — Create `ContentPackExporter`

File: `src/Oravey2.MapGen/Pipeline/ContentPackExporter.cs`

- [ ] Wraps `ContentPackImporter` from Core:
  ```csharp
  public sealed class ContentPackExporter
  {
      public ImportResult Export(string contentPackPath, string dbPath)
      {
          using var store = new WorldMapStore(dbPath);
          var importer = new ContentPackImporter(store);
          return importer.Import(contentPackPath);
      }
  }
  ```
- [ ] Or, if MapGen needs extra logic (e.g., choosing output path,
  validating before export), add that here
- [ ] The point is: no duplicate import logic — the exporter
  delegates to the Core importer

### i10.2 — Add export command to `AssemblyStepViewModel`

File: `src/Oravey2.MapGen/ViewModels/AssemblyStepViewModel.cs`

- [ ] Add `RunExportToDb()` method
- [ ] Default DB path: `{ContentPackPath}/../world.db`
- [ ] Update `StatusText` with result summary
- [ ] Add `ExportToDbCommand` property bound to the method

### i10.3 — Add "Export to Game DB" button to UI

File: `src/Oravey2.MapGen.App/Views/Steps/AssemblyStepView.xaml`

- [ ] Add button below existing action buttons
- [ ] Bind to `ExportToDbCommand`
- [ ] Show export result in status area

### i10.4 — Tests

File: `tests/Oravey2.Tests/Pipeline/ContentPackExporterTests.cs`

- [ ] `Export_CreatesDbFile` — export to temp path → file exists
- [ ] `Export_PopulatesRegion` — export → open DB → region found
- [ ] `Export_DelegatesToImporter` — verify same result as direct
  importer call
- [ ] Build + all tests pass

---

## Files changed

| File | Action |
|------|--------|
| `ContentPackExporter.cs` | **New** in `Oravey2.MapGen/Pipeline/` |
| `AssemblyStepViewModel.cs` | **Modify** — add export command |
| `AssemblyStepView.xaml` | **Modify** — add button |
| `ContentPackExporterTests.cs` | **New** |
