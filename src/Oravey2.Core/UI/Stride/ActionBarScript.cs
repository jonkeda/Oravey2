using global::Stride.Core.Mathematics;
using global::Stride.Engine;
using global::Stride.Graphics;
using global::Stride.Rendering;
using global::Stride.UI;
using global::Stride.UI.Controls;
using global::Stride.UI.Panels;
using Oravey2.Core.Framework.State;
using Color = global::Stride.Core.Mathematics.Color;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Horizontal action button bar at the bottom of the screen.
/// Provides mouse-clickable shortcuts for Map, Inventory, Journal, and Settings.
/// </summary>
public class ActionBarScript : SyncScript
{
    public GameStateManager? StateManager { get; set; }
    public SpriteFont? Font { get; set; }

    public RegionMapOverlayScript? MapOverlay { get; set; }
    public InventoryOverlayScript? InventoryOverlay { get; set; }
    public QuestJournalScript? JournalOverlay { get; set; }
    public PauseMenuScript? PauseOverlay { get; set; }

    private UIComponent? _uiComponent;
    private UIElement? _root;

    private static readonly Color BgColor = new(0, 0, 0, 160);
    private static readonly Color ButtonBg = new(60, 60, 60, 200);
    private static readonly Color ButtonHover = new(90, 90, 90, 220);
    private static readonly Color LabelColor = Color.White;
    private static readonly Color HintColor = new(180, 180, 180);

    private const float ButtonWidth = 80f;
    private const float ButtonHeight = 40f;
    private const float Gap = 4f;

    public override void Start()
    {
        base.Start();
        BuildUI();
    }

    public override void Update()
    {
        if (_root == null) return;

        // Hide bar when a full-screen overlay is open or in non-interactive states
        var state = StateManager?.CurrentState;
        bool anyOverlayOpen = (MapOverlay?.IsVisible == true)
                           || (InventoryOverlay?.IsVisible == true)
                           || (JournalOverlay?.IsVisible == true)
                           || (PauseOverlay?.IsVisible == true);

        bool shouldShow = (state is GameState.Exploring or GameState.InCombat)
                       && !anyOverlayOpen;

        _root.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BuildUI()
    {
        var buttons = new (string Label, string Hint, Action OnClick, string AutoId)[]
        {
            ("Map", "[M]", () => MapOverlay?.Toggle(), "ActionBar_MapButton"),
            ("Inv", "[I]", () => InventoryOverlay?.Toggle(), "ActionBar_InventoryButton"),
            ("Jrnl", "[J]", () => JournalOverlay?.Toggle(), "ActionBar_JournalButton"),
            ("Menu", "[Esc]", () => PauseOverlay?.Toggle(), "ActionBar_SettingsButton"),
        };

        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 8),
        };

        foreach (var (label, hint, onClick, autoId) in buttons)
        {
            var btn = CreateButton(label, hint, onClick);
            btn.Name = autoId;
            bar.Children.Add(btn);
        }

        var container = new Border
        {
            BackgroundColor = BgColor,
            Content = bar,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 8),
        };
        container.Name = "ActionBar";

        _root = container;
        _root.Visibility = Visibility.Visible;

        var page = new UIPage { RootElement = container };
        _uiComponent = new UIComponent
        {
            Page = page,
            RenderGroup = RenderGroup.Group31,
        };
        Entity.Add(_uiComponent);
    }

    private Button CreateButton(string label, string hint, Action onClick)
    {
        var labelText = new TextBlock
        {
            Text = label,
            Font = Font,
            TextSize = 14,
            TextColor = LabelColor,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var hintText = new TextBlock
        {
            Text = hint,
            Font = Font,
            TextSize = 11,
            TextColor = HintColor,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { labelText, hintText },
        };

        var btn = new Button
        {
            Content = content,
            Width = ButtonWidth,
            Height = ButtonHeight,
            Margin = new Thickness(Gap / 2, 0, Gap / 2, 0),
        };

        btn.Click += (_, _) => onClick();

        return btn;
    }
}
