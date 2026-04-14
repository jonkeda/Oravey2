using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.AI;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.ViewModels;

public class TownDesignStepViewModel : BaseViewModel
{
    private PipelineState _state = new();
    private string _dataRoot = string.Empty;
    private Func<string, CancellationToken, Task<string>>? _llmCall;
    private Func<string, IList<AIFunction>, CancellationToken, Task>? _toolCall;
    private CancellationTokenSource? _cts;

    // --- Town list ---
    public ObservableCollection<TownDesignItem> Towns { get; } = [];

    private TownDesignItem? _selectedTown;
    public TownDesignItem? SelectedTown
    {
        get => _selectedTown;
        set
        {
            if (SetProperty(ref _selectedTown, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                _designTownCommand.RaiseCanExecuteChanged();
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
                _designTownCommand.RaiseCanExecuteChanged();
                _designAllCommand.RaiseCanExecuteChanged();
                _acceptCommand.RaiseCanExecuteChanged();
                _regenerateCommand.RaiseCanExecuteChanged();
                _cancelCommand.RaiseCanExecuteChanged();
                _nextCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusText = "Select a town and click Design.";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private int _designedCount;
    public int DesignedCount
    {
        get => _designedCount;
        private set
        {
            if (SetProperty(ref _designedCount, value))
            {
                OnPropertyChanged(nameof(ProgressText));
                _nextCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int TotalCount => Towns.Count;
    public string ProgressText => $"{DesignedCount}/{TotalCount} designed";
    public bool AllDesigned => TotalCount > 0 && DesignedCount == TotalCount;

    public string? IncompleteWarning => !AllDesigned && DesignedCount > 0
        ? $"⚠ {TotalCount - DesignedCount} town(s) not yet designed — they won't be travelable in-game."
        : null;
    public bool HasIncompleteWarning => IncompleteWarning is not null;

    // --- LLM Log ---
    public ObservableCollection<LlmLogEntry> LlmLog { get; } = [];

    // --- Region context for LLM prompts ---
    private string _regionContext = string.Empty;

    // --- Commands ---
    private readonly AsyncRelayCommand _designTownCommand;
    private readonly AsyncRelayCommand _designAllCommand;
    private readonly RelayCommand _acceptCommand;
    private readonly AsyncRelayCommand _regenerateCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _nextCommand;

    public ICommand DesignTownCommand => _designTownCommand;
    public ICommand DesignAllCommand => _designAllCommand;
    public ICommand AcceptCommand => _acceptCommand;
    public ICommand RegenerateCommand => _regenerateCommand;
    public ICommand CancelCommand => _cancelCommand;
    public ICommand NextCommand => _nextCommand;

    public Action? StepCompleted { get; set; }

    public TownDesignStepViewModel()
    {
        _designTownCommand = new AsyncRelayCommand(DesignSelectedTownAsync,
            () => SelectedTown is not null && !IsRunning);
        _designAllCommand = new AsyncRelayCommand(DesignAllRemainingAsync,
            () => !IsRunning && Towns.Any(t => !t.IsDesigned));
        _acceptCommand = new RelayCommand(AcceptDesign,
            () => SelectedTown?.HasPendingDesign == true && !IsRunning);
        _regenerateCommand = new AsyncRelayCommand(RegenerateSelectedAsync,
            () => SelectedTown is not null && !IsRunning);
        _cancelCommand = new RelayCommand(Cancel, () => IsRunning);
        _nextCommand = new RelayCommand(OnNext, () => DesignedCount > 0 && !IsRunning);
    }

    public void Initialize(string dataRoot)
    {
        _dataRoot = dataRoot;
    }

    public void Load(PipelineState state)
    {
        _state = state;
        _regionContext = $"Region: {state.RegionName}, "
                       + $"Lat {state.Region.SouthLat:F2}–{state.Region.NorthLat:F2}, "
                       + $"Lon {state.Region.WestLon:F2}–{state.Region.EastLon:F2}";

        LoadTownsFromCuratedFile();
    }

    internal void LoadTownsFromCuratedFile()
    {
        Towns.Clear();
        var curatedPath = Path.Combine(_state.ContentPackPath, "data", "curated-towns.json");
        if (!File.Exists(curatedPath)) return;

        var curated = CuratedTownsFile.Load(curatedPath);
        foreach (var t in curated.Towns)
        {
            var item = new TownDesignItem
            {
                GameName = t.GameName,
                RealName = t.RealName,
                Description = t.Description,
                Size = t.Size,
                Inhabitants = t.Inhabitants,
                Destruction = t.Destruction,
                Latitude = t.Latitude,
                Longitude = t.Longitude,
            };

            // Check if design.json already exists
            var designPath = GetDesignPath(t.GameName);
            if (File.Exists(designPath))
            {
                var designFile = TownDesignFile.Load(designPath);
                item.Design = designFile.ToTownDesign();
                item.IsDesigned = true;
            }

            Towns.Add(item);
        }

        RefreshDesignedCount();
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ProgressText));
    }

    public void SetLlmCall(
        Func<string, CancellationToken, Task<string>> textCall,
        Func<string, IList<AIFunction>, CancellationToken, Task>? toolCall = null)
    {
        _llmCall = textCall;
        _toolCall = toolCall;
    }

    // --- Design single town ---

    public async Task DesignSelectedTownAsync()
    {
        if (SelectedTown is null) return;
        await DesignTownAsync(SelectedTown);
    }

    internal async Task DesignTownAsync(TownDesignItem item)
    {
        if (_llmCall is null)
        {
            StatusText = "Error: LLM not configured. Check Settings.";
            return;
        }

        IsRunning = true;
        StatusText = $"Designing {item.GameName}...";
        _cts = new CancellationTokenSource();

        try
        {
            var town = item.ToCuratedTown();
            var designer = new TownDesigner(_llmCall, _toolCall,
                log: (dir, msg) => LlmLog.Add(new LlmLogEntry(DateTime.Now, dir, msg)));
            var design = await designer.DesignAsync(town, _regionContext, _state.CurrentStep, _cts.Token);

            item.Design = design;
            item.HasPendingDesign = true;
            StatusText = $"Design ready for {item.GameName}. Review and Accept.";

            // Auto-accept: save immediately
            SaveDesign(item);
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

    // --- Design all remaining ---

    public async Task DesignAllRemainingAsync()
    {
        if (_llmCall is null)
        {
            StatusText = "Error: LLM not configured. Check Settings.";
            return;
        }

        IsRunning = true;
        _cts = new CancellationTokenSource();
        var remaining = Towns.Where(t => !t.IsDesigned).ToList();
        var total = remaining.Count;
        var done = 0;

        try
        {
            var designer = new TownDesigner(_llmCall, _toolCall,
                log: (dir, msg) => LlmLog.Add(new LlmLogEntry(DateTime.Now, dir, msg)));

            foreach (var item in remaining)
            {
                _cts.Token.ThrowIfCancellationRequested();
                done++;
                StatusText = $"Designing {item.GameName} ({done}/{total})...";

                var town = item.ToCuratedTown();
                var design = await designer.DesignAsync(town, _regionContext, _state.CurrentStep, _cts.Token);

                item.Design = design;
                item.HasPendingDesign = false;
                item.IsDesigned = true;

                // Save immediately
                SaveDesign(item);
                RefreshDesignedCount();
            }

            StatusText = $"All {total} towns designed.";
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Cancelled after {done - 1}/{total}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error on town {done}/{total}: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    // --- Accept / Regenerate ---

    internal void AcceptDesign()
    {
        if (SelectedTown?.Design is null) return;
        SaveDesign(SelectedTown);
    }

    internal void SaveDesign(TownDesignItem item)
    {
        if (item.Design is null) return;

        var path = GetDesignPath(item.GameName);
        var file = TownDesignFile.FromTownDesign(item.Design);
        file.Save(path);

        item.IsDesigned = true;
        item.HasPendingDesign = false;
        RefreshDesignedCount();
        UpdatePipelineState();
    }

    public async Task RegenerateSelectedAsync()
    {
        if (SelectedTown is null) return;
        SelectedTown.IsDesigned = false;
        SelectedTown.HasPendingDesign = false;
        SelectedTown.Design = null;
        RefreshDesignedCount();
        await DesignTownAsync(SelectedTown);
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }

    private void OnNext()
    {
        UpdatePipelineState();
        _state.TownDesign.Completed = true;
        StepCompleted?.Invoke();
    }

    // --- Helpers ---

    internal string GetDesignPath(string gameName)
    {
        return Path.Combine(_state.ContentPackPath, "towns", gameName, "design.json");
    }

    internal void RefreshDesignedCount()
    {
        DesignedCount = Towns.Count(t => t.IsDesigned);
        _designAllCommand.RaiseCanExecuteChanged();
        _nextCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IncompleteWarning));
        OnPropertyChanged(nameof(HasIncompleteWarning));
    }

    private void UpdatePipelineState()
    {
        _state.TownDesign.Designed = Towns.Where(t => t.IsDesigned).Select(t => t.GameName).ToList();
        _state.TownDesign.Remaining = Towns.Count(t => !t.IsDesigned);
    }

    public PipelineState GetState() => _state;
}

/// <summary>
/// Bindable item for a town in the design step.
/// </summary>
public class TownDesignItem : BaseViewModel
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

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    private bool _isDesigned;
    public bool IsDesigned
    {
        get => _isDesigned;
        set { if (SetProperty(ref _isDesigned, value)) OnPropertyChanged(nameof(StatusIcon)); }
    }

    private bool _hasPendingDesign;
    public bool HasPendingDesign
    {
        get => _hasPendingDesign;
        set => SetProperty(ref _hasPendingDesign, value);
    }

    public string StatusIcon => IsDesigned ? "✅" : "—";

    private TownDesign? _design;
    public TownDesign? Design
    {
        get => _design;
        set
        {
            if (SetProperty(ref _design, value))
            {
                OnPropertyChanged(nameof(LandmarkSummary));
                OnPropertyChanged(nameof(LandmarkCount));
                OnPropertyChanged(nameof(KeyLocationCount));
                OnPropertyChanged(nameof(LayoutStyle));
                OnPropertyChanged(nameof(HazardCount));
            }
        }
    }

    public string LandmarkSummary => Design is not null
        ? string.Join(", ", Design.Landmarks.Select(l => l.Name))
        : "";
    public int LandmarkCount => Design?.Landmarks.Count ?? 0;
    public int KeyLocationCount => Design?.KeyLocations.Count ?? 0;
    public string? LayoutStyle => Design?.LayoutStyle;
    public int HazardCount => Design?.Hazards.Count ?? 0;

    public CuratedTown ToCuratedTown() => new()
    {
        GameName = GameName, RealName = RealName, Latitude = Latitude, Longitude = Longitude,
        GamePosition = new System.Numerics.Vector2((float)(Longitude * 1000), (float)(Latitude * 1000)),
        Description = Description,
        Size = Enum.TryParse<TownCategory>(Size, true, out var sz) ? sz : TownCategory.Village,
        Inhabitants = Inhabitants,
        Destruction = Enum.TryParse<DestructionLevel>(Destruction, true, out var dl) ? dl : DestructionLevel.Moderate,
    };
}
