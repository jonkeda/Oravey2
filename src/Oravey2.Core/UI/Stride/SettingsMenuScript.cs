using Oravey2.Core.Audio;
using Oravey2.Core.Save;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using Color = Stride.Core.Mathematics.Color;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Settings screen for volume and auto-save. Accessible from start menu and pause menu.
/// </summary>
public class SettingsMenuScript : SyncScript
{
    public VolumeSettings? Volume { get; set; }
    public AutoSaveTracker? AutoSave { get; set; }
    public SpriteFont? Font { get; set; }

    /// <summary>Called when user clicks Back.</summary>
    public Action? OnBack { get; set; }

    private UIComponent? _uiComponent;
    private Border? _overlay;
    private Slider? _masterSlider;
    private Slider? _musicSlider;
    private Slider? _sfxSlider;
    private TextBlock? _masterValueText;
    private TextBlock? _musicValueText;
    private TextBlock? _sfxValueText;
    private ToggleButton? _autoSaveToggle;

    /// <summary>Whether the settings overlay is currently visible.</summary>
    public bool IsVisible => _overlay?.Visibility == Visibility.Visible;

    public override void Start()
    {
        base.Start();

        var titleText = new TextBlock
        {
            Text = "SETTINGS",
            Font = Font,
            TextSize = 36,
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 30),
        };

        // Volume sliders
        (_masterSlider, _masterValueText) = CreateSliderRow("Master Volume");
        (_musicSlider, _musicValueText) = CreateSliderRow("Music Volume");
        (_sfxSlider, _sfxValueText) = CreateSliderRow("SFX Volume");

        // Auto-save toggle
        _autoSaveToggle = new ToggleButton
        {
            Content = new TextBlock
            {
                Text = "Auto-Save",
                Font = Font,
                TextSize = 18,
                TextColor = Color.White,
            },
            State = (AutoSave?.Enabled ?? true) ? ToggleState.Checked : ToggleState.UnChecked,
            Margin = new Thickness(0, 16, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
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
            MinimumHeight = 45,
            Margin = new Thickness(0, 30, 0, 0),
        };
        backButton.Click += (_, _) => OnBack?.Invoke();

        var contentStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                titleText,
                CreateLabel("Master Volume"),
                _masterSlider, _masterValueText,
                CreateLabel("Music Volume"),
                _musicSlider, _musicValueText,
                CreateLabel("SFX Volume"),
                _sfxSlider, _sfxValueText,
                _autoSaveToggle,
                backButton,
            },
        };

        _overlay = new Border
        {
            BackgroundColor = new Color(10, 10, 15, 220),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = contentStack,
            Visibility = Visibility.Collapsed,
        };

        var page = new UIPage { RootElement = _overlay };
        _uiComponent = new UIComponent { Page = page, RenderGroup = global::Stride.Rendering.RenderGroup.Group31 };
        Entity.Add(_uiComponent);

        // Initialize slider positions from current volume settings
        if (Volume != null)
        {
            _masterSlider.Value = Volume.GetVolume(AudioCategory.Master);
            _musicSlider.Value = Volume.GetVolume(AudioCategory.Music);
            _sfxSlider.Value = Volume.GetVolume(AudioCategory.SFX);
        }
        UpdateValueLabels();
    }

    public override void Update()
    {
        if (_overlay?.Visibility != Visibility.Visible) return;

        // Apply slider values to volume settings
        if (Volume != null)
        {
            Volume.SetVolume(AudioCategory.Master, _masterSlider?.Value ?? 0.8f);
            Volume.SetVolume(AudioCategory.Music, _musicSlider?.Value ?? 0.6f);
            Volume.SetVolume(AudioCategory.SFX, _sfxSlider?.Value ?? 0.8f);
        }

        if (AutoSave != null && _autoSaveToggle != null)
        {
            AutoSave.Enabled = _autoSaveToggle.State == ToggleState.Checked;
        }

        UpdateValueLabels();
    }

    public void Show()
    {
        if (_overlay != null)
            _overlay.Visibility = Visibility.Visible;
    }

    public void Hide()
    {
        if (_overlay != null)
            _overlay.Visibility = Visibility.Collapsed;
    }

    private void UpdateValueLabels()
    {
        if (_masterSlider != null && _masterValueText != null)
            _masterValueText.Text = $"{(int)(_masterSlider.Value * 100)}%";
        if (_musicSlider != null && _musicValueText != null)
            _musicValueText.Text = $"{(int)(_musicSlider.Value * 100)}%";
        if (_sfxSlider != null && _sfxValueText != null)
            _sfxValueText.Text = $"{(int)(_sfxSlider.Value * 100)}%";
    }

    private (Slider slider, TextBlock valueText) CreateSliderRow(string _)
    {
        var slider = new Slider
        {
            Minimum = 0f,
            Maximum = 1f,
            Value = 0.8f,
            HorizontalAlignment = HorizontalAlignment.Center,
            MinimumWidth = 250,
            Margin = new Thickness(0, 4, 0, 2),
        };

        var valueText = new TextBlock
        {
            Text = "80%",
            Font = Font,
            TextSize = 14,
            TextColor = Color.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        return (slider, valueText);
    }

    private TextBlock CreateLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Font = Font,
            TextSize = 18,
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0),
        };
    }
}
