# Step 08 — CLI --cull Flag

**Work streams:** WS-CLI (WorldTemplateTool enhancement)
**Depends on:** Step 01 (CullSettings), Step 02 (FeatureCuller)
**User-testable result:** Run `WorldTemplateTool --cull settings.cullsettings` and the output template contains only the culled subset of features.

---

## Goals

1. Add `--cull <file>` optional argument to the `WorldTemplateTool` CLI.
2. When provided, load `CullSettings` from the JSON file and apply `FeatureCuller` before building the template.
3. When omitted, behaviour is unchanged (all features included).
4. Log culling statistics to stdout.

---

## Problem

The GUI auto-cull is interactive, but the CLI tool currently includes all parsed features in the template. Power users and CI pipelines need a way to apply culling rules from the command line without the GUI.

---

## Tasks

### 8.1 — Add --cull Argument

File: `tools/Oravey2.WorldTemplateTool/Program.cs`

- [ ] Parse `--cull <path>` from command-line arguments
- [ ] Validate the file exists and is valid JSON
- [ ] Deserialize to `CullSettings` using `CullSettings.Load(path)`
- [ ] Print loaded settings summary to stdout

### 8.2 — Apply Culling in Pipeline

- [ ] After `OsmParser` produces features, before `WorldTemplateBuilder`:
  ```csharp
  if (cullSettings != null)
  {
      towns = FeatureCuller.CullTowns(towns, cullSettings);
      roads = FeatureCuller.CullRoads(roads, towns, cullSettings);
      water = FeatureCuller.CullWater(water, cullSettings);
  }
  ```
- [ ] Log before/after counts:
  ```
  Culling: 85 towns → 25, 1200 roads → 347, 300 water → 89
  ```

### 8.3 — Help Text Update

- [ ] Update `--help` output to document the new flag:
  ```
  --cull <file>   Apply culling rules from a .cullsettings JSON file.
                  When omitted, all parsed features are included.
  ```

### 8.4 — Integration Test

File: `tests/Oravey2.Tests/WorldTemplate/WorldTemplateToolCullTests.cs`

- [ ] `CullFlag_ReducesFeatureCount` — build template with and without `--cull`, verify culled template has fewer features
- [ ] `CullFlag_InvalidPath_PrintsError` — nonexistent file → clear error message
- [ ] `CullFlag_InvalidJson_PrintsError` — malformed JSON → clear error message
- [ ] `NoCullFlag_AllFeaturesIncluded` — without flag, all parsed features present

---

## Verify

```bash
cd tools/Oravey2.WorldTemplateTool
dotnet run -- --srtm ../../data/srtm --osm ../../data/noordholland.osm.pbf --region NoordHolland --output test.worldtemplate --cull ../../data/presets/noordholland.cullsettings
```

**User test:** Run the CLI with `--cull` pointing to the Noord-Holland default settings. Output log shows "Culling: 85 towns → 25, 1200 roads → 347, 300 water → 89". The resulting `.worldtemplate` file is smaller than one built without `--cull`. Run without `--cull` → all features included, same as before.
