# Step 07 — Import Region UI on Scenario Selector

## Goal

Add an "Import Region" button to the scenario selector that opens a sub-overlay
listing available content packs. Users can import a region from a content pack
into their persistent world.db.

## Deliverables

### 7.1 Add `ImportService` property to `ScenarioSelectorScript`

File: `src/Oravey2.Core/UI/Stride/ScenarioSelectorScript.cs`

```csharp
/// <summary>Service for importing content pack regions.</summary>
public ContentPackImportService? ImportService { get; set; }
```

### 7.2 Add "Import Region" button to bottom bar

In the `Start()` method, add a button between the bottom bar `Children`
alongside Start and Cancel:

```csharp
var importButton = new Button
{
    Content = new TextBlock
    {
        Text = "Import Region",
        Font = Font,
        TextSize = 24,
        TextColor = Color.White,
        HorizontalAlignment = HorizontalAlignment.Center,
    },
    BackgroundColor = new Color(80, 60, 40, 255),
    HorizontalAlignment = HorizontalAlignment.Center,
    MinimumWidth = 200,
    MinimumHeight = 50,
    Margin = new Thickness(0, 8, 20, 8),
};
importButton.Click += (_, _) => ShowImportOverlay();
ButtonLabels.Add("Import Region");
```

Update the bottom bar:
```csharp
var bottomBar = new StackPanel
{
    Orientation = Orientation.Horizontal,
    HorizontalAlignment = HorizontalAlignment.Center,
    Margin = new Thickness(0, 20, 0, 0),
    Children = { importButton, _startButton, cancelButton },
};
```

### 7.3 Import overlay — `ShowImportOverlay()`

Add a private method that builds and shows the import sub-overlay:

```csharp
private Border? _importOverlay;
private StackPanel? _importListPanel;

private void ShowImportOverlay()
{
    if (ImportService == null) return;

    // Hide the main selector
    if (_overlay != null) _overlay.Visibility = Visibility.Collapsed;

    if (_importOverlay != null)
    {
        // Refresh list and show existing overlay
        RefreshImportList();
        _importOverlay.Visibility = Visibility.Visible;
        return;
    }

    // Build the import overlay
    var title = new TextBlock
    {
        Text = "IMPORT REGION",
        Font = Font,
        TextSize = 36,
        TextColor = Color.White,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 0, 0, 20),
    };

    _importListPanel = new StackPanel
    {
        Orientation = Orientation.Vertical,
        HorizontalAlignment = HorizontalAlignment.Center,
        MinimumWidth = 500,
    };

    var backButton = new Button
    {
        Content = new TextBlock
        {
            Text = "Back",
            Font = Font,
            TextSize = 24,
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center,
        },
        BackgroundColor = new Color(60, 60, 80, 200),
        HorizontalAlignment = HorizontalAlignment.Center,
        MinimumWidth = 200,
        MinimumHeight = 50,
        Margin = new Thickness(0, 20, 0, 0),
    };
    backButton.Click += (_, _) => HideImportOverlay();
    ButtonLabels.Add("Back");

    var rootStack = new StackPanel
    {
        Orientation = Orientation.Vertical,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Children = { title, _importListPanel, backButton },
    };

    _importOverlay = new Border
    {
        BackgroundColor = new Color(10, 10, 15, 230),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        Content = rootStack,
        Visibility = Visibility.Visible,
    };

    var page = new UIPage { RootElement = _importOverlay };
    var uiComponent = new UIComponent { Page = page };
    Entity.Add(uiComponent);

    RefreshImportList();
}
```

### 7.4 Populate import list — `RefreshImportList()`

```csharp
private void RefreshImportList()
{
    if (_importListPanel == null || ImportService == null) return;
    _importListPanel.Children.Clear();

    var regions = ImportService.GetImportableRegions();

    if (regions.Count == 0)
    {
        _importListPanel.Children.Add(new TextBlock
        {
            Text = "No content packs with world.db found.\nExport a region from MapGen first.",
            Font = Font,
            TextSize = 18,
            TextColor = Color.Gray,
            WrapText = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0),
        });
        return;
    }

    foreach (var region in regions)
    {
        var imported = ImportService.IsRegionImported(region.RegionName);
        var card = CreateImportCard(region, imported);
        _importListPanel.Children.Add(card);
    }
}
```

### 7.5 Import card UI — `CreateImportCard()`

Each content pack gets a card with name, description, and Import button:

```csharp
private Border CreateImportCard(ImportableRegion region, bool alreadyImported)
{
    var nameText = new TextBlock
    {
        Text = region.RegionName,
        Font = Font,
        TextSize = 22,
        TextColor = Color.White,
    };

    var descText = new TextBlock
    {
        Text = region.Description,
        Font = Font,
        TextSize = 16,
        TextColor = Color.LightGray,
        WrapText = true,
    };

    var packText = new TextBlock
    {
        Text = $"Pack: {region.PackId}",
        Font = Font,
        TextSize = 14,
        TextColor = Color.Gray,
    };

    var statusText = new TextBlock
    {
        Text = alreadyImported ? "Already imported (will update)" : "",
        Font = Font,
        TextSize = 14,
        TextColor = new Color(200, 180, 60, 255),
    };

    var importBtn = new Button
    {
        Content = new TextBlock
        {
            Text = alreadyImported ? "Re-import" : "Import",
            Font = Font,
            TextSize = 18,
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center,
        },
        BackgroundColor = AccentColor,
        MinimumWidth = 120,
        MinimumHeight = 36,
        HorizontalAlignment = HorizontalAlignment.Right,
    };

    var packDir = region.PackDirectory;
    var regionName = region.RegionName;
    importBtn.Click += (_, _) => DoImport(packDir, regionName, statusText);
    ButtonLabels.Add($"Import {region.RegionName}");

    var infoColumn = new StackPanel
    {
        Orientation = Orientation.Vertical,
        Children = { nameText, descText, packText, statusText },
    };

    var cardContent = new StackPanel
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Children = { infoColumn, importBtn },
    };

    return new Border
    {
        BackgroundColor = new Color(40, 40, 55, 200),
        Padding = new Thickness(12),
        Margin = new Thickness(0, 4, 0, 4),
        Content = cardContent,
    };
}
```

### 7.6 Perform import — `DoImport()`

```csharp
private void DoImport(string packDir, string regionName, TextBlock statusText)
{
    if (ImportService == null) return;

    try
    {
        var result = ImportService.ImportRegion(packDir);
        var parts = new List<string>
        {
            $"{result.TownsImported} towns",
            $"{result.ChunksWritten} chunks",
            $"{result.EntitySpawnsInserted} entity spawns"
        };
        statusText.Text = $"Imported '{result.RegionName}': {string.Join(", ", parts)}.";
        statusText.TextColor = AccentColor;

        // Reload the user's world store so the scenario list updates
        ReloadWorldStore();
    }
    catch (Exception ex)
    {
        statusText.Text = $"Import failed: {ex.Message}";
        statusText.TextColor = new Color(200, 60, 60, 255);
    }
}
```

### 7.7 Reload world store — `ReloadWorldStore()`

After import, reopen the user's world.db so the scenario list refreshes:

```csharp
private void ReloadWorldStore()
{
    var worldDbPath = WorldDbPaths.GetUserWorldDbPath();
    WorldStore?.Dispose();
    WorldStore = File.Exists(worldDbPath) ? new WorldMapStore(worldDbPath) : null;
}
```

### 7.8 Return to selector — `HideImportOverlay()`

```csharp
private void HideImportOverlay()
{
    if (_importOverlay != null)
        _importOverlay.Visibility = Visibility.Collapsed;

    // Show the main selector and rebuild the scenario list
    if (_overlay != null)
        _overlay.Visibility = Visibility.Visible;

    RebuildScenarioList();
}
```

### 7.9 UI layout mockup

**Scenario selector (main):**
```
┌──────────────────────────────────────────────────────┐
│                 SELECT SCENARIO                       │
│  ┌───────────────────┐  ┌──────────────────────────┐ │
│  │ Haven Town        │  │ Noord-Holland             │ │
│  │ Scorched Outskirts│  │ Post-apocalyptic Noord-   │ │
│  │ Combat Arena      │  │ Holland region...          │ │
│  │ Empty World       │  │ Biome: wasteland          │ │
│  │ Terrain Test      │  │                           │ │
│  │ Noord-Holland     │  │                           │ │
│  └───────────────────┘  └──────────────────────────┘ │
│  [ Import Region ]       [ Start ]    [ Cancel ]      │
└──────────────────────────────────────────────────────┘
```

**Import overlay:**
```
┌──────────────────────────────────────────────────────┐
│                 IMPORT REGION                         │
│  ┌──────────────────────────────────────────────┐    │
│  │ Noord-Holland                                 │    │
│  │ Post-apocalyptic Noord-Holland region.        │    │
│  │ Pack: oravey2.apocalyptic.nl.nh              │    │
│  │ Already imported (will update)  [ Re-import ] │    │
│  └──────────────────────────────────────────────┘    │
│                              [ Back ]                 │
└──────────────────────────────────────────────────────┘
```

## Dependencies

- Step 05 (`ContentPackImportService`)
- Step 06 (game reads user library — `WorldStore` is now at user path)

## Estimated scope

- Modified files: 1 (`ScenarioSelectorScript.cs` — ~180 lines added)
- New methods: `ShowImportOverlay`, `HideImportOverlay`, `RefreshImportList`,
  `CreateImportCard`, `DoImport`, `ReloadWorldStore`
