using global::Stride.Engine;
using global::Stride.Graphics;
using global::Stride.UI;
using global::Stride.UI.Controls;
using global::Stride.UI.Panels;
using Oravey2.Core.Descriptions;
using Oravey2.Core.Input;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Info panel that slides in from the right when a POI is clicked.
/// Shows three-tier descriptions: tagline → summary → dossier.
/// </summary>
public class LocationInfoPanelScript : SyncScript
{
    public IInputProvider? InputProvider { get; set; }
    public SpriteFont? Font { get; set; }

    private UIComponent? _uiComponent;
    private TextBlock? _nameText;
    private TextBlock? _typeText;
    private TextBlock? _taglineText;
    private TextBlock? _summaryText;
    private TextBlock? _dossierText;
    private TextBlock? _statusText;
    private bool _visible;
    private string _currentTier = "tagline"; // tagline, summary, dossier

    // Currently displayed description state
    private string _locationName = "";
    private string _locationType = "";
    private string _tagline = "";
    private string? _summary;
    private string? _dossier;
    private int _currentLocationId;

    /// <summary>Exposes panel visibility for automation queries.</summary>
    public bool IsVisible => _visible;

    /// <summary>Current display tier: tagline, summary, or dossier.</summary>
    public string CurrentTier => _currentTier;

    /// <summary>Name of the currently displayed location.</summary>
    public string LocationName => _locationName;

    /// <summary>Type of the currently displayed location.</summary>
    public string LocationTypeName => _locationType;

    /// <summary>Currently displayed tagline text.</summary>
    public string Tagline => _tagline;

    /// <summary>Currently displayed summary text, if expanded.</summary>
    public string? Summary => _summary;

    /// <summary>Currently displayed dossier text, if expanded.</summary>
    public string? Dossier => _dossier;

    /// <summary>Id of the currently shown location.</summary>
    public int CurrentLocationId => _currentLocationId;

    public override void Start()
    {
        base.Start();
        BuildUI();
    }

    public override void Update()
    {
        // I key toggles info for nearest POI (close if open)
        if (InputProvider?.IsActionPressed(GameAction.Info) == true)
        {
            if (_visible)
                Hide();
        }
    }

    /// <summary>
    /// Shows the info panel with the given location data.
    /// </summary>
    public void Show(int locationId, string name, string type, string tagline,
        string? summary = null, string? dossier = null)
    {
        _currentLocationId = locationId;
        _locationName = name;
        _locationType = type;
        _tagline = tagline;
        _summary = summary;
        _dossier = dossier;
        _currentTier = "tagline";
        _visible = true;

        RefreshUI();

        if (_uiComponent?.Page?.RootElement != null)
            _uiComponent.Page.RootElement.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Expands the panel to show summary text.
    /// </summary>
    public void ExpandToSummary(string summary)
    {
        _summary = summary;
        _currentTier = "summary";
        RefreshUI();
    }

    /// <summary>
    /// Expands the panel to show the full dossier.
    /// </summary>
    public void ExpandToDossier(string dossier)
    {
        _dossier = dossier;
        _currentTier = "dossier";
        RefreshUI();
    }

    /// <summary>
    /// Shows a loading message in the status area.
    /// </summary>
    public void ShowLoading()
    {
        if (_statusText != null)
            _statusText.Text = "┄┄┄ Loading ┄┄┄";
    }

    /// <summary>
    /// Clears the loading message.
    /// </summary>
    public void ClearLoading()
    {
        if (_statusText != null)
            _statusText.Text = "";
    }

    /// <summary>
    /// Hides the info panel.
    /// </summary>
    public void Hide()
    {
        _visible = false;
        if (_uiComponent?.Page?.RootElement != null)
            _uiComponent.Page.RootElement.Visibility = Visibility.Collapsed;
    }

    private void RefreshUI()
    {
        if (_nameText != null) _nameText.Text = _locationName.ToUpperInvariant();
        if (_typeText != null) _typeText.Text = _locationType;
        if (_taglineText != null) _taglineText.Text = _tagline;

        if (_summaryText != null)
        {
            _summaryText.Text = _summary ?? "";
            _summaryText.Visibility = _currentTier is "summary" or "dossier" && _summary != null
                ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_dossierText != null)
        {
            _dossierText.Text = _dossier ?? "";
            _dossierText.Visibility = _currentTier == "dossier" && _dossier != null
                ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_statusText != null)
            _statusText.Text = "";
    }

    private void BuildUI()
    {
        var white = global::Stride.Core.Mathematics.Color.White;
        var grey = new global::Stride.Core.Mathematics.Color(180, 180, 180);
        var amber = new global::Stride.Core.Mathematics.Color(255, 200, 80);

        _nameText = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 20,
            TextColor = amber,
            Margin = new Thickness(10, 10, 10, 2)
        };

        _typeText = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 14,
            TextColor = grey,
            Margin = new Thickness(10, 0, 10, 8)
        };

        _taglineText = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 14,
            TextColor = white,
            Margin = new Thickness(10, 0, 10, 5),
            WrapText = true
        };

        _summaryText = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 13,
            TextColor = grey,
            Margin = new Thickness(10, 5, 10, 5),
            WrapText = true,
            Visibility = Visibility.Collapsed
        };

        _dossierText = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 12,
            TextColor = grey,
            Margin = new Thickness(10, 5, 10, 5),
            WrapText = true,
            Visibility = Visibility.Collapsed
        };

        _statusText = new TextBlock
        {
            Text = "",
            Font = Font,
            TextSize = 12,
            TextColor = new global::Stride.Core.Mathematics.Color(150, 150, 150),
            Margin = new Thickness(10, 5, 10, 10)
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 320,
            BackgroundColor = new global::Stride.Core.Mathematics.Color(20, 20, 20, 200),
            Children = { _nameText, _typeText, _taglineText, _summaryText, _dossierText, _statusText }
        };

        _uiComponent = Entity.Get<UIComponent>();
        if (_uiComponent != null)
        {
            _uiComponent.Page = new UIPage { RootElement = panel };
            panel.Visibility = Visibility.Collapsed;
        }
    }
}
