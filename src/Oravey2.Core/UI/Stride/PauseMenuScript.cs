using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using Color = Stride.Core.Mathematics.Color;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Pause menu overlay triggered by Escape key during Exploring or InCombat states.
/// </summary>
public class PauseMenuScript : SyncScript
{
    public GameStateManager? StateManager { get; set; }
    public IInputProvider? InputProvider { get; set; }
    public SpriteFont? Font { get; set; }

    /// <summary>Called when user clicks Save Game.</summary>
    public Action? OnSaveGame { get; set; }

    /// <summary>Called when user clicks Settings.</summary>
    public Action? OnSettings { get; set; }

    /// <summary>Called when user clicks Quit to Menu.</summary>
    public Action? OnQuitToMenu { get; set; }

    private UIComponent? _uiComponent;
    private Border? _overlay;
    private GameState _stateBeforePause;

    /// <summary>Whether the pause menu overlay is currently visible.</summary>
    public bool IsVisible => _overlay?.Visibility == Visibility.Visible;

    /// <summary>List of button labels for automation queries.</summary>
    public List<string> ButtonLabels { get; } = [];

    public override void Start()
    {
        base.Start();

        var titleText = new TextBlock
        {
            Text = "PAUSED",
            Font = Font,
            TextSize = 40,
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 30),
        };

        var resumeButton = CreateMenuButton("Resume");
        resumeButton.Click += (_, _) => Resume();

        var saveButton = CreateMenuButton("Save Game");
        saveButton.Click += (_, _) =>
        {
            OnSaveGame?.Invoke();
        };

        var settingsButton = CreateMenuButton("Settings");
        settingsButton.Click += (_, _) =>
        {
            OnSettings?.Invoke();
        };

        var quitButton = CreateMenuButton("Quit to Menu");
        quitButton.Click += (_, _) =>
        {
            OnQuitToMenu?.Invoke();
        };

        ButtonLabels.AddRange(["Resume", "Save Game", "Settings", "Quit to Menu"]);

        var buttonStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { titleText, resumeButton, saveButton, settingsButton, quitButton },
        };

        _overlay = new Border
        {
            BackgroundColor = new Color(0, 0, 0, 180),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = buttonStack,
            Visibility = Visibility.Collapsed,
        };

        var page = new UIPage { RootElement = _overlay };
        _uiComponent = new UIComponent { Page = page };
        Entity.Add(_uiComponent);
    }

    public override void Update()
    {
        if (StateManager == null || InputProvider == null) return;

        // Escape toggles pause
        if (InputProvider.IsActionPressed(GameAction.Pause))
        {
            if (StateManager.CurrentState == GameState.Paused)
            {
                Resume();
            }
            else if (StateManager.CurrentState is GameState.Exploring or GameState.InCombat)
            {
                Pause();
            }
        }
    }

    public void Pause()
    {
        if (StateManager == null) return;
        _stateBeforePause = StateManager.CurrentState;
        StateManager.TransitionTo(GameState.Paused);
        if (_overlay != null)
            _overlay.Visibility = Visibility.Visible;
    }

    public void Resume()
    {
        if (StateManager == null) return;
        StateManager.TransitionTo(_stateBeforePause);
        if (_overlay != null)
            _overlay.Visibility = Visibility.Collapsed;
    }

    private Button CreateMenuButton(string text)
    {
        return new Button
        {
            Content = new TextBlock
            {
                Text = text,
                Font = Font,
                TextSize = 24,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
            },
            BackgroundColor = new Color(60, 60, 80, 200),
            HorizontalAlignment = HorizontalAlignment.Center,
            MinimumWidth = 250,
            MinimumHeight = 50,
            Margin = new Thickness(0, 8, 0, 8),
        };
    }
}
