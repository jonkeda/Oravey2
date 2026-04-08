using System.Windows.Input;
using Oravey2.MapGen.ViewModels.WorldTemplate;
using Oravey2.MapGen.WorldTemplate;

namespace Oravey2.MapGen.ViewModels;

public class AutoCullDialogViewModel : ViewModelBase
{
    private readonly List<TownEntry> _allTowns;
    private readonly List<RoadSegment> _allRoads;
    private readonly List<WaterBody> _allWater;

    // --- Town settings ---
    private TownCategory _townMinCategory;
    public TownCategory TownMinCategory { get => _townMinCategory; set => SetProperty(ref _townMinCategory, value); }

    private int _townMinPopulation;
    public int TownMinPopulation { get => _townMinPopulation; set => SetProperty(ref _townMinPopulation, value); }

    private double _townMinSpacingKm;
    public double TownMinSpacingKm { get => _townMinSpacingKm; set => SetProperty(ref _townMinSpacingKm, value); }

    private int _townMaxCount;
    public int TownMaxCount { get => _townMaxCount; set => SetProperty(ref _townMaxCount, value); }

    private CullPriority _townPriority;
    public CullPriority TownPriority { get => _townPriority; set => SetProperty(ref _townPriority, value); }

    private bool _townAlwaysKeepCities;
    public bool TownAlwaysKeepCities { get => _townAlwaysKeepCities; set => SetProperty(ref _townAlwaysKeepCities, value); }

    private bool _townAlwaysKeepMetropolis;
    public bool TownAlwaysKeepMetropolis { get => _townAlwaysKeepMetropolis; set => SetProperty(ref _townAlwaysKeepMetropolis, value); }

    // --- Road settings ---
    private RoadClass _roadMinClass;
    public RoadClass RoadMinClass { get => _roadMinClass; set => SetProperty(ref _roadMinClass, value); }

    private bool _roadAlwaysKeepMotorways;
    public bool RoadAlwaysKeepMotorways { get => _roadAlwaysKeepMotorways; set => SetProperty(ref _roadAlwaysKeepMotorways, value); }

    private bool _roadKeepNearTowns;
    public bool RoadKeepNearTowns { get => _roadKeepNearTowns; set => SetProperty(ref _roadKeepNearTowns, value); }

    private double _roadTownProximityKm;
    public double RoadTownProximityKm { get => _roadTownProximityKm; set => SetProperty(ref _roadTownProximityKm, value); }

    private bool _roadRemoveDeadEnds;
    public bool RoadRemoveDeadEnds { get => _roadRemoveDeadEnds; set => SetProperty(ref _roadRemoveDeadEnds, value); }

    private double _roadDeadEndMinKm;
    public double RoadDeadEndMinKm { get => _roadDeadEndMinKm; set => SetProperty(ref _roadDeadEndMinKm, value); }

    private bool _roadSimplifyGeometry;
    public bool RoadSimplifyGeometry { get => _roadSimplifyGeometry; set => SetProperty(ref _roadSimplifyGeometry, value); }

    private double _roadSimplifyToleranceM;
    public double RoadSimplifyToleranceM { get => _roadSimplifyToleranceM; set => SetProperty(ref _roadSimplifyToleranceM, value); }

    // --- Water settings ---
    private double _waterMinAreaKm2;
    public double WaterMinAreaKm2 { get => _waterMinAreaKm2; set => SetProperty(ref _waterMinAreaKm2, value); }

    private double _waterMinRiverLengthKm;
    public double WaterMinRiverLengthKm { get => _waterMinRiverLengthKm; set => SetProperty(ref _waterMinRiverLengthKm, value); }

    private bool _waterAlwaysKeepSea;
    public bool WaterAlwaysKeepSea { get => _waterAlwaysKeepSea; set => SetProperty(ref _waterAlwaysKeepSea, value); }

    private bool _waterAlwaysKeepLakes;
    public bool WaterAlwaysKeepLakes { get => _waterAlwaysKeepLakes; set => SetProperty(ref _waterAlwaysKeepLakes, value); }

    // --- Preview result ---
    private string _previewText = string.Empty;
    public string PreviewText { get => _previewText; set => SetProperty(ref _previewText, value); }

    // --- Result ---
    public CullSettings? Result { get; private set; }
    public bool Applied { get; private set; }

    // --- Enum sources for pickers ---
    public TownCategory[] TownCategories { get; } = Enum.GetValues<TownCategory>();
    public RoadClass[] RoadClasses { get; } = Enum.GetValues<RoadClass>();
    public CullPriority[] CullPriorities { get; } = Enum.GetValues<CullPriority>();

    // --- Commands ---
    public ICommand PreviewCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand CancelCommand { get; }

    /// <summary>Raised when the dialog should close.</summary>
    public event Action? CloseRequested;

    public AutoCullDialogViewModel(
        CullSettings current,
        IReadOnlyList<TownItem> towns,
        IReadOnlyList<RoadItem> roads,
        IReadOnlyList<WaterItem> waterBodies)
    {
        _allTowns = towns.Select(t => t.Entry).ToList();
        _allRoads = roads.Select(r => r.Segment).ToList();
        _allWater = waterBodies.Select(w => w.Body).ToList();

        LoadFrom(current);

        PreviewCommand = new RelayCommand(Preview);
        ApplyCommand = new RelayCommand(Apply);
        CancelCommand = new RelayCommand(Cancel);
    }

    public void LoadFrom(CullSettings s)
    {
        TownMinCategory = s.TownMinCategory;
        TownMinPopulation = s.TownMinPopulation;
        TownMinSpacingKm = s.TownMinSpacingKm;
        TownMaxCount = s.TownMaxCount;
        TownPriority = s.TownPriority;
        TownAlwaysKeepCities = s.TownAlwaysKeepCities;
        TownAlwaysKeepMetropolis = s.TownAlwaysKeepMetropolis;

        RoadMinClass = s.RoadMinClass;
        RoadAlwaysKeepMotorways = s.RoadAlwaysKeepMotorways;
        RoadKeepNearTowns = s.RoadKeepNearTowns;
        RoadTownProximityKm = s.RoadTownProximityKm;
        RoadRemoveDeadEnds = s.RoadRemoveDeadEnds;
        RoadDeadEndMinKm = s.RoadDeadEndMinKm;
        RoadSimplifyGeometry = s.RoadSimplifyGeometry;
        RoadSimplifyToleranceM = s.RoadSimplifyToleranceM;

        WaterMinAreaKm2 = s.WaterMinAreaKm2;
        WaterMinRiverLengthKm = s.WaterMinRiverLengthKm;
        WaterAlwaysKeepSea = s.WaterAlwaysKeepSea;
        WaterAlwaysKeepLakes = s.WaterAlwaysKeepLakes;
    }

    public CullSettings BuildSettings() => new()
    {
        TownMinCategory = TownMinCategory,
        TownMinPopulation = TownMinPopulation,
        TownMinSpacingKm = TownMinSpacingKm,
        TownMaxCount = TownMaxCount,
        TownPriority = TownPriority,
        TownAlwaysKeepCities = TownAlwaysKeepCities,
        TownAlwaysKeepMetropolis = TownAlwaysKeepMetropolis,

        RoadMinClass = RoadMinClass,
        RoadAlwaysKeepMotorways = RoadAlwaysKeepMotorways,
        RoadKeepNearTowns = RoadKeepNearTowns,
        RoadTownProximityKm = RoadTownProximityKm,
        RoadRemoveDeadEnds = RoadRemoveDeadEnds,
        RoadDeadEndMinKm = RoadDeadEndMinKm,
        RoadSimplifyGeometry = RoadSimplifyGeometry,
        RoadSimplifyToleranceM = RoadSimplifyToleranceM,

        WaterMinAreaKm2 = WaterMinAreaKm2,
        WaterMinRiverLengthKm = WaterMinRiverLengthKm,
        WaterAlwaysKeepSea = WaterAlwaysKeepSea,
        WaterAlwaysKeepLakes = WaterAlwaysKeepLakes
    };

    public void Preview()
    {
        var settings = BuildSettings();
        var towns = FeatureCuller.CullTowns(_allTowns, settings);
        var noSimplify = settings with { RoadSimplifyGeometry = false };
        var roads = FeatureCuller.CullRoads(_allRoads, towns, noSimplify);
        var water = FeatureCuller.CullWater(_allWater, settings);

        PreviewText = $"Would keep: {towns.Count} towns, {roads.Count} roads, {water.Count} water " +
                      $"(from {_allTowns.Count} / {_allRoads.Count} / {_allWater.Count})";
    }

    private void Apply()
    {
        Result = BuildSettings();
        Applied = true;
        CloseRequested?.Invoke();
    }

    private void Cancel()
    {
        Applied = false;
        CloseRequested?.Invoke();
    }
}
