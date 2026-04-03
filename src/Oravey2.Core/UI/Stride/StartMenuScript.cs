using Oravey2.Core.Framework.State;
using Oravey2.Core.Save;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using Color = Stride.Core.Mathematics.Color;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Full-screen start menu shown on game launch. Offers New Game, Continue, Settings, Quit.
/// </summary>
public class StartMenuScript : SyncScript
{
    public GameStateManager? StateManager { get; set; }
    public SaveService? SaveService { get; set; }
    public SpriteFont? Font { get; set; }

    /// <summary>Called when user clicks New Scenario.</summary>
    public Action? OnNewScenario { get; set; }

    /// <summary>Called when user clicks Continue.</summary>
    public Action? OnContinue { get; set; }

    /// <summary>Called when user clicks Settings.</summary>
    public Action? OnSettings { get; set; }

    private UIComponent? _uiComponent;
    private Border? _overlay;
    private Button? _continueButton;
    private bool _pendingHide;

    /// <summary>Whether the start menu overlay is currently visible.</summary>
    public bool IsVisible => _overlay?.Visibility == Visibility.Visible;

    /// <summary>List of button labels for automation queries.</summary>
    public List<string> ButtonLabels { get; } = [];

    public override void Start()
    {
        base.Start();

        var titleText = new TextBlock
        {
            Text = "ORAVEY 2",
            Font = Font,
            TextSize = 48,
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var subtitleText = new TextBlock
        {
            Text = "Post-Apocalyptic RPG",
            Font = Font,
            TextSize = 20,
            TextColor = Color.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 40),
        };

        var newGameButton = CreateMenuButton("New Scenario");
        newGameButton.Click += (_, _) =>
        {
            OnNewScenario?.Invoke();
        };

        _continueButton = CreateMenuButton("Continue");
        _continueButton.Click += (_, _) =>
        {
            OnContinue?.Invoke();
        };

        var settingsButton = CreateMenuButton("Settings");
        settingsButton.Click += (_, _) =>
        {
            OnSettings?.Invoke();
        };

        var quitButton = CreateMenuButton("Quit");
        quitButton.Click += (_, _) =>
        {
            Environment.Exit(0);
        };

        ButtonLabels.AddRange(["New Scenario", "Continue", "Settings", "Quit"]);

        var buttonStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { titleText, subtitleText, newGameButton, _continueButton, settingsButton, quitButton },
        };

        _overlay = new Border
        {
            BackgroundColor = new Color(10, 10, 15, 230),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = buttonStack,
            Visibility = _pendingHide ? Visibility.Collapsed : Visibility.Visible,
        };

        var page = new UIPage { RootElement = _overlay };
        _uiComponent = new UIComponent { Page = page };
        Entity.Add(_uiComponent);

        UpdateContinueButton();
    }

    public override void Update()
    {
        UpdateContinueButton();
    }

    public void Show()
    {
        _pendingHide = false;
        if (_overlay != null)
            _overlay.Visibility = Visibility.Visible;
    }

    public void Hide()
    {
        _pendingHide = true;
        if (_overlay != null)
            _overlay.Visibility = Visibility.Collapsed;
    }

    private void UpdateContinueButton()
    {
        if (_continueButton == null) return;
        var hasSave = SaveService?.HasSaveFile() ?? false;
        _continueButton.Opacity = hasSave ? 1f : 0.4f;
        _continueButton.CanBeHitByUser = hasSave;
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
