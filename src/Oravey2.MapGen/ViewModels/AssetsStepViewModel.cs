using System.Collections.ObjectModel;
using System.Windows.Input;
using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Models.Meshy;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.Services;

namespace Oravey2.MapGen.ViewModels;

public class AssetsStepViewModel : BaseViewModel
{
    private PipelineState _state = new();
    private MeshyClient? _meshyClient;

    // --- Town-grouped data ---
    public ObservableCollection<TownAssetGroup> Towns { get; } = [];
    public ObservableCollection<TownAssetGroup> FilteredTowns { get; } = [];

    // --- Flat building list (for counting / batch operations) ---
    internal List<BuildingItem> AllBuildings => Towns.SelectMany(t => t.Buildings).ToList();

    // --- Filter ---
    private string _filterMode = "All";
    public string FilterMode
    {
        get => _filterMode;
        set
        {
            if (SetProperty(ref _filterMode, value))
                RefreshFiltered();
        }
    }

    public List<string> FilterModes { get; } = ["All", "None", "Primitive", "Ready", "Failed"];

    // --- Selection ---
    private BuildingItem? _selectedBuilding;
    public BuildingItem? SelectedBuilding
    {
        get => _selectedBuilding;
        set
        {
            if (SetProperty(ref _selectedBuilding, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                _generateCommand.RaiseCanExecuteChanged();
                _acceptCommand.RaiseCanExecuteChanged();
                _rejectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedBuilding is not null;

    // --- State ---
    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                _generateCommand.RaiseCanExecuteChanged();
                _generateAllCommand.RaiseCanExecuteChanged();
                _acceptCommand.RaiseCanExecuteChanged();
                _rejectCommand.RaiseCanExecuteChanged();
                _cancelCommand.RaiseCanExecuteChanged();
                _nextCommand.RaiseCanExecuteChanged();
                _assignDummyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusText = "Load asset queue from designs.";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _summaryText = "";
    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    private int _readyCount;
    public int ReadyCount
    {
        get => _readyCount;
        private set
        {
            if (SetProperty(ref _readyCount, value))
            {
                OnPropertyChanged(nameof(ProgressText));
                _nextCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int TotalCount => AllBuildings.Count;
    public string ProgressText => $"{ReadyCount}/{TotalCount} ready";

    private int _progress;
    public int Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    private bool _autoAccept;
    public bool AutoAccept
    {
        get => _autoAccept;
        set => SetProperty(ref _autoAccept, value);
    }

    // --- Commands ---
    private readonly RelayCommand _generateCommand;
    private readonly RelayCommand _generateAllCommand;
    private readonly RelayCommand _acceptCommand;
    private readonly RelayCommand _rejectCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _nextCommand;
    private readonly RelayCommand _assignDummyCommand;

    public ICommand GenerateCommand => _generateCommand;
    public ICommand GenerateAllCommand => _generateAllCommand;
    public ICommand AcceptCommand => _acceptCommand;
    public ICommand RejectCommand => _rejectCommand;
    public ICommand CancelCommand => _cancelCommand;
    public ICommand NextCommand => _nextCommand;
    public ICommand AssignDummyCommand => _assignDummyCommand;
    public ICommand SelectBuildingCommand { get; }

    public Action? StepCompleted { get; set; }

    private CancellationTokenSource? _cts;

    public AssetsStepViewModel()
    {
        _generateCommand = new RelayCommand(async () => await GenerateSelectedAsync(),
            () => SelectedBuilding is not null && SelectedBuilding.Status == MeshStatus.None && !IsRunning);
        _generateAllCommand = new RelayCommand(async () => await GenerateAllAsync(),
            () => !IsRunning && AllBuildings.Any(b => b.Status == MeshStatus.None));
        _acceptCommand = new RelayCommand(AcceptSelected,
            () => SelectedBuilding?.HasResult == true && !IsRunning);
        _rejectCommand = new RelayCommand(RejectSelected,
            () => SelectedBuilding is not null && !IsRunning);
        _cancelCommand = new RelayCommand(Cancel, () => IsRunning);
        _nextCommand = new RelayCommand(OnNext, () => !IsRunning);
        _assignDummyCommand = new RelayCommand(AssignDummyMeshes, () => !IsRunning);
        SelectBuildingCommand = new RelayCommand<BuildingItem>(b => { if (b is not null) SelectedBuilding = b; });
    }

    public void SetMeshyClient(MeshyClient client)
    {
        _meshyClient = client;
    }

    public void Load(PipelineState state)
    {
        _state = state;
        LoadQueue();
    }

    internal void LoadQueue()
    {
        Towns.Clear();
        var scanner = new TownAssetScanner();
        var summaries = scanner.Scan(_state.ContentPackPath);

        foreach (var summary in summaries)
        {
            var group = new TownAssetGroup(summary.TownName, summary.GameName);
            foreach (var b in summary.Buildings)
            {
                group.Buildings.Add(new BuildingItem
                {
                    BuildingId = b.BuildingId,
                    Name = b.Name,
                    TownName = summary.TownName,
                    Role = b.Role,
                    SizeCategory = b.SizeCategory,
                    VisualDescription = b.VisualDescription,
                    CurrentMeshPath = b.CurrentMeshPath,
                    Status = b.MeshStatus,
                    Floors = b.Floors,
                    Condition = b.Condition,
                    AssetId = TownAssetScanner.DeriveAssetId(summary.TownName, b.Name),
                });
            }
            group.RefreshCounts();
            Towns.Add(group);
        }

        RefreshCounts();
        RefreshFiltered();
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ProgressText));
        StatusText = $"Loaded {TotalCount} buildings across {Towns.Count} towns. {ReadyCount} ready.";
    }

    // --- Generate single ---

    internal async Task GenerateSelectedAsync()
    {
        if (SelectedBuilding is null || _meshyClient is null) return;
        if (string.IsNullOrWhiteSpace(SelectedBuilding.VisualDescription)) return;

        IsRunning = true;
        try
        {
            await GenerateAssetAsync(SelectedBuilding);
        }
        finally
        {
            IsRunning = false;
        }
    }

    internal async Task GenerateAssetAsync(BuildingItem item)
    {
        item.Status = MeshStatus.Generating;
        StatusText = $"Generating {item.AssetId}...";
        Progress = 0;

        try
        {
            var request = new TextTo3DRequest { Mode = "preview", Prompt = item.VisualDescription, ArtStyle = "realistic" };
            var response = await _meshyClient!.CreateTextTo3DAsync(request);
            var taskId = response.Result;
            item.MeshyTaskId = taskId;

            await foreach (var status in _meshyClient.StreamTextTo3DAsync(taskId, _cts?.Token ?? CancellationToken.None))
            {
                Progress = status.Progress;

                if (status.Status == "SUCCEEDED")
                {
                    item.ThumbnailUrl = status.ThumbnailUrl;
                    item.GlbDownloadUrl = status.ModelUrls?.GetValueOrDefault("glb");
                    item.HasResult = true;
                    item.Status = MeshStatus.Ready;
                    StatusText = $"Generated {item.AssetId}.";

                    if (AutoAccept && item.GlbDownloadUrl is not null)
                        await AcceptAssetAsync(item);
                    break;
                }

                if (status.Status is "FAILED" or "CANCELED")
                {
                    item.Status = MeshStatus.Failed;
                    item.ErrorMessage = status.TaskError ?? "Generation failed.";
                    StatusText = $"Failed: {item.ErrorMessage}";
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            item.Status = MeshStatus.Failed;
            item.ErrorMessage = ex.Message;
            StatusText = $"Error: {ex.Message}";
        }

        RefreshCounts();
        RefreshFiltered();
    }

    // --- Generate all ---

    internal async Task GenerateAllAsync()
    {
        IsRunning = true;
        _cts = new CancellationTokenSource();
        var pending = AllBuildings.Where(b =>
            b.Status == MeshStatus.None && !string.IsNullOrWhiteSpace(b.VisualDescription)).ToList();
        var done = 0;

        try
        {
            foreach (var item in pending)
            {
                if (_cts.IsCancellationRequested) break;
                done++;
                StatusText = $"Generating {item.AssetId} ({done}/{pending.Count})...";
                await GenerateAssetAsync(item);
            }

            StatusText = _cts.IsCancellationRequested
                ? $"Cancelled after {done - 1}/{pending.Count}."
                : $"All {pending.Count} assets processed.";
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

    // --- Accept ---

    internal void AcceptSelected()
    {
        if (SelectedBuilding?.HasResult != true) return;
        _ = AcceptAssetAsync(SelectedBuilding);
    }

    internal async Task AcceptAssetAsync(BuildingItem item)
    {
        if (item.GlbDownloadUrl is null || _meshyClient is null)
        {
            item.Status = MeshStatus.Ready;
            RefreshCounts();
            return;
        }

        StatusText = $"Downloading {item.AssetId}.glb...";
        try
        {
            var glbBytes = await _meshyClient.DownloadModelAsync(item.GlbDownloadUrl);
            var meshDir = Path.Combine(_state.ContentPackPath, "assets", "meshes");
            Directory.CreateDirectory(meshDir);

            var glbPath = Path.Combine(meshDir, $"{item.AssetId}.glb");
            await File.WriteAllBytesAsync(glbPath, glbBytes);

            var meta = new AssetMeta
            {
                AssetId = item.AssetId,
                MeshyTaskId = item.MeshyTaskId ?? "",
                Prompt = item.VisualDescription,
                GeneratedAt = DateTime.UtcNow,
                Status = "accepted",
                SourceType = "text-to-3d",
                SizeCategory = item.SizeCategory,
            };
            AssetFiles.SaveMeta(meta, Path.Combine(meshDir, $"{item.AssetId}.meta.json"));

            UpdateBuildingReferences(item);

            item.Status = MeshStatus.Ready;
            item.CurrentMeshPath = $"meshes/{item.AssetId}.glb";
            item.HasResult = true;
            StatusText = $"Accepted {item.AssetId}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Download error: {ex.Message}";
        }

        RefreshCounts();
        RefreshFiltered();
    }

    private void UpdateBuildingReferences(BuildingItem item)
    {
        var townsDir = Path.Combine(_state.ContentPackPath, "towns");
        if (!Directory.Exists(townsDir)) return;

        var meshPath = $"meshes/{item.AssetId}.glb";
        foreach (var townDir in Directory.GetDirectories(townsDir))
        {
            AssetFiles.UpdateBuildingMeshReference(townDir, item.Name, meshPath);
        }
    }

    // --- Reject ---

    internal void RejectSelected()
    {
        if (SelectedBuilding is null) return;
        SelectedBuilding.Status = MeshStatus.None;
        SelectedBuilding.HasResult = false;
        SelectedBuilding.GlbDownloadUrl = null;
        SelectedBuilding.ThumbnailUrl = null;
        SelectedBuilding.MeshyTaskId = null;
        RefreshCounts();
        RefreshFiltered();
        StatusText = $"Rejected {SelectedBuilding.AssetId} — edit prompt and regenerate.";
    }

    // --- Dummy mesh assignment (Step 09b) ---

    internal void AssignDummyMeshes()
    {
        var townsDir = Path.Combine(_state.ContentPackPath, "towns");
        if (!Directory.Exists(townsDir)) return;

        PrimitiveMeshWriter.EnsurePrimitiveMeshes(_state.ContentPackPath);

        var readyPaths = AllBuildings
            .Where(b => b.Status == MeshStatus.Ready)
            .Select(b => b.CurrentMeshPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var assigner = new DummyMeshAssigner(readyPaths);
        var assignedCount = 0;

        foreach (var townDir in Directory.GetDirectories(townsDir))
        {
            var designPath = Path.Combine(townDir, "design.json");
            var buildingsPath = Path.Combine(townDir, "buildings.json");

            if (!File.Exists(designPath) || !File.Exists(buildingsPath)) continue;

            var design = TownDesignFile.Load(designPath).ToTownDesign();
            var mapResult = TownMapFiles.Load(townDir);

            var (updatedBuildings, updatedProps) = assigner.AssignPrimitiveMeshes(
                design, mapResult.Buildings, mapResult.Props);

            var updatedResult = new TownMapResult
            {
                Layout = mapResult.Layout, Buildings = updatedBuildings, Props = updatedProps, Zones = mapResult.Zones,
            };
            TownMapFiles.Save(updatedResult, townDir);
            assignedCount++;
        }

        // Reload to reflect changes
        LoadQueue();
        StatusText = $"Assigned primitive meshes to {assignedCount} towns.";
    }

    // --- Navigation ---

    private void Cancel()
    {
        _cts?.Cancel();
    }

    private void OnNext()
    {
        UpdatePipelineState();
        _state.Assets.Completed = true;
        StepCompleted?.Invoke();
    }

    // --- Helpers ---

    internal void RefreshCounts()
    {
        var all = AllBuildings;
        ReadyCount = all.Count(b => b.Status == MeshStatus.Ready);
        var none = all.Count(b => b.Status == MeshStatus.None);
        var primitive = all.Count(b => b.Status == MeshStatus.Primitive);
        var failed = all.Count(b => b.Status == MeshStatus.Failed);
        SummaryText = $"Towns: {Towns.Count}   Buildings: {all.Count}   Ready: {ReadyCount}   Pending: {none}   Primitive: {primitive}   Failed: {failed}";

        foreach (var town in Towns)
            town.RefreshCounts();

        _generateAllCommand.RaiseCanExecuteChanged();
        _nextCommand.RaiseCanExecuteChanged();
    }

    internal void RefreshFiltered()
    {
        FilteredTowns.Clear();
        foreach (var town in Towns)
        {
            var filtered = FilterMode switch
            {
                "None" => town.Buildings.Where(b => b.Status == MeshStatus.None).ToList(),
                "Primitive" => town.Buildings.Where(b => b.Status == MeshStatus.Primitive).ToList(),
                "Ready" => town.Buildings.Where(b => b.Status == MeshStatus.Ready).ToList(),
                "Failed" => town.Buildings.Where(b => b.Status == MeshStatus.Failed).ToList(),
                _ => town.Buildings.ToList(),
            };
            if (filtered.Count == 0) continue;

            var filteredGroup = new TownAssetGroup(town.TownName, town.GameName);
            foreach (var b in filtered)
                filteredGroup.Buildings.Add(b);
            filteredGroup.RefreshCounts();
            FilteredTowns.Add(filteredGroup);
        }
    }

    private void UpdatePipelineState()
    {
        var all = AllBuildings;
        _state.Assets.Ready = all.Count(b => b.Status == MeshStatus.Ready);
        _state.Assets.Pending = all.Count(b => b.Status == MeshStatus.None);
        _state.Assets.Failed = all.Count(b => b.Status == MeshStatus.Failed);
    }

    public PipelineState GetState() => _state;
}

/// <summary>
/// Groups buildings by town for the assets step tree view.
/// </summary>
public class TownAssetGroup : BaseViewModel
{
    public string TownName { get; }
    public string GameName { get; }
    public ObservableCollection<BuildingItem> Buildings { get; } = [];

    private int _readyCount;
    public int ReadyCount
    {
        get => _readyCount;
        private set => SetProperty(ref _readyCount, value);
    }

    public int TotalCount => Buildings.Count;

    private string _completionText = "";
    public string CompletionText
    {
        get => _completionText;
        private set => SetProperty(ref _completionText, value);
    }

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public TownAssetGroup(string townName, string gameName)
    {
        TownName = townName;
        GameName = gameName;
    }

    public void RefreshCounts()
    {
        ReadyCount = Buildings.Count(b => b.Status == MeshStatus.Ready);
        CompletionText = $"({ReadyCount}/{TotalCount})";
        OnPropertyChanged(nameof(TotalCount));
    }
}

/// <summary>
/// Bindable item for a building in the town-grouped asset queue.
/// </summary>
public class BuildingItem : BaseViewModel
{
    private string _buildingId = "";
    public string BuildingId
    {
        get => _buildingId;
        set => SetProperty(ref _buildingId, value);
    }

    private string _assetId = "";
    public string AssetId
    {
        get => _assetId;
        set => SetProperty(ref _assetId, value);
    }

    private string _name = "";
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _townName = "";
    public string TownName
    {
        get => _townName;
        set => SetProperty(ref _townName, value);
    }

    private string _role = "";
    public string Role
    {
        get => _role;
        set
        {
            if (SetProperty(ref _role, value))
                OnPropertyChanged(nameof(RoleIcon));
        }
    }

    public string RoleIcon => Role switch
    {
        "landmark" => "★",
        "key" => "●",
        _ => "○",
    };

    private string _sizeCategory = "";
    public string SizeCategory
    {
        get => _sizeCategory;
        set => SetProperty(ref _sizeCategory, value);
    }

    private string _visualDescription = "";
    public string VisualDescription
    {
        get => _visualDescription;
        set
        {
            if (SetProperty(ref _visualDescription, value))
                OnPropertyChanged(nameof(PromptSnippet));
        }
    }

    public string PromptSnippet => string.IsNullOrEmpty(VisualDescription)
        ? "(no description)"
        : VisualDescription.Length > 60
            ? VisualDescription[..60] + "..."
            : VisualDescription;

    private string _currentMeshPath = "";
    public string CurrentMeshPath
    {
        get => _currentMeshPath;
        set => SetProperty(ref _currentMeshPath, value);
    }

    private MeshStatus _status = MeshStatus.None;
    public MeshStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusLabel));
            }
        }
    }

    public string StatusIcon => Status switch
    {
        MeshStatus.None => "⚫",
        MeshStatus.Primitive => "🟡",
        MeshStatus.Generating => "🔵",
        MeshStatus.Ready => "🟢",
        MeshStatus.Failed => "🔴",
        _ => "—",
    };

    public string StatusLabel => Status switch
    {
        MeshStatus.None => "None",
        MeshStatus.Primitive => "Primitive",
        MeshStatus.Generating => "Generating",
        MeshStatus.Ready => "Ready",
        MeshStatus.Failed => "Failed",
        _ => "—",
    };

    private int _floors;
    public int Floors
    {
        get => _floors;
        set => SetProperty(ref _floors, value);
    }

    private float _condition;
    public float Condition
    {
        get => _condition;
        set => SetProperty(ref _condition, value);
    }

    private bool _hasResult;
    public bool HasResult
    {
        get => _hasResult;
        set => SetProperty(ref _hasResult, value);
    }

    public string? MeshyTaskId { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? GlbDownloadUrl { get; set; }
    public string? ErrorMessage { get; set; }
}
