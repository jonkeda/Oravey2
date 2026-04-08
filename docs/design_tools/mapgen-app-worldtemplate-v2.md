# Design: Oravey2.MapGen.App — WorldTemplate Tab & Culling (v2)

## Status: Draft v2

## Changes from v1

- **Data Download** — Source selection now includes download buttons for SRTM elevation tiles and OSM PBF extracts directly from within the app
- **Unified CullSettings** — The three separate settings records (`TownCullSettings`, `RoadCullSettings`, `WaterCullSettings`) are merged into a single `CullSettings` class that serializes to one JSON file
- **Region presets** — The download and cull settings are bundled into `RegionPreset` files so adding new regions is drop-in

---

## Overview

This design extends the existing **Oravey2.MapGen.App** (.NET MAUI desktop tool) with:

1. A **WorldTemplate** tab for creating `.worldtemplate` files from real-world data interactively
2. Built-in **data download** for SRTM elevation and OSM PBF extracts
3. An interactive **culling mechanism** with a unified settings file

---

## Current State

The MapGen.App is a `.NET MAUI` Windows desktop app (900×700, dark theme) with a `TabbedPage`:

| # | Tab | Status |
|---|-----|--------|
| 1 | **Generate** | Disabled — old LLM blueprint pipeline removed |
| 2 | **Preview** | Active — JSON blueprint preview with stats |
| 3 | **Houses** | Active — Meshy AI text/image → 3D buildings |
| 4 | **Figures** | Active — Meshy AI text/image → 3D characters |
| 5 | **Settings** | Active — API keys, export paths |

---

## Proposed Tab Layout

| # | Tab | Status |
|---|-----|--------|
| 1 | **WorldTemplate** | **NEW** — Download data, parse, cull, build template |
| 2 | **Generate** | Repurposed — `WorldGenerator` against a template |
| 3 | **Preview** | Existing |
| 4 | **Houses** | Existing |
| 5 | **Figures** | Existing |
| 6 | **Settings** | Existing |

---

## WorldTemplate Tab Layout

```
┌─────────────────────────────────────────────────────────────┐
│  WORLDTEMPLATE                                              │
│                                                             │
│  ┌─── Region & Data Sources ──────────────────────────────┐ │
│  │ Region Preset: [Noord-Holland ▾]     [Save Preset…]    │ │
│  │                                                        │ │
│  │ SRTM Tiles:  [data/srtm               ] [Browse] [⬇]  │ │
│  │   Status: 4 tiles found (N52E004, N52E005, N53E004…)   │ │
│  │                                                        │ │
│  │ OSM PBF:    [data/noordholland.osm.pbf] [Browse] [⬇]  │ │
│  │   Status: 177 MB, last modified 2026-04-01             │ │
│  │                                                        │ │
│  │ Region Name: [NoordHolland]                            │ │
│  │                                       [Parse Data]     │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                             │
│  ┌─── Map Preview ── Towns ── Roads ── Water ─────────────┐ │
│  │  ┌──────────────────────────┐ ┌──────────────────────┐ │ │
│  │  │                          │ │ ☑ Purmerend   Town   │ │ │
│  │  │    Map canvas with       │ │   pop: 81,000       │ │ │
│  │  │    elevation background, │ │ ☑ Alkmaar    Town   │ │ │
│  │  │    towns, roads, water   │ │   pop: 109,000      │ │ │
│  │  │                          │ │ ☐ Wormer     Hamlet  │ │ │
│  │  │    Click to select.      │ │   pop: 1,200        │ │ │
│  │  │    Pan/zoom with mouse.  │ │ ☑ Zaandam    Town   │ │ │
│  │  │                          │ │   pop: 77,000       │ │ │
│  │  │                          │ │ ☐ Jisp       Hamlet  │ │ │
│  │  │                          │ │                      │ │ │
│  │  │                          │ │ [Select All] [None]  │ │ │
│  │  │                          │ │ [Auto-cull…]         │ │ │
│  │  └──────────────────────────┘ └──────────────────────┘ │ │
│  │  Filter: [All ▾]  Search: [       ]                    │ │
│  │  Show: ☑ Towns ☑ Roads ☑ Water ☐ Railways ☐ Land Use  │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                             │
│  ┌─── Build ──────────────────────────────────────────────┐ │
│  │ Output: [content/noordholland.worldtemplate] [Browse…]  │ │
│  │ Summary: 25 towns · 347 roads · 89 water              │ │
│  │          (culled: 60 towns · 900 roads)                │ │
│  │                                  [Build WorldTemplate] │ │
│  │ ┌──────────────────────────────────────────────┐       │ │
│  │ │ > Parsing SRTM tiles… 4 found               │       │ │
│  │ │ > Parsing OSM data… 85 towns, 1247 roads    │       │ │
│  │ │ > Auto-cull applied: 25 towns, 347 roads    │       │ │
│  │ │ > Building template…                        │       │ │
│  │ │ > Wrote 42.3 MB                             │       │ │
│  │ └──────────────────────────────────────────────┘       │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

## Section 1: Region & Data Sources

### Region Preset Dropdown

A dropdown at the top selects a `RegionPreset` which pre-fills all fields (data paths, download URLs, region name, cull settings). Ships with one built-in preset; more can be added as JSON files in `data/presets/`.

### Data Download

Each data source (SRTM, OSM) has three controls: a path field, a Browse button, and a **Download** button (⬇).

#### SRTM Download

```
┌─── Download SRTM Elevation Data ─────────────────────┐
│                                                       │
│  Source: NASA Earthdata (SRTM 1-arcsecond)            │
│                                                       │
│  Bounding box (from region preset):                   │
│  North: [53.0]  South: [52.0]                         │
│  East:  [5.5]   West:  [4.0]                          │
│                                                       │
│  Tiles needed:                                        │
│    N52E004.hgt  ☑ (exists)                            │
│    N52E005.hgt  ☐ (missing)                           │
│    N53E004.hgt  ☐ (missing)                           │
│    N53E005.hgt  ☐ (missing)                           │
│                                                       │
│  Download to: [data/srtm        ] [Browse]            │
│                                                       │
│  ████████████████░░░░░░░░  2/4 tiles  48%             │
│                                                       │
│                          [Download Missing] [Cancel]  │
│                                                       │
│  ⓘ Requires a free NASA Earthdata account.            │
│    Username: [           ]                            │
│    Password: [           ]                            │
│    [Save credentials]                                 │
│                                                       │
└───────────────────────────────────────────────────────┘
```

**SRTM download pipeline:**

1. Compute required tile names from the bounding box: `N{lat}E{lon}.hgt` for each 1°×1° cell
2. Check which tiles already exist in the target directory
3. Download missing tiles from `https://e4ftl01.cr.usgs.gov/MEASURES/SRTMGL1.003/2000.02.11/N{lat}E{lon}.SRTMGL1.hgt.zip`
4. Requires NASA Earthdata authentication (username/password → Bearer token via OAuth2)
5. Unzip `.hgt` files to target directory
6. Progress bar per tile

**Credentials** are stored in MAUI `SecureStorage` (Windows DPAPI-backed), same pattern as the Meshy API key in Settings.

#### OSM PBF Download

```
┌─── Download OSM Extract ─────────────────────────────┐
│                                                       │
│  Source: Geofabrik (updated daily)                    │
│                                                       │
│  Region: Noord-Holland                                │
│  URL: download.geofabrik.de/europe/netherlands/       │
│       noord-holland-latest.osm.pbf                    │
│  Size: ~177 MB                                        │
│                                                       │
│  Download to: [data/noordholland.osm.pbf] [Browse]    │
│                                                       │
│  ████████████████████████░░░░░░  142/177 MB  80%      │
│                                                       │
│                             [Download] [Cancel]       │
│                                                       │
│  ⓘ No account required. Data © OpenStreetMap          │
│    contributors, ODbL 1.0 license.                    │
│                                                       │
└───────────────────────────────────────────────────────┘
```

**OSM download pipeline:**

1. URL comes from the region preset (e.g., `https://download.geofabrik.de/europe/netherlands/noord-holland-latest.osm.pbf`)
2. `HttpClient` with progress reporting via `IProgress<long>`
3. Download to a temp file, rename on completion (atomic replace)
4. No authentication required
5. Show file size, download speed, ETA

#### Download Service

```csharp
namespace Oravey2.MapGen.Download;

public interface IDataDownloadService
{
    Task DownloadSrtmTilesAsync(
        SrtmDownloadRequest request,
        IProgress<DownloadProgress> progress,
        CancellationToken ct = default);

    Task DownloadOsmExtractAsync(
        OsmDownloadRequest request,
        IProgress<DownloadProgress> progress,
        CancellationToken ct = default);
}

public record SrtmDownloadRequest(
    double NorthLat, double SouthLat,
    double EastLon, double WestLon,
    string TargetDirectory,
    string? EarthdataUsername = null,
    string? EarthdataPassword = null);

public record OsmDownloadRequest(
    string DownloadUrl,
    string TargetFilePath);

public record DownloadProgress(
    string FileName,
    long BytesDownloaded,
    long TotalBytes,
    int FilesCompleted,
    int TotalFiles);
```

---

## Section 2: Map Preview & Culling

Same as v1 — inner tabs for Towns/Roads/Water/Railways/Land Use with a shared map canvas (left) and feature list panel (right). See v1 for full details.

#### Map Canvas

- **GraphicsView** with custom `IDrawable`
- Layers: elevation heightmap → land use → water → railways → roads → towns → labels
- Pan & zoom via mouse drag + scroll
- Click = select in list (bidirectional)
- Included features: green/normal colour. Excluded: dimmed red. Selected: white ring.

#### Feature Lists

Scrollable lists with include/exclude checkboxes, sortable columns, search/filter.

---

## Section 3: Culling Mechanism

### Unified CullSettings

All culling rules live in a single `CullSettings` class. This is serialized to one JSON file and stored alongside the region preset or saved independently.

```csharp
namespace Oravey2.MapGen.WorldTemplate;

/// <summary>
/// All culling rules for reducing raw OSM data to a gameplay-relevant subset.
/// Serialized as a single JSON file (.cullsettings).
/// </summary>
public record CullSettings
{
    // ---- Town culling ----
    public TownCategory TownMinCategory { get; init; } = TownCategory.Village;
    public int TownMinPopulation { get; init; } = 1_000;
    public double TownMinSpacingKm { get; init; } = 5.0;
    public int TownMaxCount { get; init; } = 30;
    public CullPriority TownPriority { get; init; } = CullPriority.Category;
    public bool TownAlwaysKeepCities { get; init; } = true;
    public bool TownAlwaysKeepMetropolis { get; init; } = true;

    // ---- Road culling ----
    public RoadClass RoadMinClass { get; init; } = RoadClass.Primary;
    public bool RoadAlwaysKeepMotorways { get; init; } = true;
    public bool RoadKeepNearTowns { get; init; } = true;
    public double RoadTownProximityKm { get; init; } = 2.0;
    public bool RoadRemoveDeadEnds { get; init; } = true;
    public double RoadDeadEndMinKm { get; init; } = 1.0;
    public bool RoadSimplifyGeometry { get; init; } = true;
    public double RoadSimplifyToleranceM { get; init; } = 50.0;

    // ---- Water culling ----
    public double WaterMinAreaKm2 { get; init; } = 0.1;
    public double WaterMinRiverLengthKm { get; init; } = 2.0;
    public bool WaterAlwaysKeepSea { get; init; } = true;
    public bool WaterAlwaysKeepLakes { get; init; } = true;

    // ---- Serialization ----
    public static CullSettings Load(string path)
        => JsonSerializer.Deserialize<CullSettings>(
            File.ReadAllText(path)) ?? new();

    public void Save(string path)
        => File.WriteAllText(path,
            JsonSerializer.Serialize(this, new JsonSerializerOptions
            { WriteIndented = true }));
}

public enum CullPriority { Population, Category, Spacing }
```

**File format:** `.cullsettings` (JSON)

```json
{
  "townMinCategory": "Village",
  "townMinPopulation": 1000,
  "townMinSpacingKm": 5.0,
  "townMaxCount": 30,
  "townPriority": "Category",
  "townAlwaysKeepCities": true,
  "townAlwaysKeepMetropolis": true,
  "roadMinClass": "Primary",
  "roadAlwaysKeepMotorways": true,
  "roadKeepNearTowns": true,
  "roadTownProximityKm": 2.0,
  "roadRemoveDeadEnds": true,
  "roadDeadEndMinKm": 1.0,
  "roadSimplifyGeometry": true,
  "roadSimplifyToleranceM": 50.0,
  "waterMinAreaKm2": 0.1,
  "waterMinRiverLengthKm": 2.0,
  "waterAlwaysKeepSea": true,
  "waterAlwaysKeepLakes": true
}
```

### Auto-Cull Dialog

The dialog shows the unified settings, organized by section headers (Town / Road / Water):

```
┌─── Auto-Cull Settings ──────────────────────────────┐
│                                                      │
│  TOWN CULLING                                        │
│  ─────────────                                       │
│  Minimum category:        [Village ▾]                │
│  Minimum population:      [1000        ]             │
│  Minimum spacing (km):    [5.0         ]             │
│  Maximum towns:           [30          ]             │
│  Always keep categories:  ☑ City  ☑ Metropolis       │
│  Priority when culling:   ● Category ○ Pop ○ Spacing │
│                                                      │
│  ROAD CULLING                                        │
│  ────────────                                        │
│  Minimum road class:      [Primary ▾]                │
│  Keep roads near towns:   ☑  (within [2.0] km)       │
│  Always keep motorways:   ☑                          │
│  Simplify geometry:       ☑  (tolerance [50] m)       │
│  Remove dead-ends:        ☑  (shorter than [1.0] km) │
│                                                      │
│  WATER CULLING                                       │
│  ─────────────                                       │
│  Minimum area (km²):     [0.1         ]             │
│  Minimum river length:   [2.0  km     ]             │
│  Always keep:            ☑ Sea  ☑ Lake               │
│                                                      │
│  [Load…] [Save…]    [Preview]  [Apply]  [Cancel]    │
└──────────────────────────────────────────────────────┘
```

**Load/Save** buttons allow importing/exporting `.cullsettings` JSON files. The **Preview** button applies rules without committing. **Apply** commits the state to all feature checkboxes.

### Culling Rules

Same as v1 — applied in order per feature type:

**Towns:** Category filter → Population filter → Protected categories → Spacing enforcement (greedy) → Max cap

**Roads:** Class filter → Protected motorways → Town proximity → Dead-end removal → Geometry simplification (Douglas-Peucker)

**Water:** Area filter → River length → Protected types

**Town→road dependency:** Toggling a town off re-evaluates roads near that town. Toggling a town on includes nearby roads above `RoadMinClass`.

### FeatureCuller

```csharp
namespace Oravey2.MapGen.WorldTemplate;

public static class FeatureCuller
{
    public static List<TownEntry> CullTowns(
        List<TownEntry> towns, CullSettings settings);

    public static List<RoadSegment> CullRoads(
        List<RoadSegment> roads,
        List<TownEntry> includedTowns,
        CullSettings settings);

    public static List<WaterBody> CullWater(
        List<WaterBody> water, CullSettings settings);

    public static Vector2[] SimplifyLine(
        Vector2[] points, double toleranceMetres);
}
```

---

## Region Presets

A `RegionPreset` bundles download URLs, geographic bounds, and default cull settings into one file. Stored as `data/presets/{name}.regionpreset` (JSON).

```csharp
namespace Oravey2.MapGen.WorldTemplate;

public record RegionPreset
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }

    // Geographic bounds
    public required double NorthLat { get; init; }
    public required double SouthLat { get; init; }
    public required double EastLon { get; init; }
    public required double WestLon { get; init; }

    // Download sources
    public required string OsmDownloadUrl { get; init; }
    public string? OsmFileName { get; init; }

    // Default paths
    public string DefaultSrtmDir { get; init; } = "data/srtm";
    public string DefaultOutputDir { get; init; } = "content";

    // Default cull settings
    public CullSettings DefaultCullSettings { get; init; } = new();
}
```

**Built-in preset: Noord-Holland**

```json
{
  "name": "NoordHolland",
  "displayName": "Noord-Holland",
  "northLat": 53.0,
  "southLat": 52.2,
  "eastLon": 5.5,
  "westLon": 4.0,
  "osmDownloadUrl": "https://download.geofabrik.de/europe/netherlands/noord-holland-latest.osm.pbf",
  "osmFileName": "noordholland.osm.pbf",
  "defaultSrtmDir": "data/srtm",
  "defaultOutputDir": "content",
  "defaultCullSettings": {
    "townMinCategory": "Village",
    "townMinPopulation": 1000,
    "townMinSpacingKm": 5.0,
    "townMaxCount": 30,
    "townPriority": "Category",
    "townAlwaysKeepCities": true,
    "townAlwaysKeepMetropolis": true,
    "roadMinClass": "Secondary",
    "roadAlwaysKeepMotorways": true,
    "roadKeepNearTowns": true,
    "roadTownProximityKm": 2.0,
    "roadRemoveDeadEnds": true,
    "roadDeadEndMinKm": 1.0,
    "roadSimplifyGeometry": true,
    "roadSimplifyToleranceM": 50.0,
    "waterMinAreaKm2": 0.01,
    "waterMinRiverLengthKm": 1.0,
    "waterAlwaysKeepSea": true,
    "waterAlwaysKeepLakes": true
  }
}
```

Selecting a preset in the dropdown fills in all paths, URLs, and cull defaults. The user can then customize and optionally **Save Preset…** to create a new `.regionpreset` file.

---

## ViewModel

```csharp
public class WorldTemplateViewModel : BaseViewModel
{
    // Region preset
    public ObservableCollection<RegionPreset> Presets { get; }
    public RegionPreset? SelectedPreset { get; set; }

    // Source inputs
    public string SrtmDirectory { get; set; }
    public string OsmFilePath { get; set; }
    public string RegionName { get; set; }
    public string OutputPath { get; set; }

    // Download state
    public bool IsDownloading { get; set; }
    public DownloadProgress? CurrentDownload { get; set; }

    // Parsed data
    public OsmExtract? ParsedExtract { get; private set; }
    public float[,]? ElevationGrid { get; private set; }

    // Feature collections with Include flag
    public ObservableCollection<TownItem> Towns { get; }
    public ObservableCollection<RoadItem> Roads { get; }
    public ObservableCollection<WaterItem> WaterBodies { get; }

    // Cull settings (single object)
    public CullSettings CullSettings { get; set; } = new();

    // Summary (computed)
    public string Summary { get; }
    public string CulledSummary { get; }

    // Commands
    public IAsyncRelayCommand DownloadSrtmCommand { get; }
    public IAsyncRelayCommand DownloadOsmCommand { get; }
    public IAsyncRelayCommand ParseCommand { get; }
    public ICommand AutoCullCommand { get; }
    public IAsyncRelayCommand BuildCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand SelectNoneCommand { get; }
    public ICommand SavePresetCommand { get; }
    public ICommand LoadCullSettingsCommand { get; }
    public ICommand SaveCullSettingsCommand { get; }

    public string LogText { get; set; }
}
```

---

## Map Canvas Rendering

Same as v1 — MAUI `GraphicsView` with custom `IDrawable`. Rendering order: elevation → land use → water → railways → roads → towns → labels. Included features rendered normally, excluded features dimmed/red, selected features ringed.

---

## Town → Road Dependency

Same as v1 — toggling a town re-evaluates road include states based on proximity. The "Near Town" column in the road list shows which included town anchors each road.

---

## Settings Integration

| Key | Type | Description |
|-----|------|-------------|
| `WorldTemplate_LastPreset` | string | Last selected preset name |
| `WorldTemplate_SrtmDir` | string | Last used SRTM directory |
| `WorldTemplate_OsmFile` | string | Last used OSM file |
| `WorldTemplate_OutputDir` | string | Default output directory |
| `WorldTemplate_CullSettings` | JSON | Serialized `CullSettings` |
| `Earthdata_Username` | SecureStorage | NASA Earthdata username |
| `Earthdata_Password` | SecureStorage | NASA Earthdata password |

---

## Integration with Existing Pipeline

### WorldTemplateTool (CLI)

Continues to work unchanged. Can optionally accept a `--cull <file.cullsettings>` argument to apply the same rules as the GUI.

### WorldGenerator (Game-time)

`TownCurator` and `RoadSelector` still run at game time as a fine pass. Template-level culling is the coarse pass.

### Generate Tab (Repurposed)

Can run `WorldGenerator.GenerateAsync()` against the built template. Separate design.

---

## Implementation Order

| # | Task | New/Modify | Depends |
|---|------|------------|---------|
| 1 | `CullSettings` record + JSON serialization | New in `Oravey2.MapGen` | — |
| 2 | `RegionPreset` record + JSON serialization | New in `Oravey2.MapGen` | Step 1 |
| 3 | `FeatureCuller` — town + road + water culling | New in `Oravey2.MapGen` | Step 1 |
| 4 | `FeatureCuller` — Douglas-Peucker simplification | Part of step 3 | — |
| 5 | Unit tests for `FeatureCuller` + `CullSettings` | New test class | Steps 1–4 |
| 6 | `IDataDownloadService` — SRTM download | New in `Oravey2.MapGen` | — |
| 7 | `IDataDownloadService` — OSM download | New in `Oravey2.MapGen` | — |
| 8 | `WorldTemplateViewModel` | New in MapGen.App | Steps 1–7 |
| 9 | `WorldTemplateView.xaml` — source + download section | New XAML | Step 8 |
| 10 | `WorldTemplateMapDrawable` — canvas rendering | New drawable | Step 8 |
| 11 | Feature list panels (towns, roads, water tabs) | Part of XAML | Steps 8, 10 |
| 12 | Auto-cull dialog with Load/Save | New popup | Steps 1, 8 |
| 13 | Map ↔ list two-way selection | ViewModel logic | Steps 10, 11 |
| 14 | Town→road dependency propagation | ViewModel logic | Steps 8, 11 |
| 15 | Register in `MainPage.xaml` + `MauiProgram.cs` | Modify | Steps 8, 9 |
| 16 | Ship Noord-Holland `.regionpreset` | Data file | Steps 1, 2 |
| 17 | Persist settings + credentials | Modify settings | Step 8 |
| 18 | Optional: `--cull` flag for `WorldTemplateTool` CLI | Modify CLI | Step 1 |

---

## Default Cull Results for Noord-Holland

With the default preset settings:

| Feature | Raw (OSM) | After Cull | Notes |
|---------|-----------|------------|-------|
| Towns | ~85 | ~25 | Amsterdam, Haarlem, Alkmaar, Purmerend, Zaandam, Hoorn, etc. |
| Roads | ~1,200 | ~350 | All motorways (A2, A7, A9) + primary/secondary near kept towns |
| Water | ~300+ | ~90 | IJ, Markermeer coast, major canals, significant lakes |
| Railways | ~50 | ~50 | No culling applied to railways by default |
| Land use | ~200+ | ~200+ | No culling — small overhead, useful for biome mapping |
