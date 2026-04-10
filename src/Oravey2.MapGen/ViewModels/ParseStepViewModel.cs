using System.Collections.ObjectModel;
using System.Windows.Input;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.ViewModels;

public class ParseStepViewModel : BaseViewModel
{
    private PipelineState _state = new();
    private string _dataRoot = string.Empty;

    // Raw parse counts
    private int _rawTownCount;
    public int RawTownCount
    {
        get => _rawTownCount;
        private set => SetProperty(ref _rawTownCount, value);
    }

    private int _rawRoadCount;
    public int RawRoadCount
    {
        get => _rawRoadCount;
        private set => SetProperty(ref _rawRoadCount, value);
    }

    private int _rawWaterCount;
    public int RawWaterCount
    {
        get => _rawWaterCount;
        private set => SetProperty(ref _rawWaterCount, value);
    }

    private int _srtmTileCount;
    public int SrtmTileCount
    {
        get => _srtmTileCount;
        private set => SetProperty(ref _srtmTileCount, value);
    }

    // Filtered counts
    private int _filteredTownCount;
    public int FilteredTownCount
    {
        get => _filteredTownCount;
        private set => SetProperty(ref _filteredTownCount, value);
    }

    private int _filteredRoadCount;
    public int FilteredRoadCount
    {
        get => _filteredRoadCount;
        private set => SetProperty(ref _filteredRoadCount, value);
    }

    private int _filteredWaterCount;
    public int FilteredWaterCount
    {
        get => _filteredWaterCount;
        private set => SetProperty(ref _filteredWaterCount, value);
    }

    // State
    private bool _isParsing;
    public bool IsParsing
    {
        get => _isParsing;
        private set
        {
            if (SetProperty(ref _isParsing, value))
            {
                _parseCommand.RaiseCanExecuteChanged();
                _saveCommand.RaiseCanExecuteChanged();
                _cullCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _isParsed;
    public bool IsParsed
    {
        get => _isParsed;
        private set
        {
            if (SetProperty(ref _isParsed, value))
            {
                _parseCommand.RaiseCanExecuteChanged();
                _nextCommand.RaiseCanExecuteChanged();
                _saveCommand.RaiseCanExecuteChanged();
                _cullCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusText = "Ready to parse.";
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string RawSummary => IsParsed
        ? $"Parsed: {RawTownCount:N0} towns · {RawRoadCount:N0} roads · {RawWaterCount:N0} water bodies · {SrtmTileCount} SRTM tiles"
        : string.Empty;

    private bool _isCulled;
    public bool IsCulled
    {
        get => _isCulled;
        private set { if (SetProperty(ref _isCulled, value)) OnPropertyChanged(nameof(FilteredSummary)); }
    }

    public string FilteredSummary => IsParsed
        ? IsCulled
            ? $"Culled to: {FilteredTownCount:N0} towns · {FilteredRoadCount:N0} roads · {FilteredWaterCount:N0} water bodies"
            : "Not yet culled — using raw data."
        : string.Empty;

    // Lazy expandable sections
    private bool _showTownList;
    public bool ShowTownList
    {
        get => _showTownList;
        set => SetProperty(ref _showTownList, value);
    }

    private bool _showSummaryTables;
    public bool ShowSummaryTables
    {
        get => _showSummaryTables;
        set => SetProperty(ref _showSummaryTables, value);
    }

    private bool _showMapPreview;
    public bool ShowMapPreview
    {
        get => _showMapPreview;
        set => SetProperty(ref _showMapPreview, value);
    }

    private bool _showCullSettings;
    public bool ShowCullSettings
    {
        get => _showCullSettings;
        set => SetProperty(ref _showCullSettings, value);
    }

    // ---- Cull setting properties (bindable) ----
    private TownCategory _townMinCategory = TownCategory.Hamlet;
    public TownCategory CullTownMinCategory
    {
        get => _townMinCategory;
        set => SetProperty(ref _townMinCategory, value);
    }

    private int _townMinPopulation = 50;
    public int CullTownMinPopulation
    {
        get => _townMinPopulation;
        set => SetProperty(ref _townMinPopulation, value);
    }

    private double _townMinSpacingKm = 1.0;
    public double CullTownMinSpacingKm
    {
        get => _townMinSpacingKm;
        set => SetProperty(ref _townMinSpacingKm, value);
    }

    private int _townMaxCount = 200;
    public int CullTownMaxCount
    {
        get => _townMaxCount;
        set => SetProperty(ref _townMaxCount, value);
    }

    private CullPriority _townPriority = CullPriority.Category;
    public CullPriority CullTownPriority
    {
        get => _townPriority;
        set => SetProperty(ref _townPriority, value);
    }

    private bool _townAlwaysKeepCities = true;
    public bool CullTownAlwaysKeepCities
    {
        get => _townAlwaysKeepCities;
        set => SetProperty(ref _townAlwaysKeepCities, value);
    }

    private bool _townAlwaysKeepMetropolis = true;
    public bool CullTownAlwaysKeepMetropolis
    {
        get => _townAlwaysKeepMetropolis;
        set => SetProperty(ref _townAlwaysKeepMetropolis, value);
    }

    private RoadClass _roadMinClass = RoadClass.Tertiary;
    public RoadClass CullRoadMinClass
    {
        get => _roadMinClass;
        set => SetProperty(ref _roadMinClass, value);
    }

    private bool _roadAlwaysKeepMotorways = true;
    public bool CullRoadAlwaysKeepMotorways
    {
        get => _roadAlwaysKeepMotorways;
        set => SetProperty(ref _roadAlwaysKeepMotorways, value);
    }

    private bool _roadKeepNearTowns = true;
    public bool CullRoadKeepNearTowns
    {
        get => _roadKeepNearTowns;
        set => SetProperty(ref _roadKeepNearTowns, value);
    }

    private double _roadTownProximityKm = 2.0;
    public double CullRoadTownProximityKm
    {
        get => _roadTownProximityKm;
        set => SetProperty(ref _roadTownProximityKm, value);
    }

    private bool _roadRemoveDeadEnds = true;
    public bool CullRoadRemoveDeadEnds
    {
        get => _roadRemoveDeadEnds;
        set => SetProperty(ref _roadRemoveDeadEnds, value);
    }

    private double _roadDeadEndMinKm = 0.5;
    public double CullRoadDeadEndMinKm
    {
        get => _roadDeadEndMinKm;
        set => SetProperty(ref _roadDeadEndMinKm, value);
    }

    private bool _roadSimplifyGeometry = true;
    public bool CullRoadSimplifyGeometry
    {
        get => _roadSimplifyGeometry;
        set => SetProperty(ref _roadSimplifyGeometry, value);
    }

    private double _roadSimplifyToleranceM = 50.0;
    public double CullRoadSimplifyToleranceM
    {
        get => _roadSimplifyToleranceM;
        set => SetProperty(ref _roadSimplifyToleranceM, value);
    }

    private double _waterMinAreaKm2 = 0.01;
    public double CullWaterMinAreaKm2
    {
        get => _waterMinAreaKm2;
        set => SetProperty(ref _waterMinAreaKm2, value);
    }

    private double _waterMinRiverLengthKm = 1.0;
    public double CullWaterMinRiverLengthKm
    {
        get => _waterMinRiverLengthKm;
        set => SetProperty(ref _waterMinRiverLengthKm, value);
    }

    private bool _waterAlwaysKeepSea = true;
    public bool CullWaterAlwaysKeepSea
    {
        get => _waterAlwaysKeepSea;
        set => SetProperty(ref _waterAlwaysKeepSea, value);
    }

    private bool _waterAlwaysKeepLakes = true;
    public bool CullWaterAlwaysKeepLakes
    {
        get => _waterAlwaysKeepLakes;
        set => SetProperty(ref _waterAlwaysKeepLakes, value);
    }

    // Enum sources for picker binding
    public TownCategory[] TownCategories { get; } = Enum.GetValues<TownCategory>();
    public RoadClass[] RoadClasses { get; } = Enum.GetValues<RoadClass>();
    public CullPriority[] CullPriorities { get; } = Enum.GetValues<CullPriority>();

    // Town list for binding
    public ObservableCollection<TownDisplayItem> Towns { get; } = [];

    // Summary tables
    public ObservableCollection<CategoryCount> RoadsByClass { get; } = [];
    public ObservableCollection<CategoryCount> WaterByType { get; } = [];

    // The parsed result held in memory for subsequent steps
    public RegionTemplate? ParsedTemplate { get; private set; }
    public OsmExtract? RawExtract { get; private set; }

    /// <summary>Invoked when a cached template finishes loading asynchronously.</summary>
    public Action<RegionTemplate>? TemplateLoaded { get; set; }

    private bool _isLoadingCache;

    private readonly AsyncRelayCommand _parseCommand;
    private readonly RelayCommand _nextCommand;
    private readonly RelayCommand _toggleTownListCommand;
    private readonly RelayCommand _toggleSummaryCommand;
    private readonly RelayCommand _toggleMapCommand;
    private readonly RelayCommand _toggleCullSettingsCommand;
    private readonly AsyncRelayCommand _cullCommand;
    private readonly AsyncRelayCommand _saveCommand;

    public ICommand ParseCommand => _parseCommand;
    public ICommand NextCommand => _nextCommand;
    public ICommand ToggleTownListCommand => _toggleTownListCommand;
    public ICommand ToggleSummaryCommand => _toggleSummaryCommand;
    public ICommand ToggleMapCommand => _toggleMapCommand;
    public ICommand ToggleCullSettingsCommand => _toggleCullSettingsCommand;
    public ICommand CullCommand => _cullCommand;
    public ICommand SaveCommand => _saveCommand;

    public Action? StepCompleted { get; set; }

    public PipelineState GetState() => _state;

    public ParseStepViewModel()
    {
        _parseCommand = new AsyncRelayCommand(ParseAsync, () => !IsParsing);
        _nextCommand = new RelayCommand(OnNext, () => IsParsed);
        _toggleTownListCommand = new RelayCommand(() => ShowTownList = !ShowTownList);
        _toggleSummaryCommand = new RelayCommand(() => ShowSummaryTables = !ShowSummaryTables);
        _toggleMapCommand = new RelayCommand(() => ShowMapPreview = !ShowMapPreview);
        _toggleCullSettingsCommand = new RelayCommand(() => ShowCullSettings = !ShowCullSettings);
        _cullCommand = new AsyncRelayCommand(CullAsync, () => IsParsed && !IsParsing);
        _saveCommand = new AsyncRelayCommand(SaveAsync, () => IsParsed && !IsParsing);
    }

    public void Initialize(string dataRoot)
    {
        _dataRoot = dataRoot;
    }

    public void Load(PipelineState state)
    {
        _state = state;

        var templatePath = GetTemplatePath();
        if (File.Exists(templatePath))
        {
            RestoreCountsFromState(state.Parse);
            IsParsed = true;
            _isLoadingCache = true;
            StatusText = "Loading cached template...";
            _ = LoadCachedTemplateAsync(templatePath);
        }
        else if (state.Parse.Completed)
        {
            RestoreCountsFromState(state.Parse);
            IsParsed = false;
            StatusText = "Template file missing. Parse again.";
        }
    }

    private void RestoreCountsFromState(ParseStepState parse)
    {
        RawTownCount = parse.TownCount;
        RawRoadCount = parse.RoadCount;
        RawWaterCount = parse.WaterBodyCount;
        SrtmTileCount = parse.SrtmTileCount;
        FilteredTownCount = parse.FilteredTownCount;
        FilteredRoadCount = parse.FilteredRoadCount;
        FilteredWaterCount = parse.FilteredWaterBodyCount;
    }

    private async Task LoadCachedTemplateAsync(string path)
    {
        try
        {
            var template = await RegionTemplateSerializer.LoadAsync(path);
            if (template is not null)
            {
                ParsedTemplate = template;
                PopulateTownList(template.Towns);
                PopulateSummaryTables(template.Roads, template.WaterBodies);
                IsParsed = true;
                _state.Parse.TemplateSaved = true;
                OnPropertyChanged(nameof(RawSummary));
                OnPropertyChanged(nameof(FilteredSummary));
                StatusText = "Loaded from cache.";
                TemplateLoaded?.Invoke(template);
            }
            else
            {
                IsParsed = false;
                StatusText = "Cached template is corrupt. Parse again.";
            }
        }
        finally
        {
            _isLoadingCache = false;
        }
    }

    internal string GetTemplatePath()
    {
        var regionName = _state.RegionName;
        return Path.Combine(_dataRoot, "regions", regionName, "region-template.bin");
    }

    public async Task ParseAsync()
    {
        if (_isLoadingCache) return;

        IsParsing = true;
        StatusText = "Parsing OSM data...";

        try
        {
            var regionName = _state.RegionName;
            var regionDir = Path.Combine(_dataRoot, "regions", regionName);

            // Build GeoMapper centered on region
            var centerLat = (_state.Region.NorthLat + _state.Region.SouthLat) / 2;
            var centerLon = (_state.Region.EastLon + _state.Region.WestLon) / 2;
            var geoMapper = new GeoMapper(centerLat, centerLon);

            // Parse OSM
            var osmPath = Path.Combine(regionDir, "osm", $"{regionName}-latest.osm.pbf");
            var osmParser = new OsmParser(geoMapper);
            var extract = await Task.Run(() => osmParser.ParsePbf(osmPath));
            RawExtract = extract;

            RawTownCount = extract.Towns.Count;
            RawRoadCount = extract.Roads.Count;
            RawWaterCount = extract.WaterBodies.Count;

            StatusText = "Parsing SRTM elevation data...";

            // Parse SRTM tiles
            var srtmDir = Path.Combine(regionDir, "srtm");
            var srtmParser = new SrtmParser(geoMapper);
            var srtmFiles = Directory.Exists(srtmDir)
                ? Directory.GetFiles(srtmDir, "*.hgt*")
                : [];
            SrtmTileCount = srtmFiles.Length;

            float[,] elevationGrid = new float[1, 1];
            if (srtmFiles.Length > 0)
            {
                // Parse first tile as primary grid (for now)
                elevationGrid = await Task.Run(() => srtmParser.ParseHgtFile(srtmFiles[0]));
            }

            StatusText = "Building template...";

            // Build RegionTemplate with raw (unculled) data
            ParsedTemplate = new RegionTemplate
            {
                Name = regionName,
                ElevationGrid = elevationGrid,
                GridOriginLat = centerLat,
                GridOriginLon = centerLon,
                GridCellSizeMetres = 30.0,
                Towns = extract.Towns,
                Roads = extract.Roads,
                WaterBodies = extract.WaterBodies,
                Railways = extract.Railways,
                LandUseZones = extract.LandUseZones,
            };

            FilteredTownCount = extract.Towns.Count;
            FilteredRoadCount = extract.Roads.Count;
            FilteredWaterCount = extract.WaterBodies.Count;

            // Populate lazy UI data
            PopulateTownList(extract.Towns);
            PopulateSummaryTables(extract.Roads, extract.WaterBodies);

            // Save template to disk for fast reload
            try
            {
                StatusText = "Saving template cache...";
                await RegionTemplateSerializer.SaveAsync(ParsedTemplate, GetTemplatePath());
                _state.Parse.TemplateSaved = true;
            }
            catch
            {
                // Non-fatal — template is still in memory
                _state.Parse.TemplateSaved = false;
            }

            // Update pipeline state
            _state.Parse.TownCount = RawTownCount;
            _state.Parse.RoadCount = RawRoadCount;
            _state.Parse.WaterBodyCount = RawWaterCount;
            _state.Parse.SrtmTileCount = SrtmTileCount;
            _state.Parse.FilteredTownCount = FilteredTownCount;
            _state.Parse.FilteredRoadCount = FilteredRoadCount;
            _state.Parse.FilteredWaterBodyCount = FilteredWaterCount;

            IsParsed = true;
            ShowCullSettings = true;
            OnPropertyChanged(nameof(RawSummary));
            OnPropertyChanged(nameof(FilteredSummary));
            StatusText = "Parse complete. Adjust cull settings and click Cull.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsParsing = false;
        }
    }

    internal CullSettings BuildCullSettings() => new()
    {
        TownMinCategory = CullTownMinCategory,
        TownMinPopulation = CullTownMinPopulation,
        TownMinSpacingKm = CullTownMinSpacingKm,
        TownMaxCount = CullTownMaxCount,
        TownPriority = CullTownPriority,
        TownAlwaysKeepCities = CullTownAlwaysKeepCities,
        TownAlwaysKeepMetropolis = CullTownAlwaysKeepMetropolis,

        RoadMinClass = CullRoadMinClass,
        RoadAlwaysKeepMotorways = CullRoadAlwaysKeepMotorways,
        RoadKeepNearTowns = CullRoadKeepNearTowns,
        RoadTownProximityKm = CullRoadTownProximityKm,
        RoadRemoveDeadEnds = CullRoadRemoveDeadEnds,
        RoadDeadEndMinKm = CullRoadDeadEndMinKm,
        RoadSimplifyGeometry = CullRoadSimplifyGeometry,
        RoadSimplifyToleranceM = CullRoadSimplifyToleranceM,

        WaterMinAreaKm2 = CullWaterMinAreaKm2,
        WaterMinRiverLengthKm = CullWaterMinRiverLengthKm,
        WaterAlwaysKeepSea = CullWaterAlwaysKeepSea,
        WaterAlwaysKeepLakes = CullWaterAlwaysKeepLakes,
    };

    internal void LoadCullSettings(CullSettings s)
    {
        CullTownMinCategory = s.TownMinCategory;
        CullTownMinPopulation = s.TownMinPopulation;
        CullTownMinSpacingKm = s.TownMinSpacingKm;
        CullTownMaxCount = s.TownMaxCount;
        CullTownPriority = s.TownPriority;
        CullTownAlwaysKeepCities = s.TownAlwaysKeepCities;
        CullTownAlwaysKeepMetropolis = s.TownAlwaysKeepMetropolis;

        CullRoadMinClass = s.RoadMinClass;
        CullRoadAlwaysKeepMotorways = s.RoadAlwaysKeepMotorways;
        CullRoadKeepNearTowns = s.RoadKeepNearTowns;
        CullRoadTownProximityKm = s.RoadTownProximityKm;
        CullRoadRemoveDeadEnds = s.RoadRemoveDeadEnds;
        CullRoadDeadEndMinKm = s.RoadDeadEndMinKm;
        CullRoadSimplifyGeometry = s.RoadSimplifyGeometry;
        CullRoadSimplifyToleranceM = s.RoadSimplifyToleranceM;

        CullWaterMinAreaKm2 = s.WaterMinAreaKm2;
        CullWaterMinRiverLengthKm = s.WaterMinRiverLengthKm;
        CullWaterAlwaysKeepSea = s.WaterAlwaysKeepSea;
        CullWaterAlwaysKeepLakes = s.WaterAlwaysKeepLakes;
    }

    public async Task CullAsync()
    {
        if (RawExtract is null) return;

        IsParsing = true;
        StatusText = "Re-culling features...";
        try
        {
            var cullSettings = BuildCullSettings();

            var filteredTowns = await Task.Run(() => FeatureCuller.CullTowns(RawExtract.Towns, cullSettings));
            var filteredRoads = await Task.Run(() => FeatureCuller.CullRoads(RawExtract.Roads, filteredTowns, cullSettings));
            var filteredWater = await Task.Run(() => FeatureCuller.CullWater(RawExtract.WaterBodies, cullSettings));

            FilteredTownCount = filteredTowns.Count;
            FilteredRoadCount = filteredRoads.Count;
            FilteredWaterCount = filteredWater.Count;

            // Update template
            ParsedTemplate = new RegionTemplate
            {
                Name = ParsedTemplate!.Name,
                ElevationGrid = ParsedTemplate.ElevationGrid,
                GridOriginLat = ParsedTemplate.GridOriginLat,
                GridOriginLon = ParsedTemplate.GridOriginLon,
                GridCellSizeMetres = ParsedTemplate.GridCellSizeMetres,
                Towns = filteredTowns,
                Roads = filteredRoads,
                WaterBodies = filteredWater,
                Railways = ParsedTemplate.Railways,
                LandUseZones = ParsedTemplate.LandUseZones,
            };

            PopulateTownList(filteredTowns);
            PopulateSummaryTables(filteredRoads, filteredWater);

            _state.Parse.FilteredTownCount = FilteredTownCount;
            _state.Parse.FilteredRoadCount = FilteredRoadCount;
            _state.Parse.FilteredWaterBodyCount = FilteredWaterCount;

            OnPropertyChanged(nameof(FilteredSummary));
            IsCulled = true;
            StatusText = "Cull complete.";
        }
        catch (Exception ex)
        {
            StatusText = $"Cull error: {ex.Message}";
        }
        finally
        {
            IsParsing = false;
        }
    }

    public async Task SaveAsync()
    {
        if (ParsedTemplate is null)
        {
            StatusText = "Nothing to save — parse data first.";
            return;
        }

        IsParsing = true;
        StatusText = "Saving template...";
        try
        {
            var path = GetTemplatePath();
            var dir = Path.GetDirectoryName(path);
            if (dir is not null) Directory.CreateDirectory(dir);
            await RegionTemplateSerializer.SaveAsync(ParsedTemplate, path);
            _state.Parse.TemplateSaved = true;
            StatusText = $"Template saved to {path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
        finally
        {
            IsParsing = false;
        }
    }

    internal void PopulateTownList(List<TownEntry> towns)
    {
        Towns.Clear();
        foreach (var t in towns.OrderByDescending(t => t.Population))
            Towns.Add(new TownDisplayItem(t.Name, t.Population, t.Category, t.Latitude, t.Longitude));
    }

    internal void PopulateSummaryTables(List<RoadSegment> roads, List<WaterBody> water)
    {
        RoadsByClass.Clear();
        foreach (var group in roads.GroupBy(r => r.RoadClass).OrderBy(g => g.Key))
            RoadsByClass.Add(new CategoryCount(group.Key.ToString(), group.Count()));

        WaterByType.Clear();
        foreach (var group in water.GroupBy(w => w.Type).OrderBy(g => g.Key))
            WaterByType.Add(new CategoryCount(group.Key.ToString(), group.Count()));
    }

    private void OnNext()
    {
        _state.Parse.Completed = true;
        StepCompleted?.Invoke();
    }

}

public record TownDisplayItem(string Name, int Population, TownCategory Category, double Lat, double Lon);
public record CategoryCount(string Category, int Count);
