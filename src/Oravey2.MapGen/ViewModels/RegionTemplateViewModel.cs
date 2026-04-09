using System.Collections.ObjectModel;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Windows.Input;
using Oravey2.MapGen.Download;
using Oravey2.MapGen.ViewModels.RegionTemplate;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.ViewModels;

public class RegionTemplateViewModel : ViewModelBase
{
    private readonly IDataDownloadService _downloadService;
    private readonly ISettingsService? _settingsService;

    // --- Presets ---

    public ObservableCollection<RegionPreset> Presets { get; } = new();

    private RegionPreset? _selectedPreset;
    public RegionPreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value) && value is not null)
                ApplyPreset(value);
        }
    }

    // --- Source properties ---

    private string _srtmDirectory = string.Empty;
    public string SrtmDirectory { get => _srtmDirectory; set => SetProperty(ref _srtmDirectory, value); }

    private string _osmFilePath = string.Empty;
    public string OsmFilePath { get => _osmFilePath; set => SetProperty(ref _osmFilePath, value); }

    private string _regionName = string.Empty;
    public string RegionName { get => _regionName; set => SetProperty(ref _regionName, value); }

    private string _outputPath = string.Empty;
    public string OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }

    // --- Download state ---

    private bool _isDownloading;
    public bool IsDownloading { get => _isDownloading; set => SetProperty(ref _isDownloading, value); }

    private DownloadProgress? _currentDownload;
    public DownloadProgress? CurrentDownload
    {
        get => _currentDownload;
        set
        {
            if (SetProperty(ref _currentDownload, value))
            {
                OnPropertyChanged(nameof(DownloadPercent));
                OnPropertyChanged(nameof(DownloadProgress01));
            }
        }
    }

    public double DownloadPercent =>
        CurrentDownload is { TotalBytes: > 0 } d
            ? (double)d.BytesDownloaded / d.TotalBytes * 100.0
            : 0.0;

    public double DownloadProgress01 =>
        CurrentDownload is { TotalBytes: > 0 } d
            ? (double)d.BytesDownloaded / d.TotalBytes
            : 0.0;

    // --- Parsed data ---

    private OsmExtract? _parsedExtract;

    private float[,]? _elevationGrid;
    public float[,]? ElevationGrid => _elevationGrid;

    /// <summary>Raised when map data changes and the canvas should be redrawn.</summary>
    public event Action? MapInvalidated;

    // --- Feature collections ---

    public ObservableCollection<TownItem> Towns { get; } = new();
    public ObservableCollection<RoadItem> Roads { get; } = new();
    public ObservableCollection<WaterItem> WaterBodies { get; } = new();

    // --- Cull settings ---

    private CullSettings _cullSettings = new();
    public CullSettings CullSettings { get => _cullSettings; set => SetProperty(ref _cullSettings, value); }

    // --- Summary (computed) ---

    public string Summary =>
        $"{Towns.Count} towns · {Roads.Count} roads · {WaterBodies.Count} water";

    public string CulledSummary
    {
        get
        {
            int excludedTowns = Towns.Count(t => !t.IsIncluded);
            int excludedRoads = Roads.Count(r => !r.IsIncluded);
            int excludedWater = WaterBodies.Count(w => !w.IsIncluded);
            return $"(culled: {excludedTowns} towns · {excludedRoads} roads · {excludedWater} water)";
        }
    }

    // --- Log ---

    private string _logText = string.Empty;
    public string LogText { get => _logText; set => SetProperty(ref _logText, value); }

    // --- Commands ---

    public ICommand DownloadSrtmCommand { get; }
    public ICommand DownloadOsmCommand { get; }
    public ICommand ParseCommand { get; }
    public ICommand AutoCullCommand { get; }
    public ICommand BuildCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand SelectNoneCommand { get; }
    public ICommand SavePresetCommand { get; }
    public ICommand LoadCullSettingsCommand { get; }
    public ICommand SaveCullSettingsCommand { get; }

    public RegionTemplateViewModel(IDataDownloadService downloadService, ISettingsService? settingsService = null)
    {
        _downloadService = downloadService;
        _settingsService = settingsService;

        DownloadSrtmCommand = new AsyncRelayCommand(DownloadSrtmAsync, () => !IsDownloading && !IsBusy);
        DownloadOsmCommand = new AsyncRelayCommand(DownloadOsmAsync, () => !IsDownloading && !IsBusy);
        ParseCommand = new AsyncRelayCommand(ParseAsync, () => !IsBusy);
        AutoCullCommand = new RelayCommand(AutoCull);
        BuildCommand = new AsyncRelayCommand(BuildAsync, () => !IsBusy);
        SelectAllCommand = new RelayCommand(SelectAll);
        SelectNoneCommand = new RelayCommand(SelectNone);
        SavePresetCommand = new RelayCommand(SavePreset);
        LoadCullSettingsCommand = new RelayCommand(LoadCullSettings);
        SaveCullSettingsCommand = new RelayCommand(SaveCullSettings);

        LoadSettings();
    }

    // --- Preset logic ---

    public void LoadPresetsFromRegions()
    {
        Presets.Clear();
        var regionsDir = Path.Combine("data", "regions");
        if (!Directory.Exists(regionsDir)) return;

        foreach (var dir in Directory.GetDirectories(regionsDir))
        {
            var presetPath = Path.Combine(dir, "region.json");
            if (File.Exists(presetPath))
                Presets.Add(RegionPreset.Load(presetPath));
        }
    }

    private void ApplyPreset(RegionPreset preset)
    {
        SrtmDirectory = preset.SrtmDir;
        OsmFilePath = preset.OsmFilePath;
        RegionName = preset.Name;
        OutputPath = preset.OutputFilePath;
        CullSettings = preset.DefaultCullSettings;

        _parsedExtract = null;
        _elevationGrid = null;
        Towns.Clear();
        Roads.Clear();
        WaterBodies.Clear();
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(CulledSummary));
        MapInvalidated?.Invoke();
    }

    /// <summary>
    /// Apply a region preset from the region picker (sets SelectedPreset and applies all fields).
    /// </summary>
    public void ApplyRegionPreset(RegionPreset preset)
    {
        preset.EnsureDirectories();
        preset.Save(preset.PresetFilePath);

        if (!Presets.Any(p => p.Name == preset.Name))
            Presets.Add(preset);

        SelectedPreset = preset;
    }

    // --- Download commands ---

    private async Task DownloadSrtmAsync()
    {
        if (SelectedPreset is null) return;
        IsDownloading = true;
        try
        {
            string? username = null, password = null;
            if (_settingsService is not null)
            {
                username = await _settingsService.GetSecureAsync("Earthdata_Username");
                password = await _settingsService.GetSecureAsync("Earthdata_Password");
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                AppendLog("Error: Earthdata credentials not configured. Set them in Settings.");
                return;
            }

            SelectedPreset.EnsureDirectories();

            var request = new SrtmDownloadRequest(
                SelectedPreset.NorthLat, SelectedPreset.SouthLat,
                SelectedPreset.EastLon, SelectedPreset.WestLon,
                SelectedPreset.SrtmDir, username, password);

            var progress = new Progress<DownloadProgress>(p =>
            {
                CurrentDownload = p;
                AppendLog($"SRTM: {p.FilesCompleted}/{p.TotalFiles} tiles ({p.BytesDownloaded:N0} bytes)");
            });

            await _downloadService.DownloadSrtmTilesAsync(request, progress);
            AppendLog("SRTM download complete.");
        }
        catch (HttpRequestException ex)
        {
            AppendLog($"SRTM download failed: {ex.Message}");
            AppendLog("Check your Earthdata credentials in Settings.");
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private async Task DownloadOsmAsync()
    {
        if (SelectedPreset is null) return;
        IsDownloading = true;
        try
        {
            SelectedPreset.EnsureDirectories();

            var request = new OsmDownloadRequest(SelectedPreset.OsmDownloadUrl, SelectedPreset.OsmFilePath);

            var progress = new Progress<DownloadProgress>(p =>
            {
                CurrentDownload = p;
                AppendLog($"OSM: {p.BytesDownloaded:N0}/{p.TotalBytes:N0} bytes");
            });

            await _downloadService.DownloadOsmExtractAsync(request, progress);
            AppendLog("OSM download complete.");
        }
        finally
        {
            IsDownloading = false;
        }
    }

    // --- Parse command ---

    private async Task ParseAsync()
    {
        if (string.IsNullOrWhiteSpace(OsmFilePath) || !File.Exists(OsmFilePath))
        {
            AppendLog("Error: OSM file not found.");
            return;
        }

        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                if (!string.IsNullOrWhiteSpace(SrtmDirectory) && Directory.Exists(SrtmDirectory))
                {
                    var hgtFiles = Directory.GetFiles(SrtmDirectory)
                        .Where(f => f.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase)
                                  || f.EndsWith(".hgt.gz", StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    if (hgtFiles.Length > 0)
                    {
                        AppendLog($"Parsing {hgtFiles.Length} SRTM tile(s)…");
                        var srtmParser = new SrtmParser(CreateGeoMapper());
                        _elevationGrid = srtmParser.ParseHgtFile(hgtFiles[0]);
                        AppendLog($"Parsed {hgtFiles.Length} SRTM tile(s).");
                    }
                }

                AppendLog("Parsing OSM features…");
                var osmParser = new OsmParser(CreateGeoMapper());
                _parsedExtract = osmParser.ParsePbf(OsmFilePath);
                AppendLog($"Parsed OSM: {_parsedExtract.Towns.Count} towns, " +
                          $"{_parsedExtract.Roads.Count} roads, " +
                          $"{_parsedExtract.WaterBodies.Count} water bodies.");
            });

            PopulateCollections(_parsedExtract!);
            AutoCull();
            UpdateNearTowns();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PopulateCollections(OsmExtract extract)
    {
        Towns.Clear();
        Roads.Clear();
        WaterBodies.Clear();

        foreach (var t in extract.Towns) Towns.Add(new TownItem(t));
        foreach (var r in extract.Roads) Roads.Add(new RoadItem(r));
        foreach (var w in extract.WaterBodies) WaterBodies.Add(new WaterItem(w));

        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(CulledSummary));
        MapInvalidated?.Invoke();
    }

    // --- Auto-cull ---

    public void AutoCull()
    {
        var allTowns = Towns.Select(t => t.Entry).ToList();
        var survivingTowns = FeatureCuller.CullTowns(allTowns, CullSettings);
        foreach (var t in Towns)
            t.IsIncluded = survivingTowns.Contains(t.Entry);

        var includedTowns = Towns.Where(t => t.IsIncluded).Select(t => t.Entry).ToList();

        // Use settings with simplification disabled so CullRoads returns
        // the original RoadSegment references (simplification creates new objects).
        var cullingSettings = CullSettings with { RoadSimplifyGeometry = false };
        var allRoads = Roads.Select(r => r.Segment).ToList();
        var survivingRoads = FeatureCuller.CullRoads(allRoads, includedTowns, cullingSettings);
        foreach (var r in Roads)
            r.IsIncluded = survivingRoads.Contains(r.Segment);

        var allWater = WaterBodies.Select(w => w.Body).ToList();
        var survivingWater = FeatureCuller.CullWater(allWater, CullSettings);
        foreach (var w in WaterBodies)
            w.IsIncluded = survivingWater.Contains(w.Body);

        OnPropertyChanged(nameof(CulledSummary));
        MapInvalidated?.Invoke();
    }

    /// <summary>
    /// Recalculates the NearTown label for each road based on included towns.
    /// </summary>
    public void UpdateNearTowns()
    {
        var includedTowns = Towns.Where(t => t.IsIncluded).ToList();
        double proximityM = CullSettings.RoadTownProximityKm * 1000.0;

        foreach (var road in Roads)
        {
            var midpoint = road.Segment.Nodes.Length > 0
                ? road.Segment.Nodes[road.Segment.Nodes.Length / 2]
                : Vector2.Zero;

            string nearest = "—";
            double bestDist = double.MaxValue;
            foreach (var town in includedTowns)
            {
                double dist = Vector2.Distance(midpoint, town.Entry.GamePosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = town.Name;
                }
            }
            road.NearTown = bestDist <= proximityM ? nearest : "—";
        }
    }

    // --- Select all / none ---

    public void SelectAll()
    {
        foreach (var t in Towns) t.IsIncluded = true;
        foreach (var r in Roads) r.IsIncluded = true;
        foreach (var w in WaterBodies) w.IsIncluded = true;
        OnPropertyChanged(nameof(CulledSummary));
        MapInvalidated?.Invoke();
    }

    public void SelectNone()
    {
        foreach (var t in Towns) t.IsIncluded = false;
        foreach (var r in Roads) r.IsIncluded = false;
        foreach (var w in WaterBodies) w.IsIncluded = false;
        OnPropertyChanged(nameof(CulledSummary));
        MapInvalidated?.Invoke();
    }

    // --- Build ---

    private async Task BuildAsync()
    {
        if (string.IsNullOrWhiteSpace(OutputPath)) return;
        IsBusy = true;
        try
        {
            var includedTowns = Towns.Where(t => t.IsIncluded).Select(t => t.Entry).ToList();
            var includedRoads = Roads.Where(r => r.IsIncluded).Select(r => r.Segment).ToList();
            var includedWater = WaterBodies.Where(w => w.IsIncluded).Select(w => w.Body).ToList();

            var extract = new OsmExtract(
                includedTowns,
                includedRoads,
                includedWater,
                _parsedExtract?.Railways ?? [],
                _parsedExtract?.LandUseZones ?? []);

            var geoMapper = CreateGeoMapper();
            var builder = new RegionTemplateBuilder(geoMapper);

            await Task.Run(() =>
            {
                var template = builder.Build(
                    RegionName,
                    _elevationGrid ?? new float[0, 0],
                    extract,
                    geoMapper.OriginLatitude,
                    geoMapper.OriginLongitude,
                    30.0);

                var outputDir = Path.GetDirectoryName(OutputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                RegionTemplateBuilder.Serialize(template, OutputPath);
            });

            var fileInfo = new FileInfo(OutputPath);
            AppendLog($"Built template: {OutputPath} ({fileInfo.Length:N0} bytes)");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- Preset / CullSettings save/load (file-picker handled by view) ---

    private void SavePreset() { }
    private void LoadCullSettings() { }
    private void SaveCullSettings() { }

    // --- Settings persistence ---

    private void LoadSettings()
    {
        if (_settingsService is null) return;

        var presetName = _settingsService.Get("RegionTemplate_LastPreset", string.Empty);
        SrtmDirectory = _settingsService.Get("RegionTemplate_SrtmDir", string.Empty);
        OsmFilePath = _settingsService.Get("RegionTemplate_OsmFile", string.Empty);
        OutputPath = _settingsService.Get("RegionTemplate_OutputDir", string.Empty);

        var cullJson = _settingsService.Get("RegionTemplate_CullSettings", string.Empty);
        if (!string.IsNullOrEmpty(cullJson))
        {
            try
            {
                CullSettings = JsonSerializer.Deserialize<CullSettings>(cullJson) ?? new();
            }
            catch
            {
                CullSettings = new();
            }
        }

        if (!string.IsNullOrEmpty(presetName))
            SelectedPreset = Presets.FirstOrDefault(p => p.Name == presetName);
    }

    public void SaveSettings()
    {
        if (_settingsService is null) return;

        _settingsService.Set("RegionTemplate_LastPreset", SelectedPreset?.Name ?? string.Empty);
        _settingsService.Set("RegionTemplate_SrtmDir", SrtmDirectory);
        _settingsService.Set("RegionTemplate_OsmFile", OsmFilePath);
        _settingsService.Set("RegionTemplate_OutputDir", OutputPath);
        _settingsService.Set("RegionTemplate_CullSettings", JsonSerializer.Serialize(CullSettings));
    }

    // --- Helpers ---

    private void AppendLog(string message)
    {
        LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
    }

    private GeoMapper CreateGeoMapper()
    {
        if (SelectedPreset is not null)
        {
            double centerLat = (SelectedPreset.NorthLat + SelectedPreset.SouthLat) / 2.0;
            double centerLon = (SelectedPreset.EastLon + SelectedPreset.WestLon) / 2.0;
            return new GeoMapper(centerLat, centerLon);
        }
        return new GeoMapper();
    }
}
