using global::Stride.Engine;
using global::Stride.Graphics;
using global::Stride.UI;
using global::Stride.UI.Controls;
using global::Stride.UI.Panels;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Combat;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Inventory.Core;
using Color = global::Stride.Core.Mathematics.Color;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Updates HUD each frame from live game components.
/// Phase C: visual HP/AP bars with color gradients.
/// </summary>
public class HudSyncScript : SyncScript
{
    public HealthComponent? Health { get; set; }
    public CombatComponent? Combat { get; set; }
    public LevelComponent? Level { get; set; }
    public InventoryComponent? Inventory { get; set; }
    public GameStateManager? StateManager { get; set; }
    public SpriteFont? Font { get; set; }

    private const float BarWidth = 200f;
    private const float BarHeight = 16f;

    private Border? _hpBarBg;
    private Border? _hpBarFill;
    private TextBlock? _hpText;
    private Border? _apBarBg;
    private Border? _apBarFill;
    private TextBlock? _apText;
    private TextBlock? _levelText;
    private TextBlock? _capsText;
    private TextBlock? _stateText;

    /// <summary>Exposes the HUD root element for external visibility control.</summary>
    public UIElement? RootElement { get; private set; }

    public override void Start()
    {
        base.Start();

        // --- HP bar row ---
        _hpBarBg = new Border
        {
            BackgroundColor = new Color(40, 40, 40, 200),
            Width = BarWidth,
            Height = BarHeight,
        };
        _hpBarFill = new Border
        {
            BackgroundColor = new Color(50, 200, 50),
            Width = BarWidth,
            Height = BarHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _hpText = new TextBlock
        {
            Text = "HP: --/--",
            Font = Font,
            TextSize = 14,
            TextColor = Color.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(BarWidth + 8, 0, 0, 0),
        };
        var hpRow = new Grid
        {
            Height = BarHeight + 4,
            Margin = new Thickness(0, 2, 0, 2),
            Children = { _hpBarBg, _hpBarFill, _hpText },
        };

        // --- AP bar row ---
        _apBarBg = new Border
        {
            BackgroundColor = new Color(40, 40, 40, 200),
            Width = BarWidth,
            Height = BarHeight,
        };
        _apBarFill = new Border
        {
            BackgroundColor = new Color(70, 130, 230),
            Width = BarWidth,
            Height = BarHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _apText = new TextBlock
        {
            Text = "AP: --/--",
            Font = Font,
            TextSize = 14,
            TextColor = Color.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(BarWidth + 8, 0, 0, 0),
        };
        var apRow = new Grid
        {
            Height = BarHeight + 4,
            Margin = new Thickness(0, 2, 0, 2),
            Children = { _apBarBg, _apBarFill, _apText },
        };

        // --- Level text ---
        _levelText = new TextBlock
        {
            Text = "LVL: -",
            Font = Font,
            TextSize = 14,
            TextColor = new Color(250, 250, 210),
            Margin = new Thickness(4, 2, 0, 2),
        };

        // --- Caps text ---
        _capsText = new TextBlock
        {
            Text = "Caps: 0",
            Font = Font,
            TextSize = 14,
            TextColor = new Color(255, 220, 50),
            Margin = new Thickness(4, 2, 0, 2),
        };

        // --- State banner ---
        _stateText = new TextBlock
        {
            Text = "Exploring",
            Font = Font,
            TextSize = 13,
            TextColor = Color.LightGray,
            Margin = new Thickness(4, 2, 0, 2),
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            BackgroundColor = new Color(0, 0, 0, 120),
            Margin = new Thickness(10, 10, 0, 0),
            Children = { hpRow, apRow, _levelText, _capsText, _stateText },
        };

        var page = new UIPage { RootElement = stack };
        RootElement = stack;
        Entity.Add(new UIComponent { Page = page, RenderGroup = global::Stride.Rendering.RenderGroup.Group31 });
    }

    public override void Update()
    {
        if (Health != null && _hpBarFill != null && _hpText != null)
        {
            float frac = Health.MaxHP > 0 ? (float)Health.CurrentHP / Health.MaxHP : 0f;
            _hpBarFill.Width = frac * BarWidth;
            _hpBarFill.BackgroundColor = frac >= 0.6f
                ? new Color(50, 200, 50)
                : frac >= 0.25f
                    ? new Color(230, 200, 25)
                    : new Color(230, 50, 25);
            _hpText.Text = $"{Health.CurrentHP}/{Health.MaxHP}";
        }

        if (Combat != null && _apBarFill != null && _apText != null)
        {
            float apFrac = Combat.MaxAP > 0 ? Combat.CurrentAP / Combat.MaxAP : 0f;
            _apBarFill.Width = apFrac * BarWidth;
            _apText.Text = $"{Combat.CurrentAP:F0}/{Combat.MaxAP}";
        }

        if (Level != null && _levelText != null)
            _levelText.Text = $"LVL: {Level.Level}  XP: {Level.CurrentXP}/{Level.XPToNextLevel}";

        if (Inventory != null && _capsText != null)
            _capsText.Text = $"Caps: {Inventory.Caps}";

        if (StateManager != null && _stateText != null)
        {
            var state = StateManager.CurrentState;
            _stateText.Text = state.ToString();
            _stateText.TextColor = state switch
            {
                GameState.InCombat => new Color(255, 100, 50),
                GameState.GameOver => new Color(180, 30, 30),
                _ => Color.LightGray,
            };
        }
    }
}
