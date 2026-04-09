using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Oravey2.MapGen.Download;
using Oravey2.MapGen.Pipeline;

namespace Oravey2.MapGen.ViewModels;

public class DownloadStepViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly IDataDownloadService _downloadService;
    private readonly ISettingsService _settingsService;
    private PipelineState _state = new();
    private string _dataRoot = string.Empty;
    private CancellationTokenSource? _cts;

    // SRTM
    private int _requiredSrtmCount;
    public int RequiredSrtmCount
    {
        get => _requiredSrtmCount;
        private set { if (_requiredSrtmCount != value) { _requiredSrtmCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(SrtmCountText)); } }
    }

    private int _downloadedSrtmCount;
    public int DownloadedSrtmCount
    {
        get => _downloadedSrtmCount;
        private set { if (_downloadedSrtmCount != value) { _downloadedSrtmCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(SrtmCountText)); } }
    }

    private bool _srtmReady;
    public bool SrtmReady
    {
        get => _srtmReady;
        private set
        {
            if (_srtmReady != value)
            {
                _srtmReady = value;
                OnPropertyChanged();
                _downloadSrtmCommand.RaiseCanExecuteChanged();
                _nextCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private double _srtmProgress;
    public double SrtmProgress
    {
        get => _srtmProgress;
        private set { if (Math.Abs(_srtmProgress - value) > 0.001) { _srtmProgress = value; OnPropertyChanged(); } }
    }

    private string _srtmStatusText = "Not checked";
    public string SrtmStatusText
    {
        get => _srtmStatusText;
        private set { if (_srtmStatusText != value) { _srtmStatusText = value; OnPropertyChanged(); } }
    }

    public string SrtmCountText => $"{DownloadedSrtmCount}/{RequiredSrtmCount} tiles";

    // OSM
    private bool _osmReady;
    public bool OsmReady
    {
        get => _osmReady;
        private set
        {
            if (_osmReady != value)
            {
                _osmReady = value;
                OnPropertyChanged();
                _downloadOsmCommand.RaiseCanExecuteChanged();
                _nextCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private double _osmProgress;
    public double OsmProgress
    {
        get => _osmProgress;
        private set { if (Math.Abs(_osmProgress - value) > 0.001) { _osmProgress = value; OnPropertyChanged(); } }
    }

    private string _osmStatusText = "Not checked";
    public string OsmStatusText
    {
        get => _osmStatusText;
        private set { if (_osmStatusText != value) { _osmStatusText = value; OnPropertyChanged(); } }
    }

    private string _osmFileName = string.Empty;
    public string OsmFileName
    {
        get => _osmFileName;
        private set { if (_osmFileName != value) { _osmFileName = value; OnPropertyChanged(); } }
    }

    // Download state
    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (_isDownloading != value)
            {
                _isDownloading = value;
                OnPropertyChanged();
                _downloadSrtmCommand.RaiseCanExecuteChanged();
                _downloadOsmCommand.RaiseCanExecuteChanged();
                _cancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanComplete => SrtmReady && OsmReady;

    private readonly AsyncRelayCommand _downloadSrtmCommand;
    private readonly AsyncRelayCommand _downloadOsmCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _nextCommand;

    public ICommand DownloadSrtmCommand => _downloadSrtmCommand;
    public ICommand DownloadOsmCommand => _downloadOsmCommand;
    public ICommand CancelCommand => _cancelCommand;
    public ICommand NextCommand => _nextCommand;

    public Action? StepCompleted { get; set; }

    public DownloadStepViewModel(IDataDownloadService downloadService, ISettingsService settingsService)
    {
        _downloadService = downloadService;
        _settingsService = settingsService;
        _downloadSrtmCommand = new AsyncRelayCommand(DownloadSrtmAsync, () => !IsDownloading && !SrtmReady);
        _downloadOsmCommand = new AsyncRelayCommand(DownloadOsmAsync, () => !IsDownloading && !OsmReady);
        _cancelCommand = new RelayCommand(CancelDownload, () => IsDownloading);
        _nextCommand = new RelayCommand(OnNext, () => CanComplete);
    }

    public void Initialize(PipelineState state, string dataRoot)
    {
        _state = state;
        _dataRoot = dataRoot;
        CheckExistingFiles();
    }

    public void CheckExistingFiles()
    {
        if (string.IsNullOrEmpty(_state.RegionName)) return;

        var srtmDir = GetSrtmDirectory();

        // SRTM
        var required = _downloadService.GetRequiredSrtmTileNames(
            _state.Region.NorthLat, _state.Region.SouthLat,
            _state.Region.EastLon, _state.Region.WestLon);
        RequiredSrtmCount = required.Count;

        var existing = _downloadService.GetExistingSrtmTiles(srtmDir);
        DownloadedSrtmCount = existing.Count;
        SrtmReady = DownloadedSrtmCount >= RequiredSrtmCount && RequiredSrtmCount > 0;
        SrtmProgress = RequiredSrtmCount > 0 ? (double)DownloadedSrtmCount / RequiredSrtmCount : 0;
        SrtmStatusText = SrtmReady ? "✅ Ready" : $"{DownloadedSrtmCount}/{RequiredSrtmCount} tiles downloaded";

        // OSM
        OsmFileName = $"{_state.RegionName}-latest.osm.pbf";
        var osmFile = GetOsmFilePath();
        OsmReady = File.Exists(osmFile);
        OsmProgress = OsmReady ? 1.0 : 0.0;
        OsmStatusText = OsmReady ? "✅ Ready" : "Not downloaded";
    }

    private async Task DownloadSrtmAsync()
    {
        IsDownloading = true;
        SrtmStatusText = "Downloading...";
        _cts = new CancellationTokenSource();

        try
        {
            var username = await _settingsService.GetSecureAsync("Earthdata_Username");
            var password = await _settingsService.GetSecureAsync("Earthdata_Password");

            var request = new SrtmDownloadRequest(
                _state.Region.NorthLat, _state.Region.SouthLat,
                _state.Region.EastLon, _state.Region.WestLon,
                GetSrtmDirectory(),
                username, password);

            var progress = new Progress<DownloadProgress>(p =>
            {
                SrtmProgress = p.TotalFiles > 0 ? (double)p.FilesCompleted / p.TotalFiles : 0;
                SrtmStatusText = $"Downloading {p.FileName}... ({p.FilesCompleted}/{p.TotalFiles})";
                DownloadedSrtmCount = p.FilesCompleted;
            });

            await _downloadService.DownloadSrtmTilesAsync(request, progress, _cts.Token);

            SrtmReady = true;
            SrtmProgress = 1.0;
            DownloadedSrtmCount = RequiredSrtmCount;
            _state.Download.SrtmDownloaded = true;
            SrtmStatusText = "✅ Ready";
        }
        catch (OperationCanceledException)
        {
            SrtmStatusText = "Cancelled";
        }
        catch (Exception ex)
        {
            SrtmStatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task DownloadOsmAsync()
    {
        IsDownloading = true;
        OsmStatusText = "Downloading...";
        _cts = new CancellationTokenSource();

        try
        {
            var osmUrl = _state.Region.OsmDownloadUrl
                ?? throw new InvalidOperationException("OSM download URL not set.");

            var request = new OsmDownloadRequest(osmUrl, GetOsmFilePath());

            var progress = new Progress<DownloadProgress>(p =>
            {
                OsmProgress = p.TotalBytes > 0 ? (double)p.BytesDownloaded / p.TotalBytes : 0;
                var mbDown = p.BytesDownloaded / (1024.0 * 1024.0);
                OsmStatusText = p.TotalBytes > 0
                    ? $"Downloading... {mbDown:F1} MB / {p.TotalBytes / (1024.0 * 1024.0):F1} MB"
                    : $"Downloading... {mbDown:F1} MB";
            });

            await _downloadService.DownloadOsmExtractAsync(request, progress, _cts.Token);

            OsmReady = true;
            OsmProgress = 1.0;
            _state.Download.OsmDownloaded = true;
            OsmStatusText = "✅ Ready";
        }
        catch (OperationCanceledException)
        {
            OsmStatusText = "Cancelled";
        }
        catch (Exception ex)
        {
            OsmStatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelDownload()
    {
        _cts?.Cancel();
    }

    private void OnNext()
    {
        _state.Download.SrtmDownloaded = true;
        _state.Download.OsmDownloaded = true;
        _state.Download.Completed = true;
        StepCompleted?.Invoke();
    }

    internal string GetSrtmDirectory()
        => Path.Combine(_dataRoot, "regions", _state.RegionName, "srtm");

    internal string GetOsmFilePath()
        => Path.Combine(_dataRoot, "regions", _state.RegionName, "osm", $"{_state.RegionName}-latest.osm.pbf");

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
