using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.Services;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.ViewModels;

public sealed class PipelineWizardViewModel : AppBaseViewModel
{
    private readonly PipelineStateService _stateService;
    private readonly CopilotLlmService _llmService;
    private PipelineState _pipelineState = new();
    private TownGenerationParams _generationParams = TownGenerationParams.Apocalyptic;

    public RegionStepViewModel RegionStepVM { get; }
    public DownloadStepViewModel DownloadStepVM { get; }
    public ParseStepViewModel ParseStepVM { get; }
    public TownSelectionStepViewModel TownSelectionStepVM { get; }
    public TownDesignStepViewModel TownDesignStepVM { get; }
    public TownMapsStepViewModel TownMapsStepVM { get; }
    public AssetsStepViewModel AssetsStepVM { get; }
    public AssemblyStepViewModel AssemblyStepVM { get; }
    public string ContentRoot { get; set; } = string.Empty;

    public ObservableCollection<PipelineStepInfo> Steps { get; } = [];

    private int _currentStep = 1;
    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (SetProperty(ref _currentStep, value))
            {
                RefreshStepStatuses();
                OnPropertyChanged(nameof(CurrentStepView));
            }
        }
    }

    private ContentView? _currentStepView;
    public ContentView? CurrentStepView
    {
        get => _currentStepView;
        private set => SetProperty(ref _currentStepView, value);
    }

    private string _statusMessage = "Select a region to begin.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand NavigateToStepCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    private readonly Dictionary<int, Func<ContentView>> _stepViewFactories = new();

    public PipelineWizardViewModel(
        PipelineStateService stateService,
        RegionStepViewModel regionStepVM,
        DownloadStepViewModel downloadStepVM,
        ParseStepViewModel parseStepVM,
        TownSelectionStepViewModel townSelectionStepVM,
        TownDesignStepViewModel townDesignStepVM,
        TownMapsStepViewModel townMapsStepVM,
        AssetsStepViewModel assetsStepVM,
        AssemblyStepViewModel assemblyStepVM,
        CopilotLlmService llmService)
    {
        _stateService = stateService;
        _llmService = llmService;
        ConfigureLlmService();

        RegionStepVM = regionStepVM;
        DownloadStepVM = downloadStepVM;
        ParseStepVM = parseStepVM;
        TownSelectionStepVM = townSelectionStepVM;
        TownDesignStepVM = townDesignStepVM;
        TownMapsStepVM = townMapsStepVM;
        AssetsStepVM = assetsStepVM;
        AssemblyStepVM = assemblyStepVM;

        RegionStepVM.StepCompleted = OnStepCompleted;
        DownloadStepVM.StepCompleted = OnStepCompleted;
        ParseStepVM.StepCompleted = OnStepCompleted;
        TownSelectionStepVM.StepCompleted = OnStepCompleted;
        TownDesignStepVM.StepCompleted = OnStepCompleted;
        TownMapsStepVM.StepCompleted = OnStepCompleted;
        AssetsStepVM.StepCompleted = OnStepCompleted;
        AssemblyStepVM.StepCompleted = OnStepCompleted;

        // Forward step IsRunning → wizard IsBusy for UITest_IsBusy sentinel
        AssemblyStepVM.PropertyChanged += OnStepPropertyChanged;

        var toolSystemMsg = """
            You are a JSON-only RPG town generator. When asked to generate towns,
            produce a JSON array and submit it via the submit_towns tool.
            Do NOT read files, run commands, or perform web searches.
            Do NOT include explanation text — only the JSON via the tool.
            """;
        TownSelectionStepVM.SetLlmCall(
            _llmService.GetLlmCall(),
            _llmService.GetToolCallDelegate(toolSystemMsg));

        var designToolSystemMsg = """
            You are a JSON-only RPG town designer. When asked to design a town,
            produce the design and submit it via the submit_town_design tool.
            Do NOT read files, run commands, or perform web searches.
            Do NOT include explanation text — only the JSON via the tool.
            """;
        TownDesignStepVM.SetLlmCall(
            _llmService.GetLlmCall(),
            _llmService.GetToolCallDelegate(designToolSystemMsg));

        NavigateToStepCommand = new Command<PipelineStepInfo>(OnNavigateToStep);
        OpenSettingsCommand = new Command(OnOpenSettings);

        InitializeSteps();
    }

    private void ConfigureLlmService()
    {
        _llmService.Model = Preferences.Get("SelectedModel", "gpt-4.1");
        _llmService.CliPath = Preferences.Get("CliPath", string.Empty);
        _llmService.UseBYOK = Preferences.Get("UseBYOK", false);
        _llmService.ProviderType = Preferences.Get("ProviderType", string.Empty);
        _llmService.BaseUrl = Preferences.Get("BaseUrl", string.Empty);
        _ = LoadApiKeyAsync();
    }

    private async Task LoadApiKeyAsync()
    {
        _llmService.ApiKey = await SecureStorage.Default.GetAsync("ApiKey");
    }

    public void RegisterStepViewFactory(int stepNumber, Func<ContentView> factory)
    {
        _stepViewFactories[stepNumber] = factory;
    }

    public async Task LoadStateAsync(string regionName)
    {
        IsBusy = true;
        try
        {
            _pipelineState = await _stateService.LoadAsync(regionName);
            InitializeStepViewModels();
            CurrentStep = _pipelineState.CurrentStep;
            for (var i = 1; i <= CurrentStep; i++)
                LoadStep(i);
            RefreshStepStatuses();
            UpdateCurrentStepView();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task InitializeDefaultAsync()
    {
        IsBusy = true;
        try
        {
            InitializeStepViewModels();
            LoadStep(1);
            RefreshStepStatuses();
            UpdateCurrentStepView();
        }
        finally
        {
            IsBusy = false;
        }
        await Task.CompletedTask;
    }

    private void InitializeStepViewModels()
    {
        _generationParams = TownGenerationParams.LoadFromManifest(
            _pipelineState.ContentPackPath);

        RegionStepVM.Initialize(ContentRoot);
        DownloadStepVM.Initialize(_stateService.DataRoot);
        ParseStepVM.Initialize(_stateService.DataRoot);
        ParseStepVM.TemplateLoaded = t => TownSelectionStepVM.SetParsedTemplate(t);
        TownSelectionStepVM.Initialize(_stateService.DataRoot, _generationParams);
        TownDesignStepVM.Initialize(_stateService.DataRoot);
        TownMapsStepVM.Initialize(_stateService.DataRoot);
    }

    private void LoadStep(int stepNumber)
    {
        switch (stepNumber)
        {
            case 1:
                RegionStepVM.Load(_pipelineState);
                break;
            case 2:
                DownloadStepVM.Load(_pipelineState);
                break;
            case 3:
                ParseStepVM.Load(_pipelineState);
                TownSelectionStepVM.SetParsedTemplate(ParseStepVM.ParsedTemplate);
                break;
            case 4:
                TownSelectionStepVM.Load(_pipelineState);
                break;
            case 5:
                TownDesignStepVM.Load(_pipelineState);
                break;
            case 6:
                TownMapsStepVM.SetRegionTemplate(ParseStepVM.ParsedTemplate);
                TownMapsStepVM.Load(_pipelineState);
                break;
            case 7:
                AssetsStepVM.Load(_pipelineState);
                break;
            case 8:
                AssemblyStepVM.Load(_pipelineState);
                break;
        }
    }

    private void OnStepCompleted()
    {
        AdvanceToNextStep();
        LoadStep(CurrentStep);
        _ = SaveStateAsync();
    }

    public PipelineState GetPipelineState() => _pipelineState;

    public async Task SaveStateAsync()
    {
        _pipelineState.CurrentStep = CurrentStep;
        if (!string.IsNullOrEmpty(_pipelineState.RegionName))
            await _stateService.SaveAsync(_pipelineState);
    }

    public void AdvanceToNextStep()
    {
        if (_pipelineState.TryAdvance())
        {
            CurrentStep = _pipelineState.CurrentStep;
            UpdateCurrentStepView();
        }
    }

    private void InitializeSteps()
    {
        Steps.Add(new PipelineStepInfo(1, "Region"));
        Steps.Add(new PipelineStepInfo(2, "Download Data"));
        Steps.Add(new PipelineStepInfo(3, "Parse & Extract"));
        Steps.Add(new PipelineStepInfo(4, "Town Selection"));
        Steps.Add(new PipelineStepInfo(5, "Town Design"));
        Steps.Add(new PipelineStepInfo(6, "Town Maps"));
        Steps.Add(new PipelineStepInfo(7, "3D Assets"));
        Steps.Add(new PipelineStepInfo(8, "Assemble Pack"));
    }

    private void RefreshStepStatuses()
    {
        foreach (var step in Steps)
        {
            var completed = _pipelineState.IsStepCompleted(step.Number);
            var isCurrent = step.Number == CurrentStep;
            var unlocked = _pipelineState.IsStepUnlocked(step.Number);
            step.UpdateStatus(completed, isCurrent, unlocked);
        }

        // Force UI refresh of the collection
        for (var i = 0; i < Steps.Count; i++)
        {
            var step = Steps[i];
            Steps[i] = step;
        }
    }

    private void UpdateCurrentStepView()
    {
        if (_stepViewFactories.TryGetValue(CurrentStep, out var factory))
            CurrentStepView = factory();
        else
            CurrentStepView = null;
    }

    private void OnNavigateToStep(PipelineStepInfo? step)
    {
        if (step is null) return;
        if (!step.IsUnlocked && !step.IsCompleted && !step.IsCurrent) return;

        CurrentStep = step.Number;
        UpdateCurrentStepView();
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssemblyStepViewModel.IsRunning))
            IsBusy = AssemblyStepVM.IsRunning;
    }

    private async void OnOpenSettings()
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page)
        {
            var settingsPage = new NavigationPage(new Views.SettingsView());
            await page.Navigation.PushModalAsync(settingsPage);
            // Re-read preferences after the modal is dismissed
            settingsPage.Disappearing += (_, _) => ConfigureLlmService();
        }
    }
}
