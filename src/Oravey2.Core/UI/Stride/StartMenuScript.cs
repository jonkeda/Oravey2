using Oravey2.Core.Content;
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
    public ContentPackService? ContentPacks { get; set; }
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
    private TextBlock? _subtitleText;
    private TextBlock? _packButtonText;
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
            Text = ContentPacks?.ActivePack?.Manifest.Name ?? "Post-Apocalyptic RPG",
            Font = Font,
            TextSize = 20,
            TextColor = Color.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 12),
        };
        _subtitleText = subtitleText;

        // Content pack cycle button (only if multiple packs installed)
        Button? packButton = null;
        if (ContentPacks != null && ContentPacks.Packs.Count > 1)
        {
            _packButtonText = new TextBlock
            {
                Text = $"Content Pack: {ContentPacks.ActivePack?.Manifest.Name ?? "None"}",
                Font = Font,
                TextSize = 18,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            packButton = new Button
            {
                Content = _packButtonText,
                BackgroundColor = new Color(40, 80, 60, 200),
                HorizontalAlignment = HorizontalAlignment.Center,
                MinimumWidth = 250,
                MinimumHeight = 40,
                Margin = new Thickness(0, 0, 0, 20),
            };
            packButton.Click += (_, _) => CycleContentPack();
            ButtonLabels.Add("Content Pack");
        }

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
            Children = { titleText, subtitleText },
        };
        if (packButton != null)
            buttonStack.Children.Add(packButton);
        buttonStack.Children.Add(newGameButton);
        buttonStack.Children.Add(_continueButton);
        buttonStack.Children.Add(settingsButton);
        buttonStack.Children.Add(quitButton);

        _overlay = new Border
        {
            BackgroundColor = new Color(10, 10, 15, 230),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = buttonStack,
            Visibility = _pendingHide ? Visibility.Collapsed : Visibility.Visible,
        };

        var page = new UIPage { RootElement = _overlay };
        _uiComponent = new UIComponent { Page = page, RenderGroup = global::Stride.Rendering.RenderGroup.Group31 };
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

    private void CycleContentPack()
    {
        if (ContentPacks == null || ContentPacks.Packs.Count == 0) return;

        var packs = ContentPacks.Packs;
        var currentIndex = -1;
        if (ContentPacks.ActivePack != null)
        {
            for (int i = 0; i < packs.Count; i++)
            {
                if (packs[i].Manifest.Id == ContentPacks.ActivePack.Manifest.Id)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        var nextIndex = (currentIndex + 1) % packs.Count;
        ContentPacks.SetActivePack(packs[nextIndex].Manifest.Id);

        if (_packButtonText != null)
            _packButtonText.Text = $"Content Pack: {ContentPacks.ActivePack?.Manifest.Name ?? "None"}";
        if (_subtitleText != null)
            _subtitleText.Text = ContentPacks.ActivePack?.Manifest.Name ?? "Oravey 2";
    }
}
