# Design: Oravey2.MapGen.App — WorldTemplate Tab & Culling

## Status: Draft

## Overview

This design extends the existing **Oravey2.MapGen.App** (.NET MAUI desktop tool) with:

1. A **WorldTemplate** tab for creating `.worldtemplate` files from real-world data interactively (replacing the CLI-only `WorldTemplateTool`)
2. An interactive **culling mechanism** for towns and roads, so the operator can review, include/exclude, and tweak the extracted OSM features before building the template

---

## Current State

The MapGen.App is a `.NET MAUI` Windows desktop app (900×700, dark theme) with a `TabbedPage`:

| # | Tab                | Status                                                             |
| - | ------------------ | ------------------------------------------------------------------ |
| 1 | **Generate** | Disabled — old LLM blueprint pipeline removed, returns stub error |
| 2 | **Preview**  | Active — JSON blueprint preview with stats and validation         |
| 3 | **Houses**   | Active — Meshy AI text/image → 3D building models                |
| 4 | **Figures**  | Active — Meshy AI text/image → 3D character models + rigging     |
| 5 | **Settings** | Active — API keys, export paths, content pack path                |

The WorldTemplate creation pipeline currently runs only via the CLI tool (`Oravey2.WorldTemplateTool`). It extracts all towns and roads from OSM data without any manual filtering — every `place=city|town|village|hamlet` node and every `highway=motorway|trunk|primary|secondary` way is included. The LLM-based `TownCurator` later selects 8–15 towns during world generation, but that happens at game time, not at template build time.

**Problem:** Noord-Holland OSM data contains 85+ towns and 1,200+ road segments. Many are noise (tiny hamlets, dead-end secondary roads). The template file and generation pipeline benefit from a pre-curated, smaller dataset. Currently there's no way to review or adjust what goes in.

---

## Proposed Changes

### Tab Layout (After)

| # | Tab                     | Status                                                       |
| - | ----------------------- | ------------------------------------------------------------ |
| 1 | **WorldTemplate** | **NEW** — Interactive template builder with culling   |
| 2 | **Generate**      | Repurposed — triggers `WorldGenerator` against a template |
| 3 | **Preview**       | Existing — blueprint preview                                |
| 4 | **Houses**        | Existing — Meshy AI buildings                               |
| 5 | **Figures**       | Existing — Meshy AI characters                              |
| 6 | **Settings**      | Existing — API keys, paths                                  |

---

## WorldTemplate Tab

### File: `WorldTemplateView.xaml` + `WorldTemplateViewModel.cs`

### Layout

The tab is split into three vertical sections: **Source Selection**, **Map Preview + Culling**, and **Build Actions**.

```
┌─────────────────────────────────────────────────────────────┐
│  WORLDTEMPLATE                                              │
│                                                             │
│  ┌─── Source Files ───────────────────────────────────────┐ │
│  │ SRTM Directory: [data/srtm               ] [Browse…]   │ │
│  │ OSM PBF File:   [data/noordholland.osm.pbf] [Browse…]  │ │
│  │ Region Name:    [NoordHolland             ]            │ │
│  │                                        [Parse Data]    │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                             │
│  ┌─── Map Preview ── Towns ── Roads ── Water ─────────────┐ │
│  │                                                        │ │
│  │  ┌──────────────────────────┐ ┌──────────────────────┐ │ │
│  │  │                          │ │ ☑ Purmerend   Town   │ │ │
│  │  │    Map canvas with       │ │   pop: 81,000       │ │ │
│  │  │    towns as dots,        │ │ ☑ Alkmaar    Town   │ │ │
│  │  │    roads as lines,       │ │   pop: 109,000      │ │ │
│  │  │    water as blue areas   │ │ ☐ Wormer     Hamlet  │ │ │
│  │  │                          │ │   pop: 1,200        │ │ │
│  │  │    Click to select.      │ │ ☑ Zaandam    Town   │ │ │
│  │  │    Toggle visibility     │ │   pop: 77,000       │ │ │
│  │  │    per feature type.     │ │ ☐ Jisp       Hamlet  │ │ │
│  │  │                          │ │   pop: 400          │ │ │
│  │  │                          │ │ …                    │ │ │
│  │  │                          │ │                      │ │ │
│  │  │                          │ │ [Select All] [None]  │ │ │
│  │  │                          │ │ [Auto-cull…]         │ │ │
│  │  └──────────────────────────┘ └──────────────────────┘ │ │
│  │                                                        │ │
│  │  Filter: [All categories ▾]  Search: [          ]      │ │
│  │  Show: ☑ Towns  ☑ Roads  ☑ Water  ☐ Railways  ☐ Land  │ │
│  │                                                        │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                             │
│  ┌─── Build ──────────────────────────────────────────────┐ │
│  │ Output: [content/noordholland.worldtemplate] [Browse…]  │ │
│  │                                                        │ │
│  │ Summary: 12 towns · 347 roads · 89 water · 15 railways │ │
│  │          (culled: 73 towns · 900 roads)                │ │
│  │                                                        │ │
│  │                                  [Build WorldTemplate] │ │
│  │                                                        │ │
│  │ ┌──────────────────────────────────────────┐           │ │
│  │ │ > Parsing SRTM tiles...                  │           │ │
│  │ │ > Found 4 elevation tiles                │           │ │
│  │ │ > Parsing OSM data...                    │           │ │
│  │ │ > Extracted 85 towns, 1247 roads         │           │ │
│  │ │ > Applying culling rules...              │           │ │
│  │ │ > Building template...                   │           │ │
│  │ │ > Wrote 42.3 MB to noordholland.wtpl     │           │ │
│  │ └──────────────────────────────────────────┘           │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

## Section Details

### 1. Source Selection

| Field                | Type          | Default                       | Description                                                                      |
| -------------------- | ------------- | ----------------------------- | -------------------------------------------------------------------------------- |
| SRTM Directory       | Folder picker | `data/srtm`                 | Directory containing `.hgt` elevation files                                    |
| OSM PBF File         | File picker   | `data/noordholland.osm.pbf` | OpenStreetMap PBF extract                                                        |
| Region Name          | Text entry    | `NoordHolland`              | Name stored in the template header                                               |
| **Parse Data** | Button        | —                            | Runs `SrtmParser` + `OsmParser`, populates the map preview and feature lists |

**Parse Data** runs on a background thread. While parsing:

- Button changes to "Parsing…" with a spinner
- Progress messages appear in the log panel
- On completion, the map preview and feature lists populate

After parsing, the `OsmExtract` result is held in memory and the map canvas renders all features.

---

### 2. Map Preview & Culling (Tabbed Sub-views)

The centre section has **inner tabs** for each feature type: **Towns**, **Roads**, **Water**, **Railways**, **Land Use**. Each tab shows the same map canvas (left) but the list panel (right) shows that feature type.

#### 2a. Map Canvas

- **GraphicsView** (MAUI `IDrawable`) rendering the parsed features
- Coordinate space: game XZ from `GeoMapper`, auto-fitted to canvas bounds
- Layers (toggleable via show checkboxes):
  - Elevation grid: greyscale heightmap background
  - Land use zones: semi-transparent coloured polygons
  - Water bodies: blue filled polygons / blue lines (rivers/canals)
  - Roads: coloured lines (red=motorway, orange=trunk, yellow=primary, grey=secondary)
  - Railways: dashed lines
  - Towns: dots sized by category, coloured by include/exclude state
    - Green dot = included, red dot = excluded, white ring = selected
- Pan & zoom via mouse drag + scroll wheel
- Click a feature on the map = selects it in the list panel (and vice versa)

#### 2b. Feature List Panel

Each feature type gets a scrollable list with checkboxes:

**Towns list:**

| Column     | Description                                 |
| ---------- | ------------------------------------------- |
| ☑/☐      | Include/exclude checkbox                    |
| Name       | Town name from OSM                          |
| Category   | Hamlet / Village / Town / City / Metropolis |
| Population | From OSM `population` tag (0 if missing)  |
| Lat / Lon  | Geographic coordinates                      |

Sortable by name, population, or category. Filterable by category dropdown. Search box for name filter.

**Roads list:**

| Column    | Description                                 |
| --------- | ------------------------------------------- |
| ☑/☐     | Include/exclude checkbox                    |
| Class     | Motorway / Trunk / Primary / Secondary      |
| Nodes     | Number of coordinate points                 |
| Length    | Approximate length in km                    |
| Near Town | Nearest included town name (if within 500m) |

Sortable by class or length. Filterable by road class.

**Water / Railways / Land Use** follow the same pattern with type-specific columns.

**Bulk actions** at the bottom of each list:

- **Select All** — include all features of this type
- **None** — exclude all features
- **Auto-cull…** — opens the culling configuration dialog (see below)

---

### 3. Culling Mechanism

The culling mechanism reduces the raw OSM data to a gameplay-relevant subset. It operates in two modes: **automatic** (rule-based) and **manual** (checkbox toggling).

#### 3a. Auto-Cull Dialog

Clicking **Auto-cull…** opens a modal with configurable rules:

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
│                                                      │
│  Priority when culling:                              │
│    ○ Keep larger population                          │
│    ● Keep higher category                            │
│    ○ Keep more evenly spaced                         │
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
│                     [Preview]  [Apply]  [Cancel]     │
└──────────────────────────────────────────────────────┘
```

#### 3b. Town Culling Rules

Applied in order:

1. **Category filter** — Exclude all towns below the minimum category (e.g., exclude Hamlet if minimum is Village)
2. **Population filter** — Exclude towns below the population threshold
3. **Protected categories** — Cities and Metropolises are never culled (configurable)
4. **Spacing enforcement** — If two towns are closer than the minimum spacing:
   - Sort remaining towns by the selected priority (population, category, or spacing)
   - Remove the lower-priority town
   - Repeat until all pairs satisfy the spacing constraint
5. **Maximum cap** — If more than N towns remain, keep the top N by priority

**Algorithm for spacing enforcement:**

```
SortedTowns = towns.OrderByDescending(priority)
Included = []
for each town in SortedTowns:
    if all(distance(town, kept) >= minSpacing for kept in Included):
        Included.add(town)
    if Included.count >= maxTowns:
        break
```

This is a greedy algorithm — keeps the highest-priority towns and builds outward respecting the spacing constraint. It mirrors what `TownCurator.Validate()` does at generation time but gives the operator visual control.

#### 3c. Road Culling Rules

Applied in order:

1. **Class filter** — Exclude roads below the minimum class (e.g., exclude Secondary if minimum is Primary)
2. **Protected motorways** — Motorways are always included (configurable)
3. **Town proximity** — Keep roads that have at least one node within N km of an **included** town. This means town culling automatically propagates to road culling — removing a town removes its connecting roads.
4. **Dead-end removal** — Remove road segments shorter than the threshold that don't connect to another road or town
5. **Geometry simplification** — Apply Douglas-Peucker simplification to remaining roads to reduce node count (configurable tolerance in metres)

**Town-road dependency:** When a town is toggled off in the UI, roads that were only kept because of proximity to that town are automatically re-evaluated. The UI shows a "Near Town" column so the operator can see which town anchors each road.

#### 3d. Water Culling Rules

1. **Area filter** — Exclude water bodies smaller than the minimum area
2. **River length** — Exclude rivers/canals shorter than the minimum length
3. **Protected types** — Sea and large lakes always kept

#### 3e. Preview

The **Preview** button applies the rules without committing — it updates the map canvas to show which features would be kept (green) vs culled (red). The operator can then toggle individual features before clicking **Apply**.

**Apply** writes the include/exclude state to all features and updates the summary counts.

---

### 4. Build Actions

| Element                       | Description                                                                                                             |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| Output path                   | File picker defaulting to `content/{regionName}.worldtemplate`                                                        |
| Summary                       | Live counts of included features:`12 towns · 347 roads · 89 water · 15 railways` with culled counts in parentheses |
| **Build WorldTemplate** | Runs the build pipeline on a background thread                                                                          |
| Log panel                     | Scrolling text log of build progress messages                                                                           |

**Build pipeline:**

```
1. Create WorldTemplateBuilder with parsed data
2. Filter OsmExtract to only included features
3. Build RegionTemplate from filtered data + SRTM elevation
4. Serialize to .worldtemplate binary
5. Report output file size
```

The existing `WorldTemplateBuilder.Build()` and `WorldTemplateBuilder.Serialize()` are reused — the only change is that the `OsmExtract` passed in contains only the included features rather than the full parse result.

---

## Data Model Additions

### CullSettings (new)

```csharp
namespace Oravey2.MapGen.WorldTemplate;

public record TownCullSettings(
    TownCategory MinCategory = TownCategory.Village,
    int MinPopulation = 1000,
    double MinSpacingKm = 5.0,
    int MaxTowns = 30,
    TownCullPriority Priority = TownCullPriority.Category,
    bool AlwaysKeepCities = true,
    bool AlwaysKeepMetropolis = true);

public enum TownCullPriority { Population, Category, Spacing }

public record RoadCullSettings(
    RoadClass MinRoadClass = RoadClass.Primary,
    bool AlwaysKeepMotorways = true,
    bool KeepRoadsNearTowns = true,
    double TownProximityKm = 2.0,
    bool RemoveDeadEnds = true,
    double DeadEndMinKm = 1.0,
    bool SimplifyGeometry = true,
    double SimplifyToleranceM = 50.0);

public record WaterCullSettings(
    double MinAreaKm2 = 0.1,
    double MinRiverLengthKm = 2.0,
    bool AlwaysKeepSea = true,
    bool AlwaysKeepLakes = true);
```

### FeatureCuller (new)

```csharp
namespace Oravey2.MapGen.WorldTemplate;

public static class FeatureCuller
{
    public static List<TownEntry> CullTowns(
        List<TownEntry> towns, TownCullSettings settings);

    public static List<RoadSegment> CullRoads(
        List<RoadSegment> roads,
        List<TownEntry> includedTowns,
        RoadCullSettings settings);

    public static List<WaterBody> CullWater(
        List<WaterBody> water, WaterCullSettings settings);

    /// Douglas-Peucker line simplification
    public static Vector2[] SimplifyLine(
        Vector2[] points, double toleranceMetres);
}
```

This class is a pure function — no side effects, deterministic, fully unit-testable.

---

## ViewModel

### WorldTemplateViewModel

```csharp
public class WorldTemplateViewModel : BaseViewModel
{
    // Source inputs (bound to UI)
    public string SrtmDirectory { get; set; }
    public string OsmFilePath { get; set; }
    public string RegionName { get; set; }
    public string OutputPath { get; set; }

    // Parsed data (populated after Parse)
    public OsmExtract? ParsedExtract { get; private set; }
    public float[,]? ElevationGrid { get; private set; }

    // Observable feature collections with Include flag
    public ObservableCollection<TownItem> Towns { get; }
    public ObservableCollection<RoadItem> Roads { get; }
    public ObservableCollection<WaterItem> WaterBodies { get; }

    // Cull settings
    public TownCullSettings TownCull { get; set; }
    public RoadCullSettings RoadCull { get; set; }
    public WaterCullSettings WaterCull { get; set; }

    // Summary (computed)
    public string Summary => $"{IncludedTownCount} towns · {IncludedRoadCount} roads · …";

    // Commands
    public ICommand ParseCommand { get; }          // Parse SRTM + OSM
    public ICommand AutoCullCommand { get; }       // Open cull dialog
    public ICommand BuildCommand { get; }          // Build .worldtemplate
    public ICommand SelectAllCommand { get; }
    public ICommand SelectNoneCommand { get; }

    // Log
    public string LogText { get; set; }
}
```

**Item wrappers** (for checkboxes + map selection):

```csharp
public class TownItem : ObservableObject
{
    public TownEntry Data { get; }
    public bool IsIncluded { get; set; } = true;
    public bool IsSelected { get; set; }
}

public class RoadItem : ObservableObject
{
    public RoadSegment Data { get; }
    public bool IsIncluded { get; set; } = true;
    public string NearestTown { get; set; }
    public double LengthKm { get; set; }
}
```

---

## Map Canvas Rendering

The map canvas uses MAUI's `GraphicsView` with a custom `IDrawable`:

```csharp
public class WorldTemplateMapDrawable : IDrawable
{
    public OsmExtract? Extract { get; set; }
    public float[,]? Elevation { get; set; }
    public HashSet<int> IncludedTownIndices { get; set; }
    public HashSet<int> IncludedRoadIndices { get; set; }

    // Visibility toggles
    public bool ShowTowns { get; set; } = true;
    public bool ShowRoads { get; set; } = true;
    public bool ShowWater { get; set; } = true;
    public bool ShowRailways { get; set; }
    public bool ShowLandUse { get; set; }

    // View transform
    public float Zoom { get; set; } = 1f;
    public PointF Pan { get; set; }
    public int? SelectedFeatureIndex { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect) { … }
}
```

**Rendering order** (back to front):

1. Elevation heightmap (greyscale gradient)
2. Land use zones (semi-transparent fills)
3. Water bodies (blue fills / lines)
4. Railways (dashed grey lines)
5. Roads (coloured by class, dimmed if excluded)
6. Towns (dots sized by category, green=included, red=excluded, ring=selected)
7. Town labels (name text near dots)

---

## Interaction Between Town and Road Culling

When a town's include state changes (checkbox toggle or auto-cull), roads are re-evaluated:

```
Town toggled OFF
  → For each road marked "near this town":
    → If no other included town is within proximity:
      → Mark road as excluded
    → Update "Near Town" column

Town toggled ON
  → For each excluded road within proximity:
    → If road class >= minRoadClass:
      → Mark road as included
    → Update "Near Town" column
```

This keeps the road set consistent with the town set without requiring the operator to manually toggle hundreds of roads.

---

## Integration with Existing Pipeline

### WorldTemplateTool (CLI)

The CLI tool continues to work unchanged for CI/scripted builds. It includes all features without culling — same as today.

### WorldGenerator (Game-time)

The `TownCurator` and `RoadSelector` in `WorldGenerator` still run during game-time generation. Template-level culling is a **coarse pass** that removes noise. The LLM curation is a **fine pass** that selects 8–15 towns from the pre-culled set and assigns post-apocalyptic names, roles, and factions.

**Benefit:** With 30 curated towns instead of 85 raw ones, the LLM prompt is shorter, cheaper, and more focused. The LLM doesn't waste tokens debating whether a hamlet of 400 people should be in the game.

### Generate Tab (Repurposed)

The existing disabled "Generate" tab can be repurposed to run `WorldGenerator.GenerateAsync()` against a built `.worldtemplate` file + an LLM endpoint (from Settings). This makes the MapGen.App a complete pipeline tool:

```
WorldTemplate tab: Raw data → Curated template
Generate tab:      Template → world.db
```

This is a separate design and not detailed here, but the tab infrastructure is already in place.

---

## Settings Integration

New settings persisted via MAUI `Preferences`:

| Key                          | Type   | Default                       | Description                      |
| ---------------------------- | ------ | ----------------------------- | -------------------------------- |
| `WorldTemplate_SrtmDir`    | string | `data/srtm`                 | Last used SRTM directory         |
| `WorldTemplate_OsmFile`    | string | `data/noordholland.osm.pbf` | Last used OSM file               |
| `WorldTemplate_RegionName` | string | `NoordHolland`              | Last used region name            |
| `WorldTemplate_OutputDir`  | string | `content/`                  | Default output directory         |
| `WorldTemplate_TownCull`   | JSON   | (defaults)                    | Serialized `TownCullSettings`  |
| `WorldTemplate_RoadCull`   | JSON   | (defaults)                    | Serialized `RoadCullSettings`  |
| `WorldTemplate_WaterCull`  | JSON   | (defaults)                    | Serialized `WaterCullSettings` |

---

## Implementation Order

| #  | Task                                                   | New/Modify                      | Depends       |
| -- | ------------------------------------------------------ | ------------------------------- | ------------- |
| 1  | `FeatureCuller` — town culling algorithm            | New class in `Oravey2.MapGen` | —            |
| 2  | `FeatureCuller` — road culling + Douglas-Peucker    | Extend step 1                   | Step 1        |
| 3  | `FeatureCuller` — water culling                     | Extend step 1                   | —            |
| 4  | `CullSettings` record types                          | New in `Oravey2.MapGen`       | —            |
| 5  | Unit tests for `FeatureCuller`                       | New test class                  | Steps 1–4    |
| 6  | `WorldTemplateViewModel`                             | New in MapGen.App               | Steps 1–4    |
| 7  | `WorldTemplateView.xaml` — source selection + build | New XAML page                   | Step 6        |
| 8  | `WorldTemplateMapDrawable` — canvas rendering       | New drawable                    | Step 6        |
| 9  | Feature list panels (towns, roads, water)              | Part of XAML                    | Steps 6, 8    |
| 10 | Auto-cull dialog                                       | New popup/modal                 | Steps 1–4, 6 |
| 11 | Map click → list selection two-way binding            | ViewModel logic                 | Steps 8, 9    |
| 12 | Town–road dependency propagation                      | ViewModel logic                 | Steps 6, 9    |
| 13 | Register in `MainPage.xaml` + `MauiProgram.cs`     | Modify                          | Steps 6, 7    |
| 14 | Persist settings                                       | Modify `SettingsViewModel`    | Step 6        |

---

## Default Cull Presets for Noord-Holland

For the initial release, ship with sensible defaults that reduce the ~85 towns to ~25–30:

| Setting               | Value       | Rationale                                                   |
| --------------------- | ----------- | ----------------------------------------------------------- |
| Min category          | Village     | Exclude hamlets (<500 people)                               |
| Min population        | 1,000       | Only settlements that would logically survive an apocalypse |
| Min spacing           | 5 km        | Prevents town clustering in the Zaanstreek corridor         |
| Max towns             | 30          | Good density for the game map without overcrowding          |
| Priority              | Category    | Cities > Towns > Villages when culling by spacing           |
| Min road class        | Secondary   | Keep the main road network                                  |
| Always keep motorways | Yes         | A2, A7, A9 motorways are Noord-Holland landmarks            |
| Town proximity        | 2 km        | Roads must connect to a surviving settlement                |
| Remove dead-ends      | Yes, < 1 km | Clean up stubs that lead nowhere                            |
| Simplify geometry     | Yes, 50m    | Reduce memory without visible quality loss                  |
| Min water area        | 0.01 km²   | Keep all but the tiniest ditches                            |
| Min river length      | 1 km        | Keep significant waterways                                  |

Expected result after auto-cull:

- ~25 towns (from 85): Amsterdam, Haarlem, Alkmaar, Purmerend, Zaandam, Hoorn, Hilversum, etc.
- ~350 roads (from 1,200): All motorways + primary/secondary roads near kept towns
- ~90 water features (from 300+): Major canals, lakes, IJ, Markermeer coast
