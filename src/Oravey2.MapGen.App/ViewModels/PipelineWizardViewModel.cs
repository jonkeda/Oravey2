using System.Collections.ObjectModel;
using System.Windows.Input;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.ViewModels;

public sealed class PipelineWizardViewModel : BaseViewModel
{
    private readonly PipelineStateService _stateService;
    private PipelineState _pipelineState = new();

    public RegionStepViewModel RegionStepVM { get; }
    public DownloadStepViewModel DownloadStepVM { get; }
    public ParseStepViewModel ParseStepVM { get; }
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
        ParseStepViewModel parseStepVM)
    {
        _stateService = stateService;
        RegionStepVM = regionStepVM;
        DownloadStepVM = downloadStepVM;
        ParseStepVM = parseStepVM;

        RegionStepVM.StepCompleted = OnStepCompleted;
        DownloadStepVM.StepCompleted = OnStepCompleted;
        ParseStepVM.StepCompleted = OnStepCompleted;

        NavigateToStepCommand = new Command<PipelineStepInfo>(OnNavigateToStep);
        OpenSettingsCommand = new Command(OnOpenSettings);

        InitializeSteps();
    }

    public void RegisterStepViewFactory(int stepNumber, Func<ContentView> factory)
    {
        _stepViewFactories[stepNumber] = factory;
    }

    public async Task LoadStateAsync(string regionName)
    {
        _pipelineState = await _stateService.LoadAsync(regionName);
        CurrentStep = _pipelineState.CurrentStep;
        InitializeStepViewModels();
        RefreshStepStatuses();
        UpdateCurrentStepView();
    }

    public async Task InitializeDefaultAsync()
    {
        InitializeStepViewModels();
        RefreshStepStatuses();
        UpdateCurrentStepView();
        await Task.CompletedTask;
    }

    private void InitializeStepViewModels()
    {
        RegionStepVM.Initialize(_pipelineState, ContentRoot);
        DownloadStepVM.Initialize(_pipelineState, _stateService.DataRoot);
        ParseStepVM.Initialize(_pipelineState, _stateService.DataRoot);
    }

    private void OnStepCompleted()
    {
        AdvanceToNextStep();
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

    private async void OnOpenSettings()
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page)
        {
            await page.Navigation.PushModalAsync(
                new NavigationPage(new Views.SettingsView()));
        }
    }
}
