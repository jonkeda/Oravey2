using Oravey2.Core.Framework.State;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using Color = Stride.Core.Mathematics.Color;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Full-screen overlay for game over and victory states.
/// Shows "GAME OVER" on player death (permanent) or "ENEMIES DEFEATED"
/// on combat victory (auto-dismisses after 2 seconds).
/// </summary>
public class GameOverOverlayScript : SyncScript
{
    public GameStateManager? StateManager { get; set; }
    public SpriteFont? Font { get; set; }

    private UIComponent? _uiComponent;
    private Border? _overlay;
    private TextBlock? _titleText;
    private TextBlock? _subtitleText;

    /// <summary>
    /// Exposes the current overlay text for automation queries.
    /// </summary>
    public string? CurrentTitle => _titleText?.Text;

    /// <summary>
    /// Exposes the current subtitle text for automation queries.
    /// </summary>
    public string? CurrentSubtitle => _subtitleText?.Text;

    /// <summary>
    /// Whether the overlay is currently visible.
    /// </summary>
    public bool IsVisible => _overlay?.Visibility == Visibility.Visible;

    private GameState _lastState;
    private float _dismissTimer;

    public override void Start()
    {
        base.Start();

        _titleText = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 48,
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        _subtitleText = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 20,
            TextColor = Color.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0),
        };

        var textStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _titleText, _subtitleText },
        };

        _overlay = new Border
        {
            BackgroundColor = new Color(0, 0, 0, 180),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = textStack,
            Visibility = Visibility.Collapsed,
        };

        var page = new UIPage { RootElement = _overlay };
        _uiComponent = new UIComponent { Page = page, RenderGroup = global::Stride.Rendering.RenderGroup.Group31 };
        Entity.Add(_uiComponent);

        _lastState = StateManager?.CurrentState ?? GameState.Loading;
    }

    public override void Update()
    {
        if (StateManager == null || _overlay == null) return;

        var currentState = StateManager.CurrentState;

        // Detect state transitions
        if (currentState != _lastState)
        {
            if (currentState == GameState.GameOver)
            {
                Show("GAME OVER", "You have been killed.");
                _dismissTimer = 0f; // Permanent
            }
            else if (_lastState == GameState.InCombat && currentState == GameState.Exploring)
            {
                Show("ENEMIES DEFEATED", "Returning to exploration...");
                _dismissTimer = 2f;
            }
            else if (_overlay.Visibility == Visibility.Visible && currentState != GameState.GameOver)
            {
                Hide();
            }

            _lastState = currentState;
        }

        // Victory auto-dismiss timer
        if (_dismissTimer > 0f)
        {
            _dismissTimer -= (float)Game.UpdateTime.Elapsed.TotalSeconds;
            if (_dismissTimer <= 0f)
            {
                _dismissTimer = 0f;
                Hide();
            }
        }
    }

    public void Show(string title, string subtitle)
    {
        _titleText!.Text = title;
        _subtitleText!.Text = subtitle;
        _overlay!.Visibility = Visibility.Visible;
    }

    public void SetTitle(string title) => _titleText!.Text = title;

    public void SetSubtitle(string subtitle) => _subtitleText!.Text = subtitle;

    public void Hide()
    {
        _overlay!.Visibility = Visibility.Collapsed;
        _titleText!.Text = "";
        _subtitleText!.Text = "";
        _dismissTimer = 0f;
    }
}
