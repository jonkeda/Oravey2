using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using System.Windows.Input;
using Microsoft.Extensions.AI;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.ViewModels;

public enum TownSortMode { None, NameAsc, NameDesc, SizeAsc, SizeDesc }

public class TownSelectionStepViewModel : BaseViewModel
{
    private PipelineState _state = new();
    private string _dataRoot = string.Empty;
    private Func<string, CancellationToken, Task<string>>? _llmCall;
    private Func<string, IList<AIFunction>, CancellationToken, Task>? _toolCall;
    private TownGenerationParams _generationParams = TownGenerationParams.Apocalyptic;
    private CancellationTokenSource? _cts;

    // --- Mode ---
    private bool _isModeA = true;
    public bool IsModeA
    {
        get => _isModeA;
        set { if (SetProperty(ref _isModeA, value)) { OnPropertyChanged(nameof(IsModeB)); OnPropertyChanged(nameof(ModeDescription)); } }
    }
    public bool IsModeB
    {
        get => !_isModeA;
        set => IsModeA = !value;
    }

    public string ModeDescription => IsModeA
        ? "Discover: LLM invents towns from its knowledge of the region."
        : "Select: LLM picks from the parsed OSM town list.";

    // --- Min/Max Towns ---
    private int _minTowns = 8;
    public int MinTowns
    {
        get => _minTowns;
        set { if (SetProperty(ref _minTowns, value)) RefreshValidation(); }
    }

    private int _maxTowns = 15;
    public int MaxTowns
    {
        get => _maxTowns;
        set { if (SetProperty(ref _maxTowns, value)) RefreshValidation(); }
    }

    // --- State ---
    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                _runLlmCommand.RaiseCanExecuteChanged();
                _rerollAllCommand.RaiseCanExecuteChanged();
                _saveNextCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _hasResults;
    public bool HasResults
    {
        get => _hasResults;
        internal set
        {
            if (SetProperty(ref _hasResults, value))
            {
                _rerollAllCommand.RaiseCanExecuteChanged();
                _saveNextCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsListTabVisible));
                OnPropertyChanged(nameof(IsMapTabVisible));
            }
        }
    }

    private string _statusText = "Choose a mode and click Run LLM.";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    // --- Tabs ---
    private bool _isListTab = true;
    public bool IsListTab
    {
        get => _isListTab;
        set
        {
            if (SetProperty(ref _isListTab, value))
            {
                OnPropertyChanged(nameof(IsMapTab));
                OnPropertyChanged(nameof(IsListTabVisible));
                OnPropertyChanged(nameof(IsMapTabVisible));
                if (!value) MapInvalidated?.Invoke();
            }
        }
    }
    public bool IsMapTab => !IsListTab;
    public bool IsListTabVisible => HasResults && IsListTab;
    public bool IsMapTabVisible => HasResults && !IsListTab;

    // --- Sort & Search ---
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) RefreshFilteredTowns(); }
    }

    private TownSortMode _sortMode;
    public TownSortMode SortMode
    {
        get => _sortMode;
        set { if (SetProperty(ref _sortMode, value)) RefreshFilteredTowns(); }
    }

    // --- Results ---
    public ObservableCollection<TownSelectionItem> Towns { get; } = [];
    public ObservableCollection<TownSelectionItem> FilteredTowns { get; } = [];

    // --- LLM Log ---
    public ObservableCollection<LlmLogEntry> LlmLog { get; } = [];

    // --- Map ---
    public event Action? MapInvalidated;
    public double RegionNorthLat => _state.Region.NorthLat;
    public double RegionSouthLat => _state.Region.SouthLat;
    public double RegionEastLon => _state.Region.EastLon;
    public double RegionWestLon => _state.Region.WestLon;

    // --- Validation ---
    public string ValidationSummary
    {
        get
        {
            var count = Towns.Count(t => t.IsIncluded);
            var ok = count >= MinTowns && count <= MaxTowns;
            if (ok)
                return $"Towns: {count} (min {MinTowns}, max {MaxTowns}) ✓";
            if (count < MinTowns)
                return $"Towns: {count} (min {MinTowns}, max {MaxTowns}) ✗ — need at least {MinTowns}";
            return $"Towns: {count} (min {MinTowns}, max {MaxTowns}) ✗ — too many, max {MaxTowns}";
        }
    }

    public bool IsValid
    {
        get
        {
            var count = Towns.Count(t => t.IsIncluded);
            return count >= MinTowns && count <= MaxTowns;
        }
    }

    // --- Commands ---
    private readonly AsyncRelayCommand _runLlmCommand;
    private readonly AsyncRelayCommand _rerollAllCommand;
    private readonly AsyncRelayCommand _saveNextCommand;
    private readonly RelayCommand _cancelCommand;

    public ICommand RunLlmCommand => _runLlmCommand;
    public ICommand RerollAllCommand => _rerollAllCommand;
    public ICommand SaveNextCommand => _saveNextCommand;
    public ICommand CancelCommand => _cancelCommand;

    public RelayCommand<TownSelectionItem> DeleteTownCommand { get; }

    public Action? StepCompleted { get; set; }
    public Action? AddTownRequested { get; set; }

    // --- Parsed template from previous step ---
    internal RegionTemplate? ParsedTemplate { get; set; }

    /// <summary>
    /// Returns OSM towns from the parsed template that are not already in the selection.
    /// </summary>
    public List<TownEntry> GetAvailableOsmTowns()
    {
        if (ParsedTemplate is null) return [];
        var existing = Towns.Select(t => t.RealName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ParsedTemplate.Towns
            .Where(t => !existing.Contains(t.Name))
            .OrderByDescending(t => t.Population)
            .ToList();
    }

    public bool HasParsedTemplate => ParsedTemplate is not null;

    public TownSelectionStepViewModel()
    {
        _runLlmCommand = new AsyncRelayCommand(RunLlmAsync, () => !IsRunning);
        _rerollAllCommand = new AsyncRelayCommand(RerollAllAsync, () => HasResults && !IsRunning);
        _saveNextCommand = new AsyncRelayCommand(SaveAndNextAsync, () => HasResults && !IsRunning);
        _cancelCommand = new RelayCommand(Cancel);
        DeleteTownCommand = new RelayCommand<TownSelectionItem>(DeleteTown);
    }

    public void Initialize(string dataRoot,
                            TownGenerationParams? generationParams = null)
    {
        _dataRoot = dataRoot;
        _generationParams = generationParams ?? TownGenerationParams.Apocalyptic;
        MinTowns = _generationParams.MinTowns;
        MaxTowns = _generationParams.MaxTowns;
    }

    public void Load(PipelineState state)
    {
        _state = state;

        var file = GetOutputPath();
        if (File.Exists(file))
        {
            var loaded = CuratedTownsFile.Load(file);
            IsModeA = loaded.Mode == "A";
            var refLat = (state.Region.SouthLat + state.Region.NorthLat) / 2.0;
            var refLon = (state.Region.WestLon + state.Region.EastLon) / 2.0;
            PopulateTowns(loaded.Towns.Select(e => new CuratedTown
            {
                GameName = e.GameName, RealName = e.RealName, Latitude = e.Latitude, Longitude = e.Longitude,
                GamePosition = TownCurator.LatLonToMetres(e.Latitude, e.Longitude, refLat, refLon),
                Description = e.Description,
                Size = Enum.TryParse<TownCategory>(e.Size, true, out var sz) ? sz : TownCategory.Village,
                Inhabitants = e.Inhabitants,
                Destruction = Enum.TryParse<DestructionLevel>(e.Destruction, true, out var dl) ? dl : DestructionLevel.Moderate,
            }).ToList());
            HasResults = true;
            _state.TownSelection.Completed = true;
            StatusText = "Loaded from saved file.";
        }
    }

    public void SetLlmCall(
        Func<string, CancellationToken, Task<string>> textCall,
        Func<string, IList<AIFunction>, CancellationToken, Task>? toolCall = null)
    {
        _llmCall = textCall;
        _toolCall = toolCall;
    }

    public void SetParsedTemplate(RegionTemplate? template)
    {
        ParsedTemplate = template;
    }

    // --- LLM Execution ---

    public async Task RunLlmAsync()
    {
        if (_llmCall is null)
        {
            StatusText = "Error: LLM not configured. Check Settings.";
            return;
        }

        IsRunning = true;
        _cts = new CancellationTokenSource();
        StatusText = IsModeA ? "Discovering towns via LLM..." : "Selecting towns from list via LLM...";

        try
        {
            var curator = new TownCurator(_llmCall, _toolCall, _generationParams,
                log: (dir, msg) => LlmLog.Add(new LlmLogEntry(DateTime.Now, dir, msg)));
            List<CuratedTown> towns;

            if (IsModeA)
            {
                if (ParsedTemplate is null)
                {
                    StatusText = "Error: No parsed template available. Complete step 3 first.";
                    return;
                }
                towns = await curator.DiscoverAsync(ParsedTemplate, _cts.Token);
            }
            else
            {
                if (ParsedTemplate is null)
                {
                    StatusText = "Error: No parsed template available. Complete step 3 first.";
                    return;
                }
                var region = await curator.CurateAsync(ParsedTemplate, _cts.Token);
                towns = region.Towns;
            }

            PopulateTowns(towns);
            HasResults = true;
            StatusText = $"LLM returned {towns.Count} towns.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled.";
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

    public async Task RerollAllAsync()
    {
        Towns.Clear();
        HasResults = false;
        await RunLlmAsync();
    }

    public async Task RerollTownAsync(TownSelectionItem item)
    {
        if (_llmCall is null || !HasResults) return;

        IsRunning = true;
        StatusText = $"Re-rolling {item.GameName}...";
        try
        {
            var curator = new TownCurator(_llmCall, _toolCall, _generationParams,
                log: (dir, msg) => LlmLog.Add(new LlmLogEntry(DateTime.Now, dir, msg)));
            var existing = Towns.Where(t => t.IsIncluded)
                .Select(t => t.ToCuratedTown()).ToList();
            var oldTown = item.ToCuratedTown();

            if (ParsedTemplate is null)
            {
                StatusText = "Error: No parsed template available.";
                return;
            }

            var replacement = await curator.RerollTownAsync(
                ParsedTemplate, existing, oldTown);

            item.GameName = replacement.GameName;
            item.RealName = replacement.RealName;
            item.Latitude = replacement.Latitude;
            item.Longitude = replacement.Longitude;
            item.Description = replacement.Description;
            item.Size = replacement.Size;
            item.Inhabitants = replacement.Inhabitants;
            item.Destruction = replacement.Destruction;

            RefreshValidation();
            RefreshFilteredTowns();
            MapInvalidated?.Invoke();
            StatusText = $"Re-rolled: {replacement.GameName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Re-roll error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    public async Task AddTownFromOsm(TownEntry osmTown)
    {
        if (_llmCall is null)
        {
            StatusText = "Error: LLM not configured.";
            return;
        }

        IsRunning = true;
        StatusText = $"Generating metadata for {osmTown.Name}...";
        try
        {
            var curator = new TownCurator(_llmCall, _toolCall, _generationParams,
                log: (dir, msg) => LlmLog.Add(new LlmLogEntry(DateTime.Now, dir, msg)));
            var enriched = await curator.EnrichTownAsync(osmTown);

            var item = new TownSelectionItem
            {
                GameName = enriched.GameName,
                RealName = osmTown.Name,
                Latitude = osmTown.Latitude,
                Longitude = osmTown.Longitude,
                Description = enriched.Description,
                Size = osmTown.Category,
                Inhabitants = osmTown.Population,
                Destruction = enriched.Destruction,
                IsIncluded = true,
            };
            item.PropertyChanged += OnTownItemChanged;
            Towns.Add(item);
            HasResults = true;
            RefreshValidation();
            RefreshFilteredTowns();
            MapInvalidated?.Invoke();
            StatusText = $"Added: {item.GameName} ({item.RealName})";
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

    public async Task SaveAndNextAsync()
    {
        var included = Towns.Where(t => t.IsIncluded).Select(t => t.ToCuratedTown());
        var mode = IsModeA ? "A" : "B";
        var file = CuratedTownsFile.FromCuratedTowns(included, mode);

        var path = GetOutputPath();
        await Task.Run(() => file.Save(path));

        _state.TownSelection.Mode = mode;
        _state.TownSelection.TownCount = Towns.Count(t => t.IsIncluded);
        _state.TownSelection.Completed = true;
        StatusText = "Saved. Advancing...";
        StepCompleted?.Invoke();
    }

    private void AddTownFromDialog()
    {
        AddTownRequested?.Invoke();
    }

    public void DeleteTown(TownSelectionItem? item)
    {
        if (item is null) return;
        item.PropertyChanged -= OnTownItemChanged;
        Towns.Remove(item);
        RefreshValidation();
        RefreshFilteredTowns();
        MapInvalidated?.Invoke();
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }

    // --- Helpers ---

    internal void PopulateTowns(List<CuratedTown> towns)
    {
        Towns.Clear();
        foreach (var t in towns)
        {
            var item = new TownSelectionItem
            {
                GameName = t.GameName,
                RealName = t.RealName,
                Latitude = t.Latitude,
                Longitude = t.Longitude,
                Description = t.Description,
                Size = t.Size,
                Inhabitants = t.Inhabitants,
                Destruction = t.Destruction,
                IsIncluded = true,
            };
            item.PropertyChanged += OnTownItemChanged;
            Towns.Add(item);
        }
        RefreshValidation();
        RefreshFilteredTowns();
        MapInvalidated?.Invoke();
    }

    private void OnTownItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TownSelectionItem.IsIncluded))
        {
            RefreshValidation();
            MapInvalidated?.Invoke();
        }
    }

    internal void RefreshValidation()
    {
        OnPropertyChanged(nameof(ValidationSummary));
        OnPropertyChanged(nameof(IsValid));
    }

    internal void RefreshFilteredTowns()
    {
        IEnumerable<TownSelectionItem> result = Towns;

        if (!string.IsNullOrEmpty(SearchText))
        {
            result = result.Where(t =>
                t.GameName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || t.RealName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        result = SortMode switch
        {
            TownSortMode.NameAsc => result.OrderBy(t => t.GameName, StringComparer.OrdinalIgnoreCase),
            TownSortMode.NameDesc => result.OrderByDescending(t => t.GameName, StringComparer.OrdinalIgnoreCase),
            TownSortMode.SizeAsc => result.OrderBy(t => t.Size).ThenBy(t => t.GameName, StringComparer.OrdinalIgnoreCase),
            TownSortMode.SizeDesc => result.OrderByDescending(t => t.Size).ThenBy(t => t.GameName, StringComparer.OrdinalIgnoreCase),
            _ => result,
        };

        FilteredTowns.Clear();
        foreach (var item in result)
            FilteredTowns.Add(item);
    }

    internal string GetOutputPath()
    {
        return Path.Combine(_state.ContentPackPath, "data", "curated-towns.json");
    }

    public PipelineState GetState() => _state;
}

public record LlmLogEntry(DateTime Timestamp, string Direction, string Content);

/// <summary>
/// Bindable wrapper for a single curated town in the selection list.
/// </summary>
public class TownSelectionItem : BaseViewModel
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

    private double _latitude;
    public double Latitude
    {
        get => _latitude;
        set => SetProperty(ref _latitude, value);
    }

    private double _longitude;
    public double Longitude
    {
        get => _longitude;
        set => SetProperty(ref _longitude, value);
    }

    private string _description = "";
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private TownCategory _size = TownCategory.Village;
    public TownCategory Size
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

    private DestructionLevel _destruction = DestructionLevel.Moderate;
    public DestructionLevel Destruction
    {
        get => _destruction;
        set { if (SetProperty(ref _destruction, value)) OnPropertyChanged(nameof(DestructionColor)); }
    }

    public string DestructionColor => Destruction switch
    {
        DestructionLevel.Pristine => "#A6E3A1",
        DestructionLevel.Light => "#B4E3A6",
        DestructionLevel.Moderate => "#F9E2AF",
        DestructionLevel.Heavy => "#F3B48A",
        DestructionLevel.Devastated => "#F38BA8",
        _ => "#F9E2AF",
    };

    private bool _isIncluded = true;
    public bool IsIncluded
    {
        get => _isIncluded;
        set => SetProperty(ref _isIncluded, value);
    }

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set { if (SetProperty(ref _isEditing, value)) OnPropertyChanged(nameof(IsNotEditing)); }
    }

    public bool IsNotEditing => !IsEditing;

    public CuratedTown ToCuratedTown() => new()
    {
        GameName = GameName, RealName = RealName, Latitude = Latitude, Longitude = Longitude,
        GamePosition = new Vector2((float)(Longitude * 1000), (float)(Latitude * 1000)),
        Description = Description, Size = Size, Inhabitants = Inhabitants, Destruction = Destruction,
    };
}
