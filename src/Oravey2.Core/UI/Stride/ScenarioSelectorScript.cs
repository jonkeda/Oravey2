using System.Text.Json;
using Oravey2.Core.Content;
using Oravey2.Core.Data;
using Oravey2.Core.World.Serialization;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using Color = Stride.Core.Mathematics.Color;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Scenario metadata for the selector list.
/// </summary>
public sealed record ScenarioInfo(string Id, string Name, string Description, string Notes);

/// <summary>
/// Full-screen overlay showing a selectable scenario list with detail notes.
/// Left panel: clickable list of scenario names. Right panel: selected scenario details.
/// Bottom bar: Start + Cancel buttons.
/// </summary>
public class ScenarioSelectorScript : SyncScript
{
    public SpriteFont? Font { get; set; }

    /// <summary>Content pack service for sourcing pack-specific scenarios.</summary>
    public ContentPackService? ContentPacks { get; set; }

    /// <summary>World database store for region-based scenarios.</summary>
    public WorldMapStore? WorldStore { get; set; }

    /// <summary>Debug database store for debug region scenarios.</summary>
    public WorldMapStore? DebugStore { get; set; }

    /// <summary>Service for importing content pack regions.</summary>
    public ContentPackImportService? ImportService { get; set; }

    /// <summary>Fires with the chosen scenario ID when Start is clicked.</summary>
    public Action<string>? OnScenarioSelected { get; set; }

    /// <summary>Fires when Cancel is clicked.</summary>
    public Action? OnBack { get; set; }

    /// <summary>Fires when a new WorldMapStore is opened after import.</summary>
    public Action<WorldMapStore>? OnStoreAdded { get; set; }

    private UIComponent? _uiComponent;
    private Border? _overlay;
    private Canvas? _rootCanvas;
    private string? _selectedId;
    private TextBlock? _detailTitle;
    private TextBlock? _detailDescription;
    private TextBlock? _detailNotes;
    private Button? _startButton;
    private StackPanel? _listPanel;
    private readonly List<(string Id, Button Button)> _listButtons = [];
    private ScenarioInfo[] _allScenarios = [];

    /// <summary>Whether the selector overlay is currently visible.</summary>
    public bool IsVisible => _overlay?.Visibility == Visibility.Visible;

    /// <summary>Button labels for automation queries.</summary>
    public List<string> ButtonLabels { get; } = [];

    public static readonly ScenarioInfo[] Scenarios =
    [
        new("town", "Haven Town",
            "Safe settlement hub with NPCs, quests, and shops.",
            "Starting zone. Talk to Elder Tomas to pick up quests.\n\n"
          + "Map: 32×32 tiles\nEnemies: None\nFeatures: NPCs, Dialogue, Quests"),

        new("wasteland", "Scorched Outskirts",
            "Combat zone with radrats and an optional raider boss.",
            "Connected to Haven Town via the west gate. Hostile territory.\n\n"
          + "Map: 32×32 tiles\nEnemies: 3 Radrats + Scar Boss\nFeatures: Combat, Loot, Kill Quests"),

        new("m0_combat", "Combat Arena",
            "Quick combat test with three enemies.",
            "Minimal scenario for testing combat mechanics.\n\n"
          + "Map: 32×32 tiles\nEnemies: 3\nFeatures: Combat, Loot"),

        new("empty", "Empty World",
            "Bare sandbox for testing.",
            "Flat ground, no enemies, no NPCs. Good for debugging.\n\n"
          + "Map: 32×32 tiles\nEnemies: None\nFeatures: None"),

        new("terrain_test", "Terrain Test",
            "Heightmap terrain with roads, river, and bridge.",
            "3×3 chunk grid with varied surfaces, hills, and linear features.\n\n"
          + "Map: 48×48 tiles\nEnemies: None\nFeatures: Road, River, Bridge"),
    ];

    private static readonly Color SelectedColor = new(80, 80, 120, 255);
    private static readonly Color UnselectedColor = new(50, 50, 70, 200);
    private static readonly Color AccentColor = new(60, 120, 80, 255);

    /// <summary>
    /// Discovers custom compiled maps in the Maps/ directory and returns ScenarioInfo for each.
    /// </summary>
    public static ScenarioInfo[] DiscoverCustomMaps()
    {
        var mapsDir = Path.Combine(AppContext.BaseDirectory, "Maps");
        if (!Directory.Exists(mapsDir))
            return [];

        var builtInIds = new HashSet<string>(Scenarios.Select(s => s.Id));
        var custom = new List<ScenarioInfo>();

        foreach (var dir in Directory.GetDirectories(mapsDir))
        {
            var worldJsonPath = Path.Combine(dir, "world.json");
            if (!File.Exists(worldJsonPath)) continue;

            var dirName = Path.GetFileName(dir);
            if (builtInIds.Contains(dirName)) continue;

            var name = dirName;
            var description = "Custom compiled map.";
            var notes = "";

            try
            {
                var json = File.ReadAllText(worldJsonPath);
                var worldJson = JsonSerializer.Deserialize<WorldJson>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                if (worldJson != null)
                {
                    if (!string.IsNullOrWhiteSpace(worldJson.Name))
                        name = worldJson.Name;
                    if (!string.IsNullOrWhiteSpace(worldJson.Description))
                        description = worldJson.Description;
                    notes = $"Map: {worldJson.ChunksWide}×{worldJson.ChunksHigh} chunks";
                    if (!string.IsNullOrWhiteSpace(worldJson.Source))
                        notes += $"\nSource: {worldJson.Source}";
                }
            }
            catch
            {
                // If world.json is malformed, still show the map with directory name
            }

            custom.Add(new ScenarioInfo(dirName, name, description, notes));
        }

        return custom.ToArray();
    }

    public override void Start()
    {
        base.Start();

        // --- Title ---
        var titleText = new TextBlock
        {
            Text = "SELECT SCENARIO",
            Font = Font,
            TextSize = 36,
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20),
        };

        // --- Left panel: scenario list ---
        var listPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            MinimumWidth = 220,
            Margin = new Thickness(0, 0, 20, 0),
        };
        _listPanel = listPanel;

        // Combine built-in + content pack scenarios + discovered custom maps
        _allScenarios = BuildScenarioList();

        foreach (var scenario in _allScenarios)
        {
            var btn = CreateListButton(scenario.Name);
            var id = scenario.Id;
            btn.Click += (_, _) => SelectScenario(id);
            _listButtons.Add((id, btn));
            listPanel.Children.Add(btn);
            ButtonLabels.Add(scenario.Name);
        }

        // --- Right panel: detail view ---
        _detailTitle = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 28,
            TextColor = Color.White,
            Margin = new Thickness(0, 0, 0, 12),
        };

        _detailDescription = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 18,
            TextColor = Color.LightGray,
            WrapText = true,
            Margin = new Thickness(0, 0, 0, 16),
        };

        _detailNotes = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 16,
            TextColor = Color.Gray,
            WrapText = true,
        };

        var detailPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            MinimumWidth = 350,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { _detailTitle, _detailDescription, _detailNotes },
        };

        // --- Center content: list + detail side by side ---
        var contentRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { listPanel, detailPanel },
        };

        // --- Bottom bar: Import Region + Start + Cancel ---
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

        _startButton = new Button
        {
            Content = new TextBlock
            {
                Text = "Start",
                Font = Font,
                TextSize = 24,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
            },
            BackgroundColor = AccentColor,
            HorizontalAlignment = HorizontalAlignment.Center,
            MinimumWidth = 200,
            MinimumHeight = 50,
            Margin = new Thickness(0, 8, 20, 8),
        };
        _startButton.Click += (_, _) =>
        {
            if (_selectedId != null)
                OnScenarioSelected?.Invoke(_selectedId);
        };
        ButtonLabels.Add("Start");

        var cancelButton = new Button
        {
            Content = new TextBlock
            {
                Text = "Cancel",
                Font = Font,
                TextSize = 24,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
            },
            BackgroundColor = new Color(60, 60, 80, 200),
            HorizontalAlignment = HorizontalAlignment.Center,
            MinimumWidth = 200,
            MinimumHeight = 50,
            Margin = new Thickness(20, 8, 0, 8),
        };
        cancelButton.Click += (_, _) => OnBack?.Invoke();
        ButtonLabels.Add("Cancel");

        var bottomBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0),
            Children = { importButton, _startButton, cancelButton },
        };

        // --- Root layout ---
        var rootStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { titleText, contentRow, bottomBar },
        };

        _overlay = new Border
        {
            BackgroundColor = new Color(10, 10, 15, 230),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = rootStack,
            Visibility = Visibility.Collapsed,
        };

        _rootCanvas = new Canvas();
        _rootCanvas.Children.Add(_overlay);

        var page = new UIPage { RootElement = _rootCanvas };
        _uiComponent = new UIComponent { Page = page, RenderGroup = global::Stride.Rendering.RenderGroup.Group31 };
        Entity.Add(_uiComponent);

        // Pre-select first scenario
        if (_allScenarios.Length > 0)
            SelectScenario(_allScenarios[0].Id);
    }

    public override void Update() { }

    public void Show()
    {
        // Rebuild list in case content pack changed
        RebuildScenarioList();

        if (_overlay != null)
            _overlay.Visibility = Visibility.Visible;

        if (_allScenarios.Length > 0)
            SelectScenario(_allScenarios[0].Id);
    }

    public void Hide()
    {
        if (_overlay != null)
            _overlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>The currently selected scenario ID, or null.</summary>
    public string? SelectedScenarioId => _selectedId;

    private void SelectScenario(string scenarioId)
    {
        _selectedId = scenarioId;

        // Update list button highlights
        foreach (var (id, btn) in _listButtons)
            btn.BackgroundColor = id == scenarioId ? SelectedColor : UnselectedColor;

        // Update detail panel
        var info = Array.Find(_allScenarios, s => s.Id == scenarioId);
        if (info != null)
        {
            if (_detailTitle != null) _detailTitle.Text = info.Name;
            if (_detailDescription != null) _detailDescription.Text = info.Description;
            if (_detailNotes != null) _detailNotes.Text = info.Notes;
        }
    }

    private Button CreateListButton(string text)
    {
        return new Button
        {
            Content = new TextBlock
            {
                Text = text,
                Font = Font,
                TextSize = 20,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Left,
            },
            BackgroundColor = UnselectedColor,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinimumWidth = 220,
            MinimumHeight = 42,
            Margin = new Thickness(0, 3, 0, 3),
        };
    }

    /// <summary>
    /// Discovers region-based scenarios from world database stores.
    /// Static helper for testability without Stride.
    /// </summary>
    public static ScenarioInfo[] DiscoverRegions(WorldMapStore? worldStore, WorldMapStore? debugStore)
    {
        var builtInIds = new HashSet<string>(Scenarios.Select(s => s.Id));
        var scenarios = new List<ScenarioInfo>();
        if (worldStore != null)
            foreach (var r in worldStore.GetAllRegions())
                if (!builtInIds.Contains(r.Name))
                    scenarios.Add(new ScenarioInfo(r.Name, r.Description ?? r.Name, $"Biome: {r.Biome}", ""));
        if (debugStore != null)
            foreach (var r in debugStore.GetAllRegions())
                if (!builtInIds.Contains(r.Name))
                    scenarios.Add(new ScenarioInfo(r.Name, $"[DEBUG] {r.Description ?? r.Name}", $"Biome: {r.Biome}", ""));
        return scenarios.ToArray();
    }

    private ScenarioInfo[] BuildScenarioList()
    {
        var list = new List<ScenarioInfo>();

        // Add built-in scenarios
        list.AddRange(Scenarios);

        // Add database region scenarios (DB is the sole source of truth;
        // content packs and custom maps must be imported via "Export to Game DB" first)
        list.AddRange(DiscoverRegions(WorldStore, DebugStore));

        return list.ToArray();
    }

    private void RebuildScenarioList()
    {
        if (_listPanel == null) return;

        // Clear existing buttons
        _listPanel.Children.Clear();
        _listButtons.Clear();

        // Remove old scenario button labels (keep Start, Cancel, Import Region)
        ButtonLabels.RemoveAll(l => l != "Start" && l != "Cancel" && l != "Import Region");

        // Rebuild
        _allScenarios = BuildScenarioList();
        foreach (var scenario in _allScenarios)
        {
            var btn = CreateListButton(scenario.Name);
            var id = scenario.Id;
            btn.Click += (_, _) => SelectScenario(id);
            _listButtons.Add((id, btn));
            _listPanel.Children.Add(btn);
            ButtonLabels.Add(scenario.Name);
        }
    }

    // ── Import overlay ─────────────────────────────────────

    private Border? _importOverlay;
    private StackPanel? _importListPanel;

    private void ShowImportOverlay()
    {
        if (ImportService == null) return;

        if (_overlay != null) _overlay.Visibility = Visibility.Collapsed;

        if (_importOverlay != null)
        {
            RefreshImportList();
            _importOverlay.Visibility = Visibility.Visible;
            return;
        }

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

        // Add import overlay to the shared root canvas
        _rootCanvas?.Children.Add(_importOverlay);

        RefreshImportList();
    }

    private void HideImportOverlay()
    {
        if (_importOverlay != null)
            _importOverlay.Visibility = Visibility.Collapsed;

        // Refresh the scenario list (may have new imported regions) and show main overlay
        RebuildScenarioList();
        // Re-open the world store if a new region was imported
        var worldDbPath = WorldDbPaths.GetUserWorldDbPath();
        if (File.Exists(worldDbPath) && WorldStore == null)
        {
            WorldStore = new WorldMapStore(worldDbPath);
            OnStoreAdded?.Invoke(WorldStore);
        }

        if (_overlay != null)
            _overlay.Visibility = Visibility.Visible;

        if (_allScenarios.Length > 0)
            SelectScenario(_allScenarios[0].Id);
    }

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

        var statusText = new TextBlock
        {
            Text = alreadyImported ? "✓ Imported" : "Not imported",
            Font = Font,
            TextSize = 14,
            TextColor = alreadyImported ? new Color(100, 200, 100, 255) : Color.Gray,
        };

        var importBtn = new Button
        {
            Content = new TextBlock
            {
                Text = alreadyImported ? "Re-import" : "Import",
                Font = Font,
                TextSize = 20,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
            },
            BackgroundColor = AccentColor,
            MinimumWidth = 120,
            MinimumHeight = 40,
            Margin = new Thickness(20, 0, 0, 0),
        };

        var packDir = region.PackDirectory;
        importBtn.Click += (_, _) =>
        {
            ImportService!.ImportRegion(packDir);
            RefreshImportList();
        };

        var textStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children = { nameText, descText, statusText },
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { textStack, importBtn },
        };

        return new Border
        {
            BackgroundColor = new Color(30, 30, 50, 200),
            Margin = new Thickness(0, 4, 0, 4),
            Padding = new Thickness(12, 8, 12, 8),
            Content = row,
        };
    }
}
