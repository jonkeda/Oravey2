using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.ViewModels;

public class RegionStepViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private PipelineState _state = new();

    private string _regionName = string.Empty;
    public string RegionName
    {
        get => _regionName;
        private set { if (_regionName != value) { _regionName = value; OnPropertyChanged(); } }
    }

    private string _regionDisplayName = string.Empty;
    public string RegionDisplayName
    {
        get => _regionDisplayName;
        private set
        {
            if (_regionDisplayName != value)
            {
                _regionDisplayName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasRegion));
                OnPropertyChanged(nameof(BoundingBoxText));
                _nextCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private double _northLat;
    public double NorthLat
    {
        get => _northLat;
        private set { if (_northLat != value) { _northLat = value; OnPropertyChanged(); OnPropertyChanged(nameof(BoundingBoxText)); } }
    }

    private double _southLat;
    public double SouthLat
    {
        get => _southLat;
        private set { if (_southLat != value) { _southLat = value; OnPropertyChanged(); OnPropertyChanged(nameof(BoundingBoxText)); } }
    }

    private double _eastLon;
    public double EastLon
    {
        get => _eastLon;
        private set { if (_eastLon != value) { _eastLon = value; OnPropertyChanged(); OnPropertyChanged(nameof(BoundingBoxText)); } }
    }

    private double _westLon;
    public double WestLon
    {
        get => _westLon;
        private set { if (_westLon != value) { _westLon = value; OnPropertyChanged(); OnPropertyChanged(nameof(BoundingBoxText)); } }
    }

    private string _osmUrl = string.Empty;
    public string OsmUrl
    {
        get => _osmUrl;
        private set { if (_osmUrl != value) { _osmUrl = value; OnPropertyChanged(); } }
    }

    public bool HasRegion => !string.IsNullOrEmpty(RegionDisplayName);

    public string BoundingBoxText => HasRegion
        ? $"N {NorthLat:F4}°  S {SouthLat:F4}°  E {EastLon:F4}°  W {WestLon:F4}°"
        : string.Empty;

    public ObservableCollection<string> ContentPacks { get; } = [];

    private string? _selectedContentPack;
    public string? SelectedContentPack
    {
        get => _selectedContentPack;
        set
        {
            if (_selectedContentPack != value)
            {
                _selectedContentPack = value;
                OnPropertyChanged();
                _nextCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanComplete => HasRegion && !string.IsNullOrEmpty(SelectedContentPack);

    private readonly RelayCommand _nextCommand;
    public ICommand NextCommand => _nextCommand;

    public Action? StepCompleted { get; set; }

    public RegionStepViewModel()
    {
        _nextCommand = new RelayCommand(OnNext, () => CanComplete);
    }

    public void Initialize(PipelineState state, string? contentRoot = null)
    {
        _state = state;

        if (!string.IsNullOrEmpty(state.Region.PresetName))
        {
            RegionName = state.Region.PresetName;
            RegionDisplayName = state.Region.PresetName;
            NorthLat = state.Region.NorthLat;
            SouthLat = state.Region.SouthLat;
            EastLon = state.Region.EastLon;
            WestLon = state.Region.WestLon;
            OsmUrl = state.Region.OsmDownloadUrl ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(state.ContentPackPath))
            SelectedContentPack = state.ContentPackPath;

        if (contentRoot is not null)
            ScanContentPacks(contentRoot);
    }

    public void ApplyRegion(RegionPreset preset)
    {
        RegionName = preset.Name;
        RegionDisplayName = preset.DisplayName;
        NorthLat = preset.NorthLat;
        SouthLat = preset.SouthLat;
        EastLon = preset.EastLon;
        WestLon = preset.WestLon;
        OsmUrl = preset.OsmDownloadUrl;

        _state.RegionName = preset.Name;
        _state.Region.PresetName = preset.Name;
        _state.Region.NorthLat = preset.NorthLat;
        _state.Region.SouthLat = preset.SouthLat;
        _state.Region.EastLon = preset.EastLon;
        _state.Region.WestLon = preset.WestLon;
        _state.Region.OsmDownloadUrl = preset.OsmDownloadUrl;
    }

    public void ScanContentPacks(string contentRoot)
    {
        ContentPacks.Clear();
        if (!Directory.Exists(contentRoot)) return;

        foreach (var dir in Directory.GetDirectories(contentRoot).Order())
        {
            if (File.Exists(Path.Combine(dir, "manifest.json")))
                ContentPacks.Add(Path.GetFileName(dir));
        }
    }

    private void OnNext()
    {
        _state.Region.Completed = true;
        _state.ContentPackPath = SelectedContentPack ?? string.Empty;
        StepCompleted?.Invoke();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
