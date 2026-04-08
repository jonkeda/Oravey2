# Step 04 — ViewModel & Settings Persistence

**Work streams:** WS-ViewModel (MVVM bindings and state management)
**Depends on:** Step 01 (CullSettings, RegionPreset), Step 02 (FeatureCuller), Step 03 (IDataDownloadService)
**User-testable result:** Unit tests pass for ViewModel commands and settings persistence. All state survives app restart.

---

## Goals

1. Create `WorldTemplateViewModel` with all commands, collections, and bindable properties.
2. Persist user settings (paths, last preset, cull settings) via MAUI `Preferences`.
3. Persist NASA Earthdata credentials via `SecureStorage`.
4. Wire preset selection to auto-fill all fields.
5. Wire Parse, Auto-Cull, and Build commands.

---

## Problem

The WorldTemplate tab needs a ViewModel to manage state: selected preset, data source paths, parsed features, include/exclude flags, cull settings, download progress, build log. The existing app uses `BaseViewModel` with `SetProperty<T>` and MAUI `Preferences`/`SecureStorage`.

---

## Tasks

### 4.1 — Feature Item Wrappers

File: `src/Oravey2.MapGen.App/ViewModels/WorldTemplate/TownItem.cs`

- [ ] Create `TownItem` class inheriting `BaseViewModel`:
  - `TownEntry Entry` (source data)
  - `bool IsIncluded` (checkbox state, default true)
  - `bool IsSelected` (list selection state)
  - Display properties: `Name`, `Category`, `Population`, `Lat`, `Lon`

File: `src/Oravey2.MapGen.App/ViewModels/WorldTemplate/RoadItem.cs`

- [ ] Create `RoadItem`:
  - `RoadSegment Segment`
  - `bool IsIncluded`, `bool IsSelected`
  - `string NearTown` — name of nearest included town (empty if none)
  - Display: `Classification`, `PointCount`, `LengthKm`

File: `src/Oravey2.MapGen.App/ViewModels/WorldTemplate/WaterItem.cs`

- [ ] Create `WaterItem`:
  - `WaterBody Body`
  - `bool IsIncluded`, `bool IsSelected`
  - Display: `Name`, `Type`, `AreaKm2`

### 4.2 — WorldTemplateViewModel

File: `src/Oravey2.MapGen.App/ViewModels/WorldTemplateViewModel.cs`

- [ ] Inherit `BaseViewModel`
- [ ] Constructor takes `IDataDownloadService` (injected)

**Preset properties:**
- [ ] `ObservableCollection<RegionPreset> Presets` — loaded from `data/presets/*.regionpreset`
- [ ] `RegionPreset? SelectedPreset` — on change, fill all fields from preset

**Source properties:**
- [ ] `string SrtmDirectory`
- [ ] `string OsmFilePath`
- [ ] `string RegionName`
- [ ] `string OutputPath`

**Download state:**
- [ ] `bool IsDownloading`
- [ ] `DownloadProgress? CurrentDownload`
- [ ] `double DownloadPercent` (computed: `BytesDownloaded / TotalBytes * 100`)

**Parsed data (private backing):**
- [ ] `OsmExtract? ParsedExtract` — result from OsmParser
- [ ] `float[,]? ElevationGrid` — result from SrtmParser

**Feature collections:**
- [ ] `ObservableCollection<TownItem> Towns`
- [ ] `ObservableCollection<RoadItem> Roads`
- [ ] `ObservableCollection<WaterItem> WaterBodies`

**Cull settings:**
- [ ] `CullSettings CullSettings` — single property, bindable

**Summary (computed):**
- [ ] `string Summary` — e.g., "25 towns · 347 roads · 89 water"
- [ ] `string CulledSummary` — e.g., "(culled: 60 towns · 900 roads)"

**Log:**
- [ ] `string LogText` — appended during parse/build operations

### 4.3 — Commands

- [ ] `IAsyncRelayCommand DownloadSrtmCommand` — calls `IDataDownloadService.DownloadSrtmTilesAsync`
- [ ] `IAsyncRelayCommand DownloadOsmCommand` — calls `IDataDownloadService.DownloadOsmExtractAsync`
- [ ] `IAsyncRelayCommand ParseCommand` — runs `OsmParser` + `SrtmParser`, populates collections
- [ ] `ICommand AutoCullCommand` — applies `FeatureCuller` with current `CullSettings`, sets `IsIncluded` flags
- [ ] `IAsyncRelayCommand BuildCommand` — builds template from included features via `WorldTemplateBuilder`
- [ ] `ICommand SelectAllCommand` — sets all `IsIncluded = true` for active feature type
- [ ] `ICommand SelectNoneCommand` — sets all `IsIncluded = false`
- [ ] `ICommand SavePresetCommand` — saves current state as `.regionpreset` file
- [ ] `ICommand LoadCullSettingsCommand` — file picker → `CullSettings.Load()`
- [ ] `ICommand SaveCullSettingsCommand` — file picker → `CullSettings.Save()`

### 4.4 — Preset Selection Logic

- [ ] When `SelectedPreset` changes:
  1. Set `SrtmDirectory` = preset.DefaultSrtmDir
  2. Set `OsmFilePath` = `Path.Combine("data", preset.OsmFileName)`
  3. Set `RegionName` = preset.Name
  4. Set `OutputPath` = `Path.Combine(preset.DefaultOutputDir, $"{preset.Name}.worldtemplate")`
  5. Set `CullSettings` = preset.DefaultCullSettings
  6. Clear parsed data and feature collections

### 4.5 — Parse Command Logic

- [ ] Validate `SrtmDirectory` exists and contains `.hgt` files
- [ ] Validate `OsmFilePath` exists
- [ ] Run on background thread (`Task.Run`):
  1. Parse SRTM → `ElevationGrid` (log tile count)
  2. Parse OSM → `ParsedExtract` (log town/road/water counts)
  3. Populate `Towns`, `Roads`, `WaterBodies` collections (back on UI thread)
  4. Auto-apply cull settings if enabled
- [ ] Update `Summary` and `CulledSummary`

### 4.6 — Build Command Logic

- [ ] Collect included features:
  ```csharp
  var towns = Towns.Where(t => t.IsIncluded).Select(t => t.Entry).ToList();
  var roads = Roads.Where(r => r.IsIncluded).Select(r => r.Segment).ToList();
  var water = WaterBodies.Where(w => w.IsIncluded).Select(w => w.Body).ToList();
  ```
- [ ] Build `RegionTemplate` and write via `WorldTemplateBuilder`
- [ ] Log output file size and path

### 4.7 — Settings Persistence

- [ ] On property change, persist to `Preferences`:
  ```
  WorldTemplate_LastPreset → SelectedPreset.Name
  WorldTemplate_SrtmDir → SrtmDirectory
  WorldTemplate_OsmFile → OsmFilePath
  WorldTemplate_OutputDir → OutputPath
  WorldTemplate_CullSettings → JsonSerializer.Serialize(CullSettings)
  ```
- [ ] On ViewModel construction, restore from `Preferences`
- [ ] NASA Earthdata credentials: `SecureStorage.SetAsync` / `GetAsync`
  ```
  Earthdata_Username
  Earthdata_Password
  ```

### 4.8 — Unit Tests

File: `tests/Oravey2.Tests/MapGenApp/WorldTemplateViewModelTests.cs`

- [ ] `PresetSelection_FillsAllFields` — select Noord-Holland, verify paths and cull settings
- [ ] `PresetSelection_ClearsOldData` — select preset, parse, select different preset, collections empty
- [ ] `AutoCull_AppliesSettings` — populate towns, cull, verify IsIncluded flags match FeatureCuller output
- [ ] `SelectAll_SetsAllIncluded` — all items IsIncluded=true
- [ ] `SelectNone_ClearsAllIncluded` — all items IsIncluded=false
- [ ] `Summary_ReflectsIncludedCounts` — verify summary text matches included count

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~WorldTemplateViewModel"
```

**User test:** Settings survive app restart — close and reopen app, last preset and paths are restored.
