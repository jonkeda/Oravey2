# Step 05b — Parse & Extract Fixes

Follow-up to step 05. Addresses four issues discovered after initial
implementation.

## 1. Persist `RegionTemplate` to disk

Parsing takes 10–30 seconds for Noord-Holland. If the user navigates away and
comes back, or restarts the app, they must re-parse from scratch because the
`RegionTemplate` is only held in memory.

### Changes

#### 1.1 `RegionTemplateSerializer` (new)

File: `src/Oravey2.MapGen/RegionTemplates/RegionTemplateSerializer.cs`

```
SaveAsync(RegionTemplate template, string path)
LoadAsync(string path) → RegionTemplate?
```

- Serialize to a binary format using `BinaryWriter` / `BinaryReader`.
  JSON is too slow for large float[,] grids and Vector2[] arrays.
- File location: `data/regions/{name}/region-template.bin`
- Format:

  ```
  [4 bytes] magic "ORRT"
  [4 bytes] version (1)
  [string]  Name
  [double]  GridOriginLat, GridOriginLon, GridCellSizeMetres
  [int]     ElevationGrid rows, cols
  [float…]  ElevationGrid row-major
  [int]     TownCount
  [TownEntry…] each: string Name, double Lat, double Lon, int Pop,
               float GameX, float GameY, int Category, bool HasBoundary,
               (if HasBoundary: int PointCount, float… X/Y pairs)
  [int]     RoadCount
  [RoadSegment…] each: int RoadClass, int NodeCount, float… X/Y pairs
  [int]     WaterCount
  [WaterBody…] each: int WaterType, int NodeCount, float… X/Y pairs
  [int]     RailwayCount
  [RailwaySegment…] each: …
  [int]     LandUseCount
  [LandUseZone…] each: …
  ```

#### 1.2 `ParseStepViewModel` changes

- After `ParseAsync` completes successfully, call
  `RegionTemplateSerializer.SaveAsync(template, path)`.
- In `Initialize()`, if `state.Parse.Completed` **and** the
  `region-template.bin` file exists, load it with
  `RegionTemplateSerializer.LoadAsync` and set `IsParsed = true` directly
  (skip the "parse again" prompt).
- If the file is missing or corrupt, fall back to current behavior
  (prompt to re-parse).
- Update `ParseStepState` with a new `bool TemplateSaved { get; set; }` flag
  so the state JSON records that the binary file was written.

#### 1.3 Tests

- Round-trip test: create a `RegionTemplate` with known data, save, load,
  assert all fields match (towns, roads, water, elevation grid values).
- Corrupt-file test: truncated file returns `null` from `LoadAsync`.

---

## 2. Map legend / color index

The map preview uses distinct colors for elevation bands, road classes, water,
and towns, but there is no on-screen key explaining what each color means.

### Changes

#### 2.1 `RegionTemplateMapDrawable` — draw legend

Add a `DrawLegend(ICanvas, float w, float h)` method called at the end of
`Draw()` (drawn last so it sits on top of everything).

Position: bottom-left corner, semi-transparent dark background panel.

Legend rows (each: color swatch + label):

| Swatch | Label |
|--------|-------|
| Elevation gradient bar | "Low → High elevation" |
| `#FF3333` | Motorway |
| `#FF9933` | Trunk |
| `#FFFF4D` | Primary |
| `#FFFFFF` | Secondary |
| `#808080` | Tertiary / Residential |
| `#4488CC` | Water |
| `●` White | Town (included) |
| `●` Gray  | Town (excluded) |

Implementation:
- Background: `Color.FromRgba(0, 0, 0, 0.7f)`, rounded rect
- Swatch: 14×14 filled rect (or circle for towns)
- Label: white, 11pt
- Padding: 8px internal, 12px from canvas edges
- Add `public bool ShowLegend { get; set; } = true;` property so it can
  be toggled off if needed.

#### 2.2 View — no XAML changes needed

The legend is drawn inside the existing `GraphicsView`.

---

## 3. Town labels not readable on the map

As visible in the screenshot, town labels on the map are hard to read. White
text on a varied background (green/brown elevation, white roads) has no
contrast.

### Changes

#### 3.1 `RegionTemplateMapDrawable.DrawLabels` — add text outline / shadow

Replace the current single `DrawString` with a two-pass approach:

1. **Shadow pass**: draw the label text in black (or very dark) with a
   +1px offset in both X and Y directions. This creates a drop shadow.
2. **Main pass**: draw the label text in the original color (white/gray).

Additionally, add a semi-transparent dark background behind each label:

```
- Measure text width (approximate: fontSize * name.Length * 0.55f)
- Draw a filled rounded rect behind the text:
    Color.FromRgba(0, 0, 0, 0.6f)
    Padding: 2px horizontal, 1px vertical
- Then draw the shadow text, then the main text
```

#### 3.2 Increase minimum category for labels at default zoom

Currently labels show for `TownCategory.Town` and above at default zoom.
Change `minZoomForLabels` thresholds:

| Zoom   | Minimum category shown |
|--------|----------------------|
| < 0.5  | City only            |
| < 1.0  | Town and above       |
| < 1.5  | Village and above    |
| ≥ 1.5  | All (including Hamlet) |

This reduces clutter at overview zoom levels.

---

## 4. Summary line not visible enough

The raw and filtered summary lines use light colors (`#CDD6F4` and
`#A6E3A1`) on the dark background, and they're small (14pt). They're easy to
miss after parsing completes.

### Changes

#### 4.1 `ParseStepView.xaml` — summary styling

Wrap the summary in a `Border` with a subtle accent background:

```xml
<Border IsVisible="{Binding IsParsed}"
        Stroke="#444444" StrokeThickness="1"
        Padding="12,8" BackgroundColor="#252530"
        Margin="0,8,0,0">
    <Border.StrokeShape>
        <RoundRectangle CornerRadius="8" />
    </Border.StrokeShape>
    <VerticalStackLayout Spacing="6">
        <!-- Section header -->
        <Label Text="Parse Results"
               FontSize="15" FontAttributes="Bold" TextColor="White" />

        <!-- Raw summary — use white text, slightly larger -->
        <Label Text="{Binding RawSummary}"
               FontSize="15" TextColor="White" />

        <!-- Filtered summary — keep green accent, but bolder -->
        <Label Text="{Binding FilteredSummary}"
               FontSize="15" TextColor="#A6E3A1"
               FontAttributes="Bold" />
    </VerticalStackLayout>
</Border>
```

Key differences from current:
- Contained in a bordered card (matches the town list / summary table style)
- "Parse Results" header label
- Font size bumped 14 → 15
- Raw summary changed from muted `#CDD6F4` to plain `White`
- Filtered summary gets `Bold` attribute
- Margin separates it from the status text above

---

## Deliverable checklist

| # | Item | Files |
|---|------|-------|
| 1 | `RegionTemplateSerializer` | `RegionTemplates/RegionTemplateSerializer.cs` (new) |
| 1 | VM loads cached template on init | `ViewModels/ParseStepViewModel.cs` |
| 1 | `TemplateSaved` flag | `Pipeline/PipelineState.cs` |
| 1 | Round-trip + corrupt tests | `tests/Oravey2.Tests/Pipeline/RegionTemplateSerializerTests.cs` (new) |
| 2 | Map legend | `Views/RegionTemplateMapDrawable.cs` |
| 3 | Label readability | `Views/RegionTemplateMapDrawable.cs` |
| 4 | Summary card styling | `Views/Steps/ParseStepView.xaml` |

## Dependencies

- Step 05 complete (current implementation)

## Estimated scope

- 1 new file (serializer)
- 3 modified files (VM, drawable, view)
- 1 new test file + updates to existing VM tests
