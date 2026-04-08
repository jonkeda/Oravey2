# Step 24 — ViewModel & Region Discovery

**Work streams:** WS-ViewModel (UI logic), WS-FileLayout (Directory restructure)
**Depends on:** Step 20 (RegionPreset paths), Step 23 (Download paths)
**User-testable result:** The preset picker shows regions discovered from `data/regions/`. Selecting a preset populates all paths from computed properties. Region folder + `region.json` are created when a new region is selected.

---

## Goals

1. Replace `LoadPresetsFromDirectory("data/presets")` with `LoadPresetsFromRegions()` scanning `data/regions/`.
2. `ApplyPreset` uses `RegionPreset` computed paths instead of assembling paths manually.
3. Selecting a region from the region picker creates the region folder and saves `region.json`.
4. Remove unused VM path properties that are now derived from the preset.

---

## Problem

`LoadPresetsFromDirectory` scans `data/presets/*.regionpreset`. `ApplyPreset` manually constructs paths from `DefaultSrtmDir`, `DefaultOutputDir`, and `OsmFileName`. These are now replaced by `RegionPreset` computed paths and the region folder structure.

---

## Tasks

### 24.1 — Replace LoadPresetsFromDirectory

File: `src/Oravey2.MapGen/ViewModels/WorldTemplateViewModel.cs`

- [ ] Replace `LoadPresetsFromDirectory(string presetsDirectory)` with `LoadPresetsFromRegions()`:
  ```csharp
  public void LoadPresetsFromRegions()
  {
      Presets.Clear();
      var regionsDir = Path.Combine("data", "regions");
      if (!Directory.Exists(regionsDir)) return;

      foreach (var dir in Directory.GetDirectories(regionsDir))
      {
          var presetPath = Path.Combine(dir, "region.json");
          if (File.Exists(presetPath))
              Presets.Add(RegionPreset.Load(presetPath));
      }
  }
  ```

### 24.2 — Simplify ApplyPreset

File: `src/Oravey2.MapGen/ViewModels/WorldTemplateViewModel.cs`

- [ ] Update `ApplyPreset` to use computed paths:
  ```csharp
  private void ApplyPreset(RegionPreset preset)
  {
      SrtmDirectory = preset.SrtmDir;
      OsmFilePath = preset.OsmFilePath;
      RegionName = preset.Name;
      OutputPath = preset.OutputFilePath;
      CullSettings = preset.DefaultCullSettings;

      _parsedExtract = null;
      _elevationGrid = null;
      Towns.Clear();
      Roads.Clear();
      WaterBodies.Clear();
      OnPropertyChanged(nameof(Summary));
      OnPropertyChanged(nameof(CulledSummary));
      MapInvalidated?.Invoke();
  }
  ```
  Changes from current: `SrtmDirectory` was `preset.DefaultSrtmDir`, `OsmFilePath` was `Path.Combine("data", preset.OsmFileName)`, `OutputPath` was `Path.Combine(preset.DefaultOutputDir, ...)`.

### 24.3 — Create Region Folder on Selection

File: `src/Oravey2.MapGen/ViewModels/WorldTemplateViewModel.cs`

- [ ] Update `ApplyRegionPreset` to create region folder and write `region.json`:
  ```csharp
  public void ApplyRegionPreset(RegionPreset preset)
  {
      preset.EnsureDirectories();
      preset.Save(preset.PresetFilePath);

      if (!Presets.Any(p => p.Name == preset.Name))
          Presets.Add(preset);

      SelectedPreset = preset;
  }
  ```

### 24.4 — Update Startup to Use LoadPresetsFromRegions

- [ ] Find where `LoadPresetsFromDirectory("data/presets")` is called and replace with `LoadPresetsFromRegions()`

### 24.5 — Update ParseAsync to Find .hgt.gz Files

File: `src/Oravey2.MapGen/ViewModels/WorldTemplateViewModel.cs`

- [ ] In `ParseAsync`, scan for both `.hgt` and `.hgt.gz`:
  ```csharp
  var hgtFiles = Directory.GetFiles(SrtmDirectory)
      .Where(f => f.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".hgt.gz", StringComparison.OrdinalIgnoreCase))
      .ToArray();
  ```

### 24.6 — Unit Tests

File: `tests/Oravey2.Tests/ViewModels/WorldTemplateViewModelTests.cs`

- [ ] `LoadPresetsFromRegions_FindsRegionJson` — create `data/regions/test/region.json` in temp dir, verify preset loaded
- [ ] `LoadPresetsFromRegions_IgnoresFoldersWithoutJson` — folder without `region.json` is skipped
- [ ] `ApplyPreset_SetsComputedPaths` — apply preset, verify `SrtmDirectory`, `OsmFilePath`, `OutputPath` match computed values
- [ ] `ApplyRegionPreset_CreatesRegionFolder` — call in temp dir, verify folder + `region.json` exist

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~WorldTemplateViewModel"
```

Start the MapGen app. Preset picker shows regions from `data/regions/`. Selecting a region fills all path fields correctly.
