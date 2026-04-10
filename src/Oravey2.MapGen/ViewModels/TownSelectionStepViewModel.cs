using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using System.Windows.Input;
using Microsoft.Extensions.AI;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.ViewModels;

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

    // --- Seed ---
    private int _seed = 42;
    public int Seed
    {
        get => _seed;
        set => SetProperty(ref _seed, value);
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
        private set
        {
            if (SetProperty(ref _hasResults, value))
            {
                _rerollAllCommand.RaiseCanExecuteChanged();
                _saveNextCommand.RaiseCanExecuteChanged();
                _addTownCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusText = "Choose a mode and click Run LLM.";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    // --- Results ---
    public ObservableCollection<TownSelectionItem> Towns { get; } = [];

    // --- LLM Log ---
    public ObservableCollection<LlmLogEntry> LlmLog { get; } = [];

    // --- Validation ---
    public string ValidationSummary
    {
        get
        {
            var included = Towns.Where(t => t.IsIncluded).ToList();
            var count = included.Count;
            var countOk = count is >= 8 and <= 15;
            var hasLow = included.Any(t => t.ThreatLevel <= 3);
            var hasMid = included.Any(t => t.ThreatLevel is >= 4 and <= 6);
            var hasHigh = included.Any(t => t.ThreatLevel >= 7);

            return $"Towns: {count} {(countOk ? "✓" : "✗ (need 8–15)")}"
                   + $" · Threat: {(hasLow ? "Low✓" : "Low✗")} {(hasMid ? "Mid✓" : "Mid✗")} {(hasHigh ? "High✓" : "High✗")}";
        }
    }

    public bool IsValid
    {
        get
        {
            var included = Towns.Where(t => t.IsIncluded).ToList();
            var count = included.Count;
            if (count is < 8 or > 15) return false;
            if (!included.Any(t => t.ThreatLevel <= 3)) return false;
            if (!included.Any(t => t.ThreatLevel is >= 4 and <= 6)) return false;
            if (!included.Any(t => t.ThreatLevel >= 7)) return false;
            return true;
        }
    }

    // --- Commands ---
    private readonly AsyncRelayCommand _runLlmCommand;
    private readonly AsyncRelayCommand _rerollAllCommand;
    private readonly AsyncRelayCommand _saveNextCommand;
    private readonly RelayCommand _addTownCommand;
    private readonly RelayCommand _cancelCommand;

    public ICommand RunLlmCommand => _runLlmCommand;
    public ICommand RerollAllCommand => _rerollAllCommand;
    public ICommand SaveNextCommand => _saveNextCommand;
    public ICommand AddTownCommand => _addTownCommand;
    public ICommand CancelCommand => _cancelCommand;

    public Action? StepCompleted { get; set; }

    // --- Parsed template from previous step ---
    internal RegionTemplate? ParsedTemplate { get; set; }

    public TownSelectionStepViewModel()
    {
        _runLlmCommand = new AsyncRelayCommand(RunLlmAsync, () => !IsRunning);
        _rerollAllCommand = new AsyncRelayCommand(RerollAllAsync, () => HasResults && !IsRunning);
        _saveNextCommand = new AsyncRelayCommand(SaveAndNextAsync, () => HasResults && !IsRunning);
        _addTownCommand = new RelayCommand(AddBlankTown, () => HasResults);
        _cancelCommand = new RelayCommand(Cancel);
    }

    public void Initialize(string dataRoot,
                            TownGenerationParams? generationParams = null)
    {
        _dataRoot = dataRoot;
        _generationParams = generationParams ?? TownGenerationParams.Apocalyptic;
    }

    public void Load(PipelineState state)
    {
        _state = state;

        var file = GetOutputPath();
        if (File.Exists(file))
        {
            var loaded = CuratedTownsFile.Load(file);
            IsModeA = loaded.Mode == "A";
            Seed = loaded.Seed;
            var refLat = (state.Region.SouthLat + state.Region.NorthLat) / 2.0;
            var refLon = (state.Region.WestLon + state.Region.EastLon) / 2.0;
            PopulateTowns(loaded.Towns.Select(e => new CuratedTown(
                e.GameName, e.RealName, e.Latitude, e.Longitude,
                TownCurator.LatLonToMetres(e.Latitude, e.Longitude, refLat, refLon),
                e.Role, e.Faction, e.ThreatLevel, e.Description)).ToList());
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
                towns = await curator.DiscoverAsync(ParsedTemplate, Seed, _cts.Token);
            }
            else
            {
                if (ParsedTemplate is null)
                {
                    StatusText = "Error: No parsed template available. Complete step 3 first.";
                    return;
                }
                var region = await curator.CurateAsync(ParsedTemplate, Seed, _cts.Token);
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
                ParsedTemplate, existing, oldTown, Seed);

            item.GameName = replacement.GameName;
            item.RealName = replacement.RealName;
            item.Latitude = replacement.Latitude;
            item.Longitude = replacement.Longitude;
            item.Role = replacement.Role;
            item.Faction = replacement.Faction;
            item.ThreatLevel = replacement.ThreatLevel;
            item.Description = replacement.Description;

            RefreshValidation();
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

    public async Task SaveAndNextAsync()
    {
        var included = Towns.Where(t => t.IsIncluded).Select(t => t.ToCuratedTown());
        var mode = IsModeA ? "A" : "B";
        var file = CuratedTownsFile.FromCuratedTowns(included, mode, Seed);

        var path = GetOutputPath();
        await Task.Run(() => file.Save(path));

        _state.TownSelection.Mode = mode;
        _state.TownSelection.TownCount = Towns.Count(t => t.IsIncluded);
        _state.TownSelection.Completed = true;
        StatusText = "Saved. Advancing...";
        StepCompleted?.Invoke();
    }

    private void AddBlankTown()
    {
        var item = new TownSelectionItem
        {
            GameName = "New Town",
            RealName = "",
            Latitude = (_state.Region.NorthLat + _state.Region.SouthLat) / 2,
            Longitude = (_state.Region.EastLon + _state.Region.WestLon) / 2,
            Role = "survivor_camp",
            Faction = "",
            ThreatLevel = 5,
            Description = "",
            IsIncluded = true,
            IsEditing = true,
        };
        item.PropertyChanged += OnTownItemChanged;
        Towns.Add(item);
        RefreshValidation();
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
                Role = t.Role,
                Faction = t.Faction,
                ThreatLevel = t.ThreatLevel,
                Description = t.Description,
                IsIncluded = true,
            };
            item.PropertyChanged += OnTownItemChanged;
            Towns.Add(item);
        }
        RefreshValidation();
    }

    private void OnTownItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TownSelectionItem.IsIncluded) or nameof(TownSelectionItem.ThreatLevel))
            RefreshValidation();
    }

    internal void RefreshValidation()
    {
        OnPropertyChanged(nameof(ValidationSummary));
        OnPropertyChanged(nameof(IsValid));
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

    private string _role = "";
    public string Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    private string _faction = "";
    public string Faction
    {
        get => _faction;
        set => SetProperty(ref _faction, value);
    }

    private int _threatLevel;
    public int ThreatLevel
    {
        get => _threatLevel;
        set { if (SetProperty(ref _threatLevel, value)) OnPropertyChanged(nameof(ThreatColor)); }
    }

    public string ThreatColor => ThreatLevel switch
    {
        <= 3 => "#A6E3A1",
        <= 6 => "#F9E2AF",
        _ => "#F38BA8",
    };

    private string _description = "";
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

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

    public CuratedTown ToCuratedTown() => new(
        GameName, RealName, Latitude, Longitude,
        new Vector2((float)(Longitude * 1000), (float)(Latitude * 1000)),
        Role, Faction, ThreatLevel, Description);
}
