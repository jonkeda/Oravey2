using System.Text.Json;
using Oravey2.Core.Content;
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

    /// <summary>Fires with the chosen scenario ID when Start is clicked.</summary>
    public Action<string>? OnScenarioSelected { get; set; }

    /// <summary>Fires when Cancel is clicked.</summary>
    public Action? OnBack { get; set; }

    private UIComponent? _uiComponent;
    private Border? _overlay;
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

        // --- Bottom bar: Start + Cancel ---
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
            Children = { _startButton, cancelButton },
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

        var page = new UIPage { RootElement = _overlay };
        _uiComponent = new UIComponent { Page = page };
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

    private ScenarioInfo[] BuildScenarioList()
    {
        var list = new List<ScenarioInfo>();

        // Add built-in scenarios
        list.AddRange(Scenarios);

        // Add content pack scenarios
        if (ContentPacks?.ActivePack != null)
        {
            var packScenarios = ContentPacks.LoadScenarios();
            foreach (var s in packScenarios)
            {
                // Skip if already a built-in ID
                if (list.Any(e => e.Id == s.Id)) continue;

                var notes = $"Pack: {ContentPacks.ActivePack.Manifest.Name}";
                if (s.Features?.Length > 0)
                    notes += $"\nFeatures: {string.Join(", ", s.Features)}";
                if (s.Difficulty > 0)
                    notes += $"\nDifficulty: {s.Difficulty}";

                list.Add(new ScenarioInfo(s.Id, s.Name, s.Description, notes));
            }
        }

        // Add custom compiled maps discovered on disk
        list.AddRange(DiscoverCustomMaps());

        return list.ToArray();
    }

    private void RebuildScenarioList()
    {
        if (_listPanel == null) return;

        // Clear existing buttons
        _listPanel.Children.Clear();
        _listButtons.Clear();

        // Remove old scenario button labels (keep Start, Cancel)
        ButtonLabels.RemoveAll(l => l != "Start" && l != "Cancel");

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
}
