using System.Windows.Input;
using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Models;
using Oravey2.MapGen.Services;

namespace Oravey2.MapGen.App.ViewModels;

public sealed class GeneratorViewModel : BaseViewModel
{
    private readonly MapGeneratorService _service;
    private CancellationTokenSource? _cts;

    // --- Input fields ---
    private string _locationName = string.Empty;
    public string LocationName { get => _locationName; set => SetProperty(ref _locationName, value); }

    private string _geographyDescription = string.Empty;
    public string GeographyDescription { get => _geographyDescription; set => SetProperty(ref _geographyDescription, value); }

    private string _postApocContext = string.Empty;
    public string PostApocContext { get => _postApocContext; set => SetProperty(ref _postApocContext, value); }

    private int _chunksWide = 4;
    public int ChunksWide { get => _chunksWide; set => SetProperty(ref _chunksWide, value); }

    private int _chunksHigh = 4;
    public int ChunksHigh { get => _chunksHigh; set => SetProperty(ref _chunksHigh, value); }

    private int _minLevel = 1;
    public int MinLevel { get => _minLevel; set => SetProperty(ref _minLevel, value); }

    private int _maxLevel = 5;
    public int MaxLevel { get => _maxLevel; set => SetProperty(ref _maxLevel, value); }

    private string _difficultyDescription = string.Empty;
    public string DifficultyDescription { get => _difficultyDescription; set => SetProperty(ref _difficultyDescription, value); }

    private string _factions = string.Empty;
    public string Factions { get => _factions; set => SetProperty(ref _factions, value); }

    private string _timeOfDay = "Dawn";
    public string TimeOfDay { get => _timeOfDay; set => SetProperty(ref _timeOfDay, value); }

    private string _weatherDefault = "overcast";
    public string WeatherDefault { get => _weatherDefault; set => SetProperty(ref _weatherDefault, value); }

    // --- Output ---
    private string _streamingLog = string.Empty;
    public string StreamingLog { get => _streamingLog; private set => SetProperty(ref _streamingLog, value); }

    private string _statusMessage = "Ready";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private bool _isGenerating;
    public bool IsGenerating { get => _isGenerating; private set => SetProperty(ref _isGenerating, value); }

    private string? _lastGeneratedJson;
    public string? LastGeneratedJson { get => _lastGeneratedJson; private set => SetProperty(ref _lastGeneratedJson, value); }

    private string? _lastSessionId;
    public string? LastSessionId { get => _lastSessionId; private set => SetProperty(ref _lastSessionId, value); }

    // --- Commands ---
    public Command GenerateCommand { get; }
    public Command CancelCommand { get; }
    public Command SaveBlueprintCommand { get; }
    public Command CopyJsonCommand { get; }
    public Command CompileBlueprintCommand { get; }

    private string? _lastCompileOutputDir;
    public string? LastCompileOutputDir { get => _lastCompileOutputDir; private set => SetProperty(ref _lastCompileOutputDir, value); }

    public GeneratorViewModel(MapGeneratorService service)
    {
        _service = service;
        _service.OnProgress += OnProgress;

        GenerateCommand = new Command(async () => await GenerateAsync(), () => !IsGenerating);
        CancelCommand = new Command(Cancel, () => IsGenerating);
        SaveBlueprintCommand = new Command(async () => await SaveBlueprintAsync(), () => LastGeneratedJson is not null);
        CopyJsonCommand = new Command(async () => await CopyJsonAsync(), () => LastGeneratedJson is not null);
        CompileBlueprintCommand = new Command(CompileBlueprint, () => LastGeneratedJson is not null);
    }

    private async Task GenerateAsync()
    {
        IsGenerating = true;
        StreamingLog = string.Empty;
        StatusMessage = "Generating...";
        _cts = new CancellationTokenSource();

        // Apply current settings to service
        _service.CliPath = Preferences.Get("CliPath", string.Empty);
        _service.UseBYOK = Preferences.Get("UseBYOK", false);
        _service.ProviderType = Preferences.Get("ProviderType", string.Empty);
        _service.BaseUrl = Preferences.Get("BaseUrl", string.Empty);
        try { _service.ApiKey = await SecureStorage.GetAsync("ApiKey"); } catch { }

        // Load content pack asset catalog if configured
        var packPath = Preferences.Get("ContentPackPath", string.Empty);
        if (!string.IsNullOrWhiteSpace(packPath))
        {
            var catalogPath = Path.Combine(packPath, "catalog.json");
            if (File.Exists(catalogPath))
            {
                try
                {
                    _service.AssetRegistryOverride = Oravey2.MapGen.Assets.AssetRegistry.LoadFromFile(catalogPath);
                }
                catch
                {
                    _service.AssetRegistryOverride = null;
                }
            }
            else
            {
                _service.AssetRegistryOverride = null;
            }
        }
        else
        {
            _service.AssetRegistryOverride = null;
        }

        try
        {
            var request = new MapGenerationRequest
            {
                LocationName = LocationName,
                GeographyDescription = GeographyDescription,
                PostApocContext = PostApocContext,
                ChunksWide = ChunksWide,
                ChunksHigh = ChunksHigh,
                MinLevel = MinLevel,
                MaxLevel = MaxLevel,
                DifficultyDescription = DifficultyDescription,
                Factions = Factions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                TimeOfDay = TimeOfDay,
                WeatherDefault = WeatherDefault
            };

            var result = await _service.GenerateAsync(request, _cts.Token);

            if (result.Success)
            {
                LastGeneratedJson = result.RawJson;
                LastSessionId = result.SessionId;
                StatusMessage = $"Complete ({result.Elapsed.TotalSeconds:F1}s)";
            }
            else
            {
                StatusMessage = $"Error: {result.ErrorMessage}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled";
        }
        finally
        {
            IsGenerating = false;
            _cts?.Dispose();
            _cts = null;

            GenerateCommand.ChangeCanExecute();
            CancelCommand.ChangeCanExecute();
            SaveBlueprintCommand.ChangeCanExecute();
            CopyJsonCommand.ChangeCanExecute();
            CompileBlueprintCommand.ChangeCanExecute();
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }

    private async Task SaveBlueprintAsync()
    {
        if (LastGeneratedJson is null) return;

        var packPath = Preferences.Get("ContentPackPath", string.Empty);
        string savePath;

        if (!string.IsNullOrWhiteSpace(packPath) && Directory.Exists(packPath))
        {
            var blueprintsDir = Path.Combine(packPath, "blueprints");
            Directory.CreateDirectory(blueprintsDir);

            var safeName = string.IsNullOrWhiteSpace(LocationName)
                ? "blueprint"
                : string.Join("_", LocationName.Split(Path.GetInvalidFileNameChars()));

            savePath = Path.Combine(blueprintsDir, $"{safeName}.json");
        }
        else
        {
            var exportPath = Preferences.Get("ExportPath",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oravey2", "Blueprints"));
            Directory.CreateDirectory(exportPath);

            var safeName = string.IsNullOrWhiteSpace(LocationName)
                ? "blueprint"
                : string.Join("_", LocationName.Split(Path.GetInvalidFileNameChars()));

            savePath = Path.Combine(exportPath, $"{safeName}_{DateTime.Now:yyyyMMdd-HHmmss}.json");
        }

        await File.WriteAllTextAsync(savePath, LastGeneratedJson);
        StatusMessage = $"Saved to {savePath}";
    }

    private async Task CopyJsonAsync()
    {
        if (LastGeneratedJson is null) return;
        await Clipboard.SetTextAsync(LastGeneratedJson);
        StatusMessage = "JSON copied to clipboard";
    }

    private void CompileBlueprint()
    {
        if (LastGeneratedJson is null) return;

        try
        {
            var blueprint = BlueprintLoader.LoadFromString(LastGeneratedJson);

            var safeName = string.IsNullOrWhiteSpace(LocationName)
                ? "blueprint"
                : string.Join("_", LocationName.Split(Path.GetInvalidFileNameChars()));

            var packPath = Preferences.Get("ContentPackPath", string.Empty);
            string outputDir;

            if (!string.IsNullOrWhiteSpace(packPath) && Directory.Exists(packPath))
            {
                outputDir = Path.Combine(packPath, "maps", safeName);
            }
            else
            {
                var exportPath = Preferences.Get("ExportPath",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oravey2", "Blueprints"));
                outputDir = Path.Combine(exportPath, "compiled", safeName);
            }

            var result = MapCompiler.Compile(blueprint, outputDir);

            if (result.Success)
            {
                LastCompileOutputDir = outputDir;
                StatusMessage = $"Compiled {result.ChunksGenerated} chunks, {result.BuildingsPlaced} buildings, {result.PropsPlaced} props → {outputDir}";
            }
            else
            {
                StatusMessage = $"Compile failed: {string.Join("; ", result.Warnings.Select(w => w.Message))}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Compile error: {ex.Message}";
        }
    }

    private void OnProgress(GenerationProgress progress)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (progress.StreamDelta is not null)
                StreamingLog += progress.StreamDelta;
            else if (progress.ToolName is not null)
                StreamingLog += $"\n[Tool: {progress.ToolName}]{(progress.ToolResult is not null ? $" → {progress.ToolResult}" : "")}\n";
            else
                StreamingLog += $"\n{progress.Message}\n";

            StatusMessage = progress.Message;
        });
    }
}
