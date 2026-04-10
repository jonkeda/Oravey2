using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using Oravey2.MapGen.Pipeline;

namespace Oravey2.MapGen.ViewModels;

public class AssemblyStepViewModel : BaseViewModel
{
    private readonly ContentPackAssembler _assembler = new();
    private PipelineState _state = new();

    public ObservableCollection<ValidationItemViewModel> ValidationItems { get; } = [];

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                _validateCommand.RaiseCanExecuteChanged();
                _generateScenarioCommand.RaiseCanExecuteChanged();
                _rebuildCatalogCommand.RaiseCanExecuteChanged();
                _updateManifestCommand.RaiseCanExecuteChanged();
                _completeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusText = "Validate and assemble the content pack.";
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

    private bool _isValidated;
    public bool IsValidated
    {
        get => _isValidated;
        private set
        {
            if (SetProperty(ref _isValidated, value))
                _completeCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _validationPassed;
    public bool ValidationPassed
    {
        get => _validationPassed;
        private set
        {
            if (SetProperty(ref _validationPassed, value))
                _completeCommand.RaiseCanExecuteChanged();
        }
    }

    // --- Scenario settings ---
    private string _scenarioId = "";
    public string ScenarioId
    {
        get => _scenarioId;
        set => SetProperty(ref _scenarioId, value);
    }

    private string _scenarioName = "";
    public string ScenarioName
    {
        get => _scenarioName;
        set => SetProperty(ref _scenarioName, value);
    }

    private string _scenarioDescription = "";
    public string ScenarioDescription
    {
        get => _scenarioDescription;
        set => SetProperty(ref _scenarioDescription, value);
    }

    private int _difficulty = 3;
    public int Difficulty
    {
        get => _difficulty;
        set => SetProperty(ref _difficulty, value);
    }

    // --- Commands ---
    private readonly RelayCommand _validateCommand;
    private readonly RelayCommand _generateScenarioCommand;
    private readonly RelayCommand _rebuildCatalogCommand;
    private readonly RelayCommand _updateManifestCommand;
    private readonly RelayCommand _completeCommand;

    public ICommand ValidateCommand => _validateCommand;
    public ICommand GenerateScenarioCommand => _generateScenarioCommand;
    public ICommand RebuildCatalogCommand => _rebuildCatalogCommand;
    public ICommand UpdateManifestCommand => _updateManifestCommand;
    public ICommand CompleteCommand => _completeCommand;

    public Action? StepCompleted { get; set; }

    public AssemblyStepViewModel()
    {
        _validateCommand = new RelayCommand(RunValidation, () => !IsRunning);
        _generateScenarioCommand = new RelayCommand(RunGenerateScenario, () => !IsRunning);
        _rebuildCatalogCommand = new RelayCommand(RunRebuildCatalog, () => !IsRunning);
        _updateManifestCommand = new RelayCommand(RunUpdateManifest, () => !IsRunning);
        _completeCommand = new RelayCommand(OnComplete, () => !IsRunning && IsValidated && ValidationPassed);
    }

    public void Load(PipelineState state)
    {
        _state = state;
        LoadScenarioDefaults();
    }

    internal void LoadScenarioDefaults()
    {
        if (string.IsNullOrEmpty(_state.ContentPackPath)) return;

        // Derive defaults from region name
        var regionName = _state.RegionName;
        if (!string.IsNullOrEmpty(regionName))
        {
            ScenarioId = regionName.ToLowerInvariant().Replace(' ', '-');
            ScenarioName = $"{regionName} Wastes";
            ScenarioDescription = $"Survive the ruins of {regionName}...";
        }

        // Try to load existing scenario
        var scenariosDir = Path.Combine(_state.ContentPackPath, "scenarios");
        if (Directory.Exists(scenariosDir))
        {
            var existing = Directory.GetFiles(scenariosDir, "*.json").FirstOrDefault();
            if (existing is not null)
            {
                try
                {
                    var doc = JsonDocument.Parse(File.ReadAllText(existing));
                    var root = doc.RootElement;
                    if (root.TryGetProperty("id", out var id))
                        ScenarioId = id.GetString() ?? ScenarioId;
                    if (root.TryGetProperty("name", out var name))
                        ScenarioName = name.GetString() ?? ScenarioName;
                    if (root.TryGetProperty("description", out var desc))
                        ScenarioDescription = desc.GetString() ?? ScenarioDescription;
                    if (root.TryGetProperty("difficulty", out var diff))
                        Difficulty = diff.GetInt32();
                }
                catch { /* use defaults */ }
            }
        }
    }

    internal void RunValidation()
    {
        if (string.IsNullOrEmpty(_state.ContentPackPath)) return;

        IsRunning = true;
        try
        {
            ValidationItems.Clear();
            var result = _assembler.Validate(_state.ContentPackPath);

            foreach (var item in result.Items)
                ValidationItems.Add(new ValidationItemViewModel(item));

            IsValidated = true;
            ValidationPassed = result.Passed;
            SummaryText = $"✓ {result.PassCount}  ⚠ {result.WarningCount}  ✗ {result.ErrorCount}";
            StatusText = result.Passed
                ? "Validation passed. Pack is ready."
                : $"Validation found {result.ErrorCount} error(s). Fix issues and re-validate.";
        }
        finally
        {
            IsRunning = false;
        }
    }

    internal void RunGenerateScenario()
    {
        if (string.IsNullOrEmpty(_state.ContentPackPath)) return;

        IsRunning = true;
        try
        {
            var townNames = GetTownGameNames();
            var settings = new ScenarioSettings
            {
                Id = ScenarioId,
                Name = ScenarioName,
                Description = ScenarioDescription,
                Difficulty = Difficulty,
                Tags = ["exploration", "apocalyptic"],
                PlayerStart = townNames.Count > 0
                    ? new PlayerStartInfo { Town = townNames[0], TileX = 5, TileY = 5 }
                    : null,
            };

            _assembler.GenerateScenario(_state.ContentPackPath, townNames, settings);
            StatusText = $"Scenario '{ScenarioId}' generated with {townNames.Count} towns.";
        }
        finally
        {
            IsRunning = false;
        }
    }

    internal void RunRebuildCatalog()
    {
        if (string.IsNullOrEmpty(_state.ContentPackPath)) return;

        IsRunning = true;
        try
        {
            _assembler.RebuildCatalog(_state.ContentPackPath);
            StatusText = "Catalog rebuilt from assets.";
        }
        finally
        {
            IsRunning = false;
        }
    }

    internal void RunUpdateManifest()
    {
        if (string.IsNullOrEmpty(_state.ContentPackPath)) return;

        IsRunning = true;
        try
        {
            _assembler.UpdateManifest(_state.ContentPackPath, new ManifestUpdate
            {
                Version = "0.1.0",
            });
            StatusText = "Manifest updated.";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void OnComplete()
    {
        _state.Assembly.Completed = true;
        _state.Assembly.Validated = true;
        StepCompleted?.Invoke();
    }

    internal List<string> GetTownGameNames()
    {
        var townsDir = Path.Combine(_state.ContentPackPath, "towns");
        if (!Directory.Exists(townsDir)) return [];

        return Directory.GetDirectories(townsDir)
            .Where(d => File.Exists(Path.Combine(d, "design.json")))
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class ValidationItemViewModel
{
    public ValidationSeverity Severity { get; }
    public string Check { get; }
    public string Detail { get; }
    public string Icon => Severity switch
    {
        ValidationSeverity.Pass => "✓",
        ValidationSeverity.Warning => "⚠",
        ValidationSeverity.Error => "✗",
        _ => "?",
    };
    public string SeverityColor => Severity switch
    {
        ValidationSeverity.Pass => "Success",
        ValidationSeverity.Warning => "ActionBlue",
        ValidationSeverity.Error => "Error",
        _ => "OnSurface",
    };

    public ValidationItemViewModel(ValidationItem item)
    {
        Severity = item.Severity;
        Check = item.Check;
        Detail = item.Detail;
    }
}
