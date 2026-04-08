# Design: Region Picker — OSM Data Source Browser

## Status: Draft

---

## Overview

Replace the hardcoded region preset dropdown with a **searchable, hierarchical region picker** powered by [Geofabrik's](https://download.geofabrik.de) machine-readable index. When the user picks a region, the app auto-generates a `RegionPreset` with the correct bounding box and OSM download URL — no manual JSON editing required.

---

## Data Source

Geofabrik publishes two JSON index files (public, no auth):

| File | URL | Size | Contents |
|------|-----|------|----------|
| Full (with geometry) | `https://download.geofabrik.de/index-v1.json` | ~25 MB | GeoJSON FeatureCollection with boundary polygons |
| No geometry | `https://download.geofabrik.de/index-v1-nogeom.json` | ~150 KB | Same metadata, no geometry — **use this for the tree** |

### Feature Schema

Each feature in the collection:

```json
{
    "type": "Feature",
    "properties": {
        "id": "noord-holland",
        "parent": "netherlands",
        "iso3166-1:alpha2": ["NL"],   // or iso3166-2 for sub-regions
        "name": "Noord-Holland",
        "urls": {
            "pbf": "https://download.geofabrik.de/europe/netherlands/noord-holland-latest.osm.pbf",
            "shp": "https://...shp.zip",
            "updates": "https://...updates"
        }
    },
    "geometry": { ... }  // only in index-v1.json
}
```

### Hierarchy

Regions form a tree via the `parent` field:

```
(root — no parent)
├── africa
│   ├── algeria
│   ├── angola
│   └── ...
├── asia
│   ├── afghanistan
│   ├── china
│   │   ├── anhui
│   │   ├── beijing
│   │   └── ...
│   ├── japan
│   │   ├── chubu
│   │   ├── hokkaido
│   │   └── ...
│   └── ...
├── europe
│   ├── netherlands
│   │   ├── flevoland
│   │   ├── gelderland
│   │   ├── noord-holland    ← current preset
│   │   ├── zuid-holland
│   │   └── ...
│   ├── germany
│   │   ├── bayern
│   │   │   ├── mittelfranken
│   │   │   ├── oberbayern
│   │   │   └── ...
│   │   └── ...
│   └── ...
├── north-america
│   ├── canada
│   │   ├── alberta
│   │   └── ...
│   ├── us
│   │   ├── us/california
│   │   │   ├── norcal
│   │   │   └── socal
│   │   └── ...
│   └── ...
├── south-america
├── central-america
├── australia-oceania
└── russia
    ├── central-fed-district
    ├── siberian-fed-district
    └── ...
```

Top-level continents have no `parent`. Depth can be up to 5 levels (e.g. `europe` → `germany` → `nordrhein-westfalen` → `arnsberg-regbez`).

---

## UI Wireframe

The region picker replaces the single `[Noord-Holland ▾]` Picker with a two-part layout:

```
┌─── Region & Data Sources ─────────────────────────────────────┐
│                                                               │
│  Region: [Noord-Holland               ] [Browse…] [Save…]    │
│                                                               │
│  SRTM Tiles:  [data/srtm               ] [Browse] [⬇]       │
│  OSM PBF:     [data/noord-holland.osm.pbf] [Browse] [⬇]     │
│                                                               │
└───────────────────────────────────────────────────────────────┘
```

Clicking **[Browse…]** opens a modal dialog:

```
┌─── Select Region ──────────────────────────────────────┐
│                                                        │
│  Search: [___________________________] 🔍              │
│                                                        │
│  ┌────────────────────────────────────────────────┐    │
│  │ ▶ Africa                                       │    │
│  │ ▶ Asia                                         │    │
│  │ ▶ Australia and Oceania                        │    │
│  │ ▶ Central America                              │    │
│  │ ▼ Europe                                       │    │
│  │   ▶ Albania                                    │    │
│  │   ▶ Austria                                    │    │
│  │   ...                                          │    │
│  │   ▼ Netherlands                                │    │
│  │     ● Flevoland                                │    │
│  │     ● Gelderland                               │    │
│  │     ● Noord-Holland  ← selected                │    │
│  │     ● Zuid-Holland                             │    │
│  │   ...                                          │    │
│  │ ▶ North America                                │    │
│  │ ▶ Russian Federation                           │    │
│  │ ▶ South America                                │    │
│  └────────────────────────────────────────────────┘    │
│                                                        │
│  Selected: Noord-Holland                               │
│  PBF URL:  https://download.geofabrik.de/...           │
│  Parent:   Netherlands → Europe                        │
│                                                        │
│           [Cancel]                    [Select]         │
└────────────────────────────────────────────────────────┘
```

### Interactions

| Action | Behavior |
|--------|----------|
| **▶ / ▼ expand** | Toggle children visible in the tree |
| **Click leaf / node** | Select that region (highlight, show details below) |
| **Search** | Filter tree to nodes matching text (expand parents of matches) |
| **Select button** | Close dialog, populate `RegionPreset` from selected region |
| **Cancel** | Close dialog, no change |

---

## Data Model

### GeofabrikRegion (new)

```csharp
namespace Oravey2.MapGen.WorldTemplate;

/// <summary>
/// A region from the Geofabrik index, used to build the picker tree.
/// </summary>
public record GeofabrikRegion
{
    public required string Id { get; init; }
    public string? Parent { get; init; }
    public required string Name { get; init; }
    public string? PbfUrl { get; init; }
    public string[]? Iso3166Alpha2 { get; init; }
    public string[]? Iso3166_2 { get; init; }
    public List<GeofabrikRegion> Children { get; } = [];
}
```

### GeofabrikIndex (new)

```csharp
/// <summary>
/// Parses and caches the Geofabrik index-v1-nogeom.json.
/// </summary>
public class GeofabrikIndex
{
    public IReadOnlyList<GeofabrikRegion> Roots { get; }          // top-level (no parent)
    public IReadOnlyDictionary<string, GeofabrikRegion> ById { get; }

    public static GeofabrikIndex Parse(string json);
    public IEnumerable<GeofabrikRegion> Search(string query);     // case-insensitive name match
}
```

---

## Bounding Box Strategy

The `index-v1-nogeom.json` does **not** include geometry. Two options for getting bounds:

### Option A: Fetch full index (preferred for accuracy)

Download `index-v1.json` (~25 MB), extract the bounding box from each feature's `geometry` using:

```csharp
var bbox = geometry.Coordinates.Aggregate(
    (minLon: double.Max, minLat: double.Max, maxLon: double.Min, maxLat: double.Min),
    (acc, coord) => (
        Math.Min(acc.minLon, coord.Lon),
        Math.Min(acc.minLat, coord.Lat),
        Math.Max(acc.maxLon, coord.Lon),
        Math.Max(acc.maxLat, coord.Lat)));
```

### Option B: Download PBF header (lighter)

The `.osm.pbf` file header contains a bounding box. Parse just the first few KB after selection to read it — but this requires downloading part of the actual PBF.

### Recommendation

Use **Option A** — download `index-v1.json` once, cache locally, extract bounds per region. The 25 MB download is a one-time cost and gives exact boundary polygons.

Cache the parsed index at `data/geofabrik-index.json` with a timestamp. Refresh if older than 7 days.

---

## RegionPreset Generation

When the user selects a region and clicks **[Select]**, generate:

```csharp
var preset = new RegionPreset
{
    Name = region.Id,                                   // "noord-holland"
    DisplayName = region.Name,                          // "Noord-Holland"
    NorthLat = bbox.MaxLat,                             // from geometry
    SouthLat = bbox.MinLat,
    EastLon = bbox.MaxLon,
    WestLon = bbox.MinLon,
    OsmDownloadUrl = region.PbfUrl,                     // Geofabrik URL
    OsmFileName = $"{region.Id}-latest.osm.pbf",
    DefaultSrtmDir = "data/srtm",
    DefaultOutputDir = "content",
    DefaultCullSettings = new CullSettings()            // defaults
};
```

The generated preset:
- Populates the WorldTemplate tab fields immediately
- Can be saved via **[Save Preset…]** to `data/presets/{id}.regionpreset`
- Is **not** saved automatically — user decides whether to persist

---

## Service Layer

### IGeofabrikService (new interface)

```csharp
public interface IGeofabrikService
{
    /// <summary>
    /// Fetch or load cached Geofabrik index. Returns the tree-structured index.
    /// </summary>
    Task<GeofabrikIndex> GetIndexAsync(bool forceRefresh = false);
}
```

### Implementation Notes

- Uses the already-registered `HttpClient` singleton
- Cache path: `data/geofabrik-index-v1.json`
- Stale after 7 days → auto-refresh on next `GetIndexAsync()`
- Parse on background thread (JSON is ~25 MB)
- `index-v1.json` (with geometry) for bounds extraction
- Build tree by iterating features, assigning children by `parent` field

---

## ViewModel

### RegionPickerViewModel (new, in Oravey2.MapGen — no MAUI dependency)

```csharp
public class RegionPickerViewModel : INotifyPropertyChanged
{
    // Input
    public ObservableCollection<GeofabrikRegion> Roots { get; }
    public string SearchText { get; set; }

    // Selection
    public GeofabrikRegion? SelectedRegion { get; set; }
    public string SelectedPath { get; }      // "Europe → Netherlands → Noord-Holland"
    public string SelectedPbfUrl { get; }

    // Commands
    public ICommand LoadIndexCommand { get; }    // calls IGeofabrikService
    public ICommand SelectCommand { get; }       // closes dialog with result
    public ICommand CancelCommand { get; }

    // Events
    public event Action<RegionPreset>? RegionSelected;
    public event Action? Cancelled;
}
```

---

## Integration with WorldTemplateViewModel

```csharp
// In WorldTemplateViewModel:
public ICommand BrowseRegionCommand { get; }    // opens the picker dialog

// Handler:
private async void OnBrowseRegion()
{
    // Show RegionPickerDialog (modal)
    // On selection → update RegionPreset, OsmDownloadUrl, bounds, paths
}
```

---

## Implementation Steps

| # | Task | Project | Depends On |
|---|------|---------|------------|
| 1 | `GeofabrikRegion` record, `GeofabrikIndex` parser | Oravey2.MapGen | — |
| 2 | `IGeofabrikService` + implementation with caching | Oravey2.MapGen | 1 |
| 3 | `RegionPickerViewModel` with search, tree, selection | Oravey2.MapGen | 1. 2 |
| 4 | Unit tests for parser, search, preset generation | Oravey2.Tests | 1, 2, 3 |
| 5 | `RegionPickerDialog.xaml` with TreeView / CollectionView | Oravey2.MapGen.App | 3 |
| 6 | Wire [Browse…] button in WorldTemplateView | Oravey2.MapGen.App | 5 |
| 7 | DI registration in MauiProgram.cs | Oravey2.MapGen.App | 2 |

---

## MAUI TreeView Notes

.NET MAUI does not have a built-in `TreeView` control. Options:

1. **Flat CollectionView with indentation** — Each item has a `Depth` property used for left margin. Expand/collapse toggles children visible. Simple and works well for this depth (~5 levels max).
2. **Recursive DataTemplate** — Not natively supported in MAUI CollectionView.
3. **Third-party** — Syncfusion/Telerik TreeView — adds dependency.

**Recommendation**: Option 1 — flat `CollectionView` with expand/collapse and indentation. The tree is small (~400 regions) and 5 levels deep at most.

### Flattened Tree Item

```csharp
public class RegionTreeItem : INotifyPropertyChanged
{
    public GeofabrikRegion Region { get; }
    public int Depth { get; }
    public bool HasChildren { get; }
    public bool IsExpanded { get; set; }
    public bool IsVisible { get; set; }
    public bool IsSelected { get; set; }
    public Thickness Indent => new(Depth * 20, 0, 0, 0);
}
```

---

## Caching Strategy

```
data/
  geofabrik-index-v1.json        ← cached full index (25 MB, refreshed weekly)
  presets/
    noordholland.regionpreset    ← existing hand-crafted preset
    noord-holland.regionpreset   ← auto-generated from picker
```

- On first use: download `index-v1.json`, save to `data/`
- Subsequent uses: load from disk, check `LastWriteTime`
- If > 7 days old: re-download in background, use stale until ready
- Index file is `.gitignore`d (it's a large downloadable cache)

---

## Search Behavior

- Case-insensitive substring match on `Name`
- Also match on ISO 3166 codes (e.g. searching "NL" finds Netherlands)
- When search is active:
  - Hide non-matching nodes with no matching descendants
  - Auto-expand parents of matching nodes
- When search is cleared: restore previous expand/collapse state
