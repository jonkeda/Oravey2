# Step 08b — Town Maps UI Improvements

## Problems

1. **No map overview** — after generation the user only sees stat numbers; there
   is no visual preview of the tile map.
2. **No tuneable parameters** — grid size, prop density, building fill and scale
   factor are all hard-coded in `TownMapCondenser`. The user has no control.
3. **Black-on-black text** — the Buildings and Zones `CollectionView` items
   render black text on the `#1E1E1E` card background, making them unreadable.

## 1  Map Preview Canvas

Add a `GraphicsView` tile-map preview below the stats card.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  ⑥ Town Maps                                                                │
│  Generate condensed tile maps for each designed town.                        │
│                                                                              │
│ ┌──────────────────────┐  ┌────────────────────────────────────────────────┐ │
│ │ ✅ Island Haven      │  │  Island Haven                                 │ │
│ │                      │  │  survivor_camp  Threat: 2                     │ │
│ │                      │  ├────────────────────────────────────────────────┤ │
│ │                      │  │  Generation Parameters                        │ │
│ │                      │  │                                                │ │
│ │                      │  │  Grid size  [Auto ▾]  Scale  [0.01]           │ │
│ │  1/1 generated       │  │  Prop density  [ 70 ] %  Max props [ 30 ]    │ │
│ │                      │  │  Building fill %  [ 40 ]   Seed [ 12345 ]    │ │
│ │  [Generate Map]      │  ├────────────────────────────────────────────────┤ │
│ │  [Generate All Maps] │  │  Map Statistics                                │ │
│ │                      │  │  32×32 tiles · 11 buildings · 27 props · 4 z  │ │
│ │  Status text         │  │  Grid: 32×32    Buildings: 11                 │ │
│ │                      │  │  Props: 27      Zones: 4                      │ │
│ └──────────────────────┘  ├────────────────────────────────────────────────┤ │
│                           │  Map Preview                                   │ │
│                           │ ┌────────────────────────────────────────────┐ │ │
│                           │ │                                            │ │ │
│                           │ │         ░░░▓▓▓░░░░░░░                      │ │ │
│                           │ │         ░░████▓░░░░░░                      │ │ │
│                           │ │         ░▓█L██░░░░░░░                      │ │ │
│                           │ │         ░░████░░▓▓░░░                      │ │ │
│                           │ │         ░░░▓▓░░░██░░░                      │ │ │
│                           │ │         ░░░░░░░░░░░░░                      │ │ │
│                           │ │                              🔍 Zoom +/-   │ │ │
│                           │ └────────────────────────────────────────────┘ │ │
│                           ├────────────────────────────────────────────────┤ │
│                           │  🏠 Buildings (11)                             │ │
│                           │    Fort Haven (large)  1F                      │ │
│                           │    Market Hall (medium)  2F                    │ │
│                           │    Ruin_3 (small)  1F                          │ │
│                           │    ...                                         │ │
│                           ├────────────────────────────────────────────────┤ │
│                           │  🗺 Zones (4)                                  │ │
│                           │    Main Zone · Fast Travel: True               │ │
│                           │    Flood Zone · Fast Travel: False             │ │
│                           │    ...                                         │ │
│                           ├────────────────────────────────────────────────┤ │
│                           │  [Accept]  [Re-generate]                       │ │
│                           └────────────────────────────────────────────────┘ │
│                                                                              │
│  [Next →]                                                                    │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 1.1 Canvas implementation

| Element | Details |
|---------|---------|
| Control | `GraphicsView` with custom `IDrawable` (`TownMapPreviewDrawable`) |
| Size | `HeightRequest="300"`, expands to card width |
| Pixel-per-tile | `min(canvasW / gridW, canvasH / gridH)` — auto-fit |
| Tile colours | See §4 colour table |
| Buildings | Filled rectangles shaded by `SizeCategory` |
| Props | Small dot or icon at placement tile |
| Zones | Dashed outline rectangles |
| Landmark | Highlighted in accent colour with "L" initial |
| Interaction | Canvas is read-only; zoom slider optional (future) |

### 1.2 Drawable class

```
src/Oravey2.MapGen.App/Views/TownMapPreviewDrawable.cs
```

```csharp
public sealed class TownMapPreviewDrawable : IDrawable
{
    public TownMapResult? MapResult { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect) { ... }
}
```

Bound via code-behind: when `SelectedTown.MapResult` changes, set
`_drawable.MapResult = item.MapResult` and call `GraphicsView.Invalidate()`.

## 2  Generation Parameters

Expose configurable knobs that feed into `TownMapCondenser.Condense()`.

### 2.1 Parameter record

```csharp
public sealed record MapGenerationParams
{
    public GridSizeMode GridSize { get; init; } = GridSizeMode.Auto;
    public int CustomGridDimension { get; init; } = 32;   // used when GridSize == Custom
    public float ScaleFactor { get; init; } = 0.01f;
    public int PropDensityPercent { get; init; } = 70;     // 0–100
    public int MaxProps { get; init; } = 30;
    public int BuildingFillPercent { get; init; } = 40;    // 0–100
    public int? Seed { get; init; }                        // null = random
}

public enum GridSizeMode { Auto, Small_16, Medium_32, Large_48, Custom }
```

### 2.2 Parameter descriptions

| Parameter | Default | Range | Effect |
|-----------|---------|-------|--------|
| **Grid size** | Auto | Auto / 16 / 32 / 48 / Custom | Map dimensions in tiles (auto sizes from key-location count) |
| **Custom dimension** | 32 | 16–64, step 16 | Only visible when Grid size = Custom |
| **Scale factor** | 0.01 | 0.005–0.05 | Real-world metres → tile conversion ratio |
| **Prop density %** | 70 | 0–100 | Percentage of eligible empty tiles that receive props |
| **Max props** | 30 | 0–100 | Hard cap on total prop count |
| **Building fill %** | 40 | 0–100 | Percentage of remaining road-adjacent slots that receive generic ruins |
| **Seed** | (random) | any int | Deterministic generation; leave blank for random |

### 2.3 UI controls

Placed in a collapsible "Generation Parameters" card above the stats card.

```
┌─ Generation Parameters ──────────────────────────────────────────┐
│                                                                   │
│  Grid size      [ Auto ▾ ]        Scale factor   [ 0.01       ]  │
│  Prop density   [===●=====] 70%   Max props      [ 30         ]  │
│  Building fill  [====●====] 40%   Seed           [             ]  │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

- **Grid size**: `Picker` with `Auto, 16×16, 32×32, 48×48, Custom`
- **Custom dimension**: `Entry` (numeric), only shown when Custom selected
- **Scale factor**: `Entry` (decimal)
- **Prop density**: `Slider` 0–100 with adjacent `Label` showing value
- **Max props**: `Entry` (numeric)
- **Building fill %**: `Slider` 0–100 with adjacent `Label`
- **Seed**: `Entry` (numeric), placeholder "(random)"

### 2.4 ViewModel additions on `TownMapsStepViewModel`

```csharp
// Add properties
public MapGenerationParams GenerationParams { get; set; } = new();
public GridSizeMode SelectedGridSize { get; set; } = GridSizeMode.Auto;
public bool ShowCustomDimension => SelectedGridSize == GridSizeMode.Custom;
public int PropDensity { get; set; } = 70;
public int MaxPropsValue { get; set; } = 30;
public int BuildingFill { get; set; } = 40;
public string SeedText { get; set; } = "";
public float ScaleFactor { get; set; } = 0.01f;
```

Build `MapGenerationParams` from properties before calling `Condense()`.

### 2.5 Condenser changes

Update `TownMapCondenser.Condense()` to accept `MapGenerationParams`:

```csharp
public TownMapResult Condense(
    CuratedTown town,
    TownDesign design,
    RegionTemplate region,
    MapGenerationParams parms);
```

Internal algorithm reads from `parms` instead of constants:
- `ComputeGridSize` — honour `parms.GridSize` override
- `FillGenericBuildings` — use `parms.BuildingFillPercent`
- `PlaceProps` — use `parms.PropDensityPercent` and `parms.MaxProps`
- Seed comes from `parms.Seed ?? Random.Shared.Next()`

## 3  Fix: Black-on-Black List Text

### Root cause

The `CollectionView` items inside the Buildings and Zones cards use the
platform-default text colour (black on Windows) rather than explicit white.
The cards have `BackgroundColor="#1E1E1E"` so the text is invisible.

### Fix

Set `TextColor` on **every** `Label` inside the `DataTemplate`s for the
Buildings and Zones `CollectionView` items. The existing building `Name` label
already has `TextColor="White"` but additional labels and the zone list lack it.

**Buildings DataTemplate** — ensure all labels specify colour:

| Label | Current | Fix |
|-------|---------|-----|
| `Name` | `TextColor="White"` | ✓ already correct |
| `SizeCategory` badge | `TextColor="#AAAAAA"` | ✓ already correct |
| `Floors` | `TextColor="#888888"` | ✓ already correct |

**Zones DataTemplate** — fix missing colours:

| Label | Current | Fix |
|-------|---------|-----|
| `Name` | `TextColor="White"` | ✓ already correct |
| `IsFastTravelTarget` | `TextColor="#888888"` | ✓ already correct |

If the above explicit colours are present but text is still invisible, the
issue is the `CollectionView` **selection/hover** background. On Windows MAUI
the default selection highlight can be a dark colour that, combined with the
dark card, hides text.

**Additional fix** — set an explicit item background and selection style:

```xml
<!-- Inside each CollectionView within the Building/Zone cards -->
<CollectionView.ItemTemplate>
    <DataTemplate>
        <Grid BackgroundColor="Transparent" Padding="0,2">
            <!-- ... labels ... -->
        </Grid>
    </DataTemplate>
</CollectionView.ItemTemplate>
```

Also add `SelectionMode="None"` on both inner CollectionViews (they are
display-only, selection is not needed).

### Verified affected lines in `TownMapsStepView.xaml`

1. **Buildings CollectionView** (~line 130): add `SelectionMode="None"`
2. **Buildings DataTemplate root**: wrap in `Grid BackgroundColor="Transparent"`
3. **Zones CollectionView** (~line 168): add `SelectionMode="None"`
4. **Zones DataTemplate root**: wrap in `Grid BackgroundColor="Transparent"`

## 4  Colour Palette

### UI chrome

| Element | Colour | Usage |
|---------|--------|-------|
| Card background | `#1E1E1E` | Default dark card |
| Feature card bg | `#252530` | Summary / header card |
| Border stroke | `#444444` | All card borders |
| Primary text | `#FFFFFF` | Headings, names |
| Secondary text | `#AAAAAA` | Descriptions, labels |
| Muted text | `#888888` | Counts, hints |
| Accent (green) | `#238636` | Generate / Accept buttons |
| Accent (blue) | `#0078D4` | Next button, spinners |
| Danger (red) | `#6E3630` | Cancel button |
| Neutral button | `#2D2D30` | Generate All, Re-generate |
| Threat text | `#E5A00D` | Threat level badges |

### Map preview tile colours

| Tile type | Colour | Hex |
|-----------|--------|-----|
| Grass / ground (0) | Dark olive | `#3B5323` |
| Dirt / path (1) | Sandy brown | `#8B7355` |
| Road (2) | Warm grey | `#6B6B6B` |
| Water (3) | Dark blue | `#1A3A5C` |
| Building footprint | Medium grey | `#5A5A6A` |
| Landmark building | Gold accent | `#C8A84E` |
| Prop marker | Small dot | `#AA6644` |
| Zone outline | Dashed | `#BBBBBB` at 50% opacity |
| Hazard zone fill | Red overlay | `#FF000020` (12% alpha) |

## 5  Implementation Checklist

- [ ] Create `MapGenerationParams` record in `Oravey2.MapGen/Generation/`
- [ ] Create `GridSizeMode` enum in same file
- [ ] Update `TownMapCondenser.Condense()` signature to accept `MapGenerationParams`
- [ ] Update internal algorithm to read from params instead of constants
- [ ] Add parameter properties to `TownMapsStepViewModel`
- [ ] Add "Generation Parameters" card to `TownMapsStepView.xaml`
- [ ] Create `TownMapPreviewDrawable` in `Oravey2.MapGen.App/Views/`
- [ ] Add `GraphicsView` "Map Preview" card to XAML
- [ ] Wire drawable refresh in code-behind on selection change
- [ ] Fix CollectionView `SelectionMode="None"` on Buildings/Zones lists
- [ ] Fix DataTemplate roots with `BackgroundColor="Transparent"`
- [ ] Update tests to pass `MapGenerationParams`
- [ ] Build and verify 0 errors
- [ ] Run all tests
