using global::Stride.Engine;
using global::Stride.UI;
using global::Stride.UI.Controls;
using global::Stride.UI.Panels;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Combat;
using Oravey2.Core.Framework.State;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Updates HUD text each frame from live game components.
/// Creates a simple Stride UI overlay on Start.
/// </summary>
public class HudSyncScript : SyncScript
{
    public HealthComponent? Health { get; set; }
    public CombatComponent? Combat { get; set; }
    public LevelComponent? Level { get; set; }
    public GameStateManager? StateManager { get; set; }

    private TextBlock? _hpText;
    private TextBlock? _apText;
    private TextBlock? _levelText;
    private TextBlock? _stateText;

    public override void Start()
    {
        base.Start();

        _hpText = new TextBlock
        {
            Text = "HP: --/--",
            TextSize = 18,
            TextColor = global::Stride.Core.Mathematics.Color.White,
            Margin = new Thickness(10, 10, 0, 0)
        };

        _apText = new TextBlock
        {
            Text = "AP: --/--",
            TextSize = 18,
            TextColor = global::Stride.Core.Mathematics.Color.LightBlue,
            Margin = new Thickness(10, 0, 0, 0)
        };

        _levelText = new TextBlock
        {
            Text = "LVL: -",
            TextSize = 16,
            TextColor = global::Stride.Core.Mathematics.Color.LightGoldenrodYellow,
            Margin = new Thickness(10, 0, 0, 0)
        };

        _stateText = new TextBlock
        {
            Text = "Exploring",
            TextSize = 14,
            TextColor = global::Stride.Core.Mathematics.Color.LightGray,
            Margin = new Thickness(10, 0, 0, 0)
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { _hpText, _apText, _levelText, _stateText }
        };

        var page = new UIPage { RootElement = stack };
        Entity.Add(new UIComponent { Page = page });
    }

    public override void Update()
    {
        if (Health != null && _hpText != null)
            _hpText.Text = $"HP: {Health.CurrentHP}/{Health.MaxHP}";

        if (Combat != null && _apText != null)
            _apText.Text = $"AP: {Combat.CurrentAP:F0}/{Combat.MaxAP}";

        if (Level != null && _levelText != null)
            _levelText.Text = $"LVL: {Level.Level}  XP: {Level.CurrentXP}/{Level.XPToNextLevel}";

        if (StateManager != null && _stateText != null)
            _stateText.Text = StateManager.CurrentState.ToString();
    }
}
