using System.Collections.ObjectModel;
using System.Windows.Input;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.ViewModels;

public class TownMapsStepViewModel : BaseViewModel
{
    private PipelineState _state = new();
    private string _dataRoot = string.Empty;
    private RegionTemplate? _regionTemplate;

    // --- Town list ---
    public ObservableCollection<TownMapItem> Towns { get; } = [];

    private TownMapItem? _selectedTown;
    public TownMapItem? SelectedTown
    {
        get => _selectedTown;
        set
        {
            if (SetProperty(ref _selectedTown, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                _generateMapCommand.RaiseCanExecuteChanged();
                _acceptCommand.RaiseCanExecuteChanged();
                _regenerateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedTown is not null;

    // --- State ---
    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                _generateMapCommand.RaiseCanExecuteChanged();
                _generateAllCommand.RaiseCanExecuteChanged();
                _acceptCommand.RaiseCanExecuteChanged();
                _regenerateCommand.RaiseCanExecuteChanged();
                _cancelCommand.RaiseCanExecuteChanged();
                _nextCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusText = "Select a town and click Generate Map.";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private int _generatedCount;
    public int GeneratedCount
    {
        get => _generatedCount;
        private set
        {
            if (SetProperty(ref _generatedCount, value))
            {
                OnPropertyChanged(nameof(ProgressText));
                _nextCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int TotalCount => Towns.Count;
    public string ProgressText => $"{GeneratedCount}/{TotalCount} generated";
    public bool AllGenerated => TotalCount > 0 && GeneratedCount == TotalCount;

    // --- Generation parameters ---
    private GridSizeMode _selectedGridSize = GridSizeMode.Auto;
    public GridSizeMode SelectedGridSize
    {
        get => _selectedGridSize;
        set { if (SetProperty(ref _selectedGridSize, value)) OnPropertyChanged(nameof(ShowCustomDimension)); }
    }

    public bool ShowCustomDimension => SelectedGridSize == GridSizeMode.Custom;

    private int _customGridDimension = 32;
    public int CustomGridDimension
    {
        get => _customGridDimension;
        set => SetProperty(ref _customGridDimension, value);
    }

    private float _scaleFactor = 0.01f;
    public float ScaleFactor
    {
        get => _scaleFactor;
        set => SetProperty(ref _scaleFactor, value);
    }

    private int _propDensity = 70;
    public int PropDensity
    {
        get => _propDensity;
        set => SetProperty(ref _propDensity, value);
    }

    private int _maxPropsValue = 30;
    public int MaxPropsValue
    {
        get => _maxPropsValue;
        set => SetProperty(ref _maxPropsValue, value);
    }

    private int _buildingFill = 40;
    public int BuildingFill
    {
        get => _buildingFill;
        set => SetProperty(ref _buildingFill, value);
    }

    private string _seedText = "";
    public string SeedText
    {
        get => _seedText;
        set => SetProperty(ref _seedText, value);
    }

    public List<GridSizeMode> GridSizeModes { get; } =
        [GridSizeMode.Auto, GridSizeMode.Small_16, GridSizeMode.Medium_32, GridSizeMode.Large_48, GridSizeMode.Custom];

    internal MapGenerationParams BuildParams()
    {
        int? seed = int.TryParse(SeedText, out var s) ? s : null;
        return new MapGenerationParams
        {
            GridSize = SelectedGridSize,
            CustomGridDimension = CustomGridDimension,
            ScaleFactor = ScaleFactor,
            PropDensityPercent = PropDensity,
            MaxProps = MaxPropsValue,
            BuildingFillPercent = BuildingFill,
            Seed = seed,
        };
    }

    // --- Commands ---
    private readonly RelayCommand _generateMapCommand;
    private readonly RelayCommand _generateAllCommand;
    private readonly RelayCommand _acceptCommand;
    private readonly RelayCommand _regenerateCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _nextCommand;

    public ICommand GenerateMapCommand => _generateMapCommand;
    public ICommand GenerateAllCommand => _generateAllCommand;
    public ICommand AcceptCommand => _acceptCommand;
    public ICommand RegenerateCommand => _regenerateCommand;
    public ICommand CancelCommand => _cancelCommand;
    public ICommand NextCommand => _nextCommand;

    public Action? StepCompleted { get; set; }

    private CancellationTokenSource? _cts;

    public TownMapsStepViewModel()
    {
        _generateMapCommand = new RelayCommand(GenerateSelectedMap,
            () => SelectedTown is not null && !IsRunning);
        _generateAllCommand = new RelayCommand(GenerateAllMaps,
            () => !IsRunning && Towns.Any(t => !t.IsGenerated));
        _acceptCommand = new RelayCommand(AcceptMap,
            () => SelectedTown?.HasPendingMap == true && !IsRunning);
        _regenerateCommand = new RelayCommand(RegenerateSelected,
            () => SelectedTown is not null && !IsRunning);
        _cancelCommand = new RelayCommand(Cancel, () => IsRunning);
        _nextCommand = new RelayCommand(OnNext, () => AllGenerated && !IsRunning);
    }

    public void Initialize(string dataRoot)
    {
        _dataRoot = dataRoot;
    }

    public void SetRegionTemplate(RegionTemplate? template)
    {
        _regionTemplate = template;
    }

    public void Load(PipelineState state)
    {
        _state = state;
        LoadTownsFromDesigns();
    }

    internal void LoadTownsFromDesigns()
    {
        Towns.Clear();
        var curatedPath = Path.Combine(_state.ContentPackPath, "data", "curated-towns.json");
        if (!File.Exists(curatedPath)) return;

        var curated = CuratedTownsFile.Load(curatedPath);
        foreach (var t in curated.Towns)
        {
            var designPath = Path.Combine(_state.ContentPackPath, "towns", t.GameName, "design.json");
            if (!File.Exists(designPath)) continue; // skip towns without designs

            var design = TownDesignFile.Load(designPath).ToTownDesign();
            var item = new TownMapItem
            {
                GameName = t.GameName,
                RealName = t.RealName,
                Description = t.Description,
                Size = t.Size,
                Inhabitants = t.Inhabitants,
                Destruction = t.Destruction,
                Design = design,
            };

            // Check if map files already exist
            var townDir = GetTownDir(t.GameName);
            if (File.Exists(Path.Combine(townDir, "layout.json")))
            {
                var mapResult = TownMapFiles.Load(townDir);
                item.MapResult = mapResult;
                item.IsGenerated = true;
            }

            Towns.Add(item);
        }

        RefreshGeneratedCount();
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ProgressText));
    }

    // --- Generate single ---

    internal void GenerateSelectedMap()
    {
        if (SelectedTown is null) return;
        GenerateMap(SelectedTown);
    }

    internal void GenerateMap(TownMapItem item)
    {
        if (item.Design is null)
        {
            StatusText = $"Error: No design for {item.GameName}.";
            return;
        }

        IsRunning = true;
        StatusText = $"Generating map for {item.GameName}...";

        try
        {
            var town = BuildCuratedTown(item);
            var condenser = new TownMapCondenser();
            var region = _regionTemplate ?? CreateMinimalRegion();
            var result = condenser.Condense(town, item.Design, region, BuildParams());

            item.MapResult = result;
            item.HasPendingMap = true;

            // Auto-accept
            SaveMap(item);
            StatusText = $"Map generated for {item.GameName}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    // --- Generate all ---

    internal void GenerateAllMaps()
    {
        IsRunning = true;
        _cts = new CancellationTokenSource();
        var remaining = Towns.Where(t => !t.IsGenerated).ToList();
        var total = remaining.Count;
        var done = 0;

        try
        {
            var condenser = new TownMapCondenser();
            var region = _regionTemplate ?? CreateMinimalRegion();
            var baseParms = BuildParams();

            foreach (var item in remaining)
            {
                if (_cts.IsCancellationRequested) break;
                if (item.Design is null) continue;

                done++;
                StatusText = $"Generating {item.GameName} ({done}/{total})...";

                var town = BuildCuratedTown(item);
                // Use a unique seed per town if no explicit seed
                var parms = baseParms.Seed is null
                    ? baseParms with { Seed = Random.Shared.Next() }
                    : baseParms with { Seed = baseParms.Seed.Value + done };
                var result = condenser.Condense(town, item.Design, region, parms);

                item.MapResult = result;
                item.HasPendingMap = false;
                item.IsGenerated = true;
                SaveMap(item);
                RefreshGeneratedCount();
            }

            if (!_cts.IsCancellationRequested)
            {
                GenerateOverworld();
                StatusText = $"All {total} maps generated + overworld.";
            }
            else
            {
                StatusText = $"Cancelled after {done - 1}/{total}.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    // --- Accept / Regenerate ---

    internal void AcceptMap()
    {
        if (SelectedTown?.MapResult is null) return;
        SaveMap(SelectedTown);
    }

    internal void SaveMap(TownMapItem item)
    {
        if (item.MapResult is null) return;

        var townDir = GetTownDir(item.GameName);
        TownMapFiles.Save(item.MapResult, townDir);

        item.IsGenerated = true;
        item.HasPendingMap = false;
        RefreshGeneratedCount();
        UpdatePipelineState();
    }

    internal void RegenerateSelected()
    {
        if (SelectedTown is null) return;
        SelectedTown.IsGenerated = false;
        SelectedTown.HasPendingMap = false;
        SelectedTown.MapResult = null;
        RefreshGeneratedCount();
        GenerateMap(SelectedTown);
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }

    private void OnNext()
    {
        GenerateOverworld();
        UpdatePipelineState();
        _state.TownMaps.Completed = true;
        StepCompleted?.Invoke();
    }

    internal void GenerateOverworld()
    {
        var region = _regionTemplate ?? CreateMinimalRegion();
        var curatedTowns = BuildCuratedTownList();
        if (curatedTowns.Count == 0) return;

        var generator = new OverworldGenerator();
        var result = generator.Generate(region, curatedTowns, region.Name);

        var overworldDir = Path.Combine(_state.ContentPackPath, "overworld");
        OverworldFiles.Save(result, overworldDir);
    }

    private List<CuratedTown> BuildCuratedTownList() =>
        Towns.Select(t => BuildCuratedTown(t)).ToList();

    // --- Helpers ---

    internal string GetTownDir(string gameName)
    {
        return Path.Combine(_state.ContentPackPath, "towns", gameName);
    }

    internal void RefreshGeneratedCount()
    {
        GeneratedCount = Towns.Count(t => t.IsGenerated);
        _generateAllCommand.RaiseCanExecuteChanged();
        _nextCommand.RaiseCanExecuteChanged();
    }

    private void UpdatePipelineState()
    {
        _state.TownMaps.Generated = Towns.Where(t => t.IsGenerated).Select(t => t.GameName).ToList();
        _state.TownMaps.Remaining = Towns.Count(t => !t.IsGenerated);
    }

    internal static CuratedTown BuildCuratedTown(TownMapItem item) => new(
        item.GameName, item.RealName, 0, 0,
        System.Numerics.Vector2.Zero,
        item.Description,
        Enum.TryParse<TownCategory>(item.Size, true, out var sz) ? sz : TownCategory.Village,
        item.Inhabitants,
        Enum.TryParse<DestructionLevel>(item.Destruction, true, out var dl) ? dl : DestructionLevel.Moderate);

    private static RegionTemplate CreateMinimalRegion() => new()
    {
        Name = "minimal",
        ElevationGrid = new float[1, 1],
        GridOriginLat = 0,
        GridOriginLon = 0,
        GridCellSizeMetres = 100,
    };

    public PipelineState GetState() => _state;
}

/// <summary>
/// Bindable item for a town in the map generation step.
/// </summary>
public class TownMapItem : BaseViewModel
{
    private string _gameName = "";
    public string GameName
    {
        get => _gameName;
        set => SetProperty(ref _gameName, value);
    }

    private string _realName = "";
    public string RealName
    {
        get => _realName;
        set => SetProperty(ref _realName, value);
    }

    private string _description = "";
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private string _size = "";
    public string Size
    {
        get => _size;
        set => SetProperty(ref _size, value);
    }

    private int _inhabitants;
    public int Inhabitants
    {
        get => _inhabitants;
        set => SetProperty(ref _inhabitants, value);
    }

    private string _destruction = "";
    public string Destruction
    {
        get => _destruction;
        set => SetProperty(ref _destruction, value);
    }

    private bool _isGenerated;
    public bool IsGenerated
    {
        get => _isGenerated;
        set { if (SetProperty(ref _isGenerated, value)) OnPropertyChanged(nameof(StatusIcon)); }
    }

    private bool _hasPendingMap;
    public bool HasPendingMap
    {
        get => _hasPendingMap;
        set => SetProperty(ref _hasPendingMap, value);
    }

    public string StatusIcon => IsGenerated ? "✅" : "—";

    private TownDesign? _design;
    public TownDesign? Design
    {
        get => _design;
        set => SetProperty(ref _design, value);
    }

    private TownMapResult? _mapResult;
    public TownMapResult? MapResult
    {
        get => _mapResult;
        set
        {
            if (SetProperty(ref _mapResult, value))
            {
                OnPropertyChanged(nameof(BuildingCount));
                OnPropertyChanged(nameof(PropCount));
                OnPropertyChanged(nameof(ZoneCount));
                OnPropertyChanged(nameof(GridSize));
                OnPropertyChanged(nameof(StatsText));
            }
        }
    }

    public int BuildingCount => MapResult?.Buildings.Count ?? 0;
    public int PropCount => MapResult?.Props.Count ?? 0;
    public int ZoneCount => MapResult?.Zones.Count ?? 0;
    public string GridSize => MapResult is not null
        ? $"{MapResult.Layout.Width}×{MapResult.Layout.Height}"
        : "—";
    public string StatsText => MapResult is not null
        ? $"{GridSize} tiles · {BuildingCount} buildings · {PropCount} props · {ZoneCount} zones"
        : "";
}
