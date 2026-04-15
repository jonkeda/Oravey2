using System.Numerics;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.Tests.Pipeline;

public class TownDesignStepViewModelTests
{
    private static TownDesignStepViewModel MakeVM() => new();

    private static PipelineState MakeState(string? contentPackPath = null) => new()
    {
        RegionName = "noord-holland",
        ContentPackPath = contentPackPath
            ?? Path.Combine(Path.GetTempPath(), "test-design-" + Guid.NewGuid().ToString("N")),
        CurrentStep = 5,
        Region = new RegionStepState
        {
            Completed = true,
            NorthLat = 52.9,
            SouthLat = 52.2,
            EastLon = 5.2,
            WestLon = 4.5,
        },
        TownSelection = new TownSelectionStepState { Completed = true, TownCount = 3 },
    };

    /// <summary>
    /// Writes a curated-towns.json into the content pack so Load can find it.
    /// </summary>
    private static string SetupCuratedTowns(PipelineState state, int count = 3)
    {
        var towns = new List<CuratedTown>();
        for (int i = 0; i < count; i++)
        {
            towns.Add(new CuratedTown
            {
                GameName = $"Haven-{i}",
                RealName = $"Town{i}",
                Latitude = 52.3 + i * 0.06,
                Longitude = 4.6 + i * 0.05,
                GamePosition = new Vector2(i * 20000f, i * 16700f),
                Description = $"Town {i} description",
                Size = TownCategory.Village,
                Inhabitants = 5000 + i * 1000,
                Destruction = DestructionLevel.Moderate,
            });
        }

        var file = CuratedTownsFile.FromCuratedTowns(towns, "A");
        var path = Path.Combine(state.ContentPackPath, "data", "curated-towns.json");
        file.Save(path);
        return path;
    }

    private static TownDesign MakeDesign(string townName = "Haven-0") => new()
    {
        TownName = townName,
        Landmarks = [new LandmarkBuilding { Name = "Fort Kijkduin", VisualDescription = "A massive coastal fortress", SizeCategory = "large", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" }],
        KeyLocations =
        [
            new KeyLocation { Name = "Market", Purpose = "shop", VisualDescription = "An old drydock market", SizeCategory = "medium", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" },
            new KeyLocation { Name = "Clinic", Purpose = "medical", VisualDescription = "A converted church clinic", SizeCategory = "small", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" },
            new KeyLocation { Name = "Barracks", Purpose = "barracks", VisualDescription = "Reinforced bunker", SizeCategory = "medium", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" },
        ],
        LayoutStyle = "compound",
        Hazards = [new EnvironmentalHazard { Type = "flooding", Description = "The harbour floods at high tide", LocationHint = "south-west waterfront" }],
    };

    // --- Default state ---

    [Fact]
    public void Default_IsNotRunning()
    {
        var vm = MakeVM();
        Assert.False(vm.IsRunning);
        Assert.Empty(vm.Towns);
    }

    [Fact]
    public void Default_NoSelection()
    {
        var vm = MakeVM();
        Assert.Null(vm.SelectedTown);
        Assert.False(vm.HasSelection);
    }

    [Fact]
    public void Default_DesignedCountZero()
    {
        var vm = MakeVM();
        Assert.Equal(0, vm.DesignedCount);
        Assert.Equal("0/0 designed", vm.ProgressText);
    }

    // --- Load ---

    [Fact]
    public void Load_PopulatesTownsFromCuratedFile()
    {
        var vm = MakeVM();
        var state = MakeState();
        SetupCuratedTowns(state, 3);
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        Assert.Equal(3, vm.Towns.Count);
        Assert.Equal("Haven-0", vm.Towns[0].GameName);
        Assert.Equal("Haven-1", vm.Towns[1].GameName);
        Assert.Equal("Haven-2", vm.Towns[2].GameName);
    }

    [Fact]
    public void Load_NoCuratedFile_EmptyTowns()
    {
        var vm = MakeVM();
        var state = MakeState();
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        Assert.Empty(vm.Towns);
    }

    [Fact]
    public void Load_DetectsExistingDesigns()
    {
        var vm = MakeVM();
        var state = MakeState();
        SetupCuratedTowns(state, 2);

        // Pre-save a design for Haven-0
        var designPath = Path.Combine(state.ContentPackPath, "towns", "Haven-0", "design.json");
        MakeDesign("Haven-0").Save(designPath);

        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        Assert.Equal(2, vm.Towns.Count);
        Assert.True(vm.Towns[0].IsDesigned);
        Assert.False(vm.Towns[1].IsDesigned);
        Assert.Equal(1, vm.DesignedCount);
    }

    // --- TownDesignItem ---

    [Fact]
    public void TownDesignItem_DefaultNotDesigned()
    {
        var item = new TownDesignItem { GameName = "Test" };
        Assert.False(item.IsDesigned);
        Assert.Equal("—", item.StatusIcon);
    }

    [Fact]
    public void TownDesignItem_DesignedShowsCheckmark()
    {
        var item = new TownDesignItem { GameName = "Test", IsDesigned = true };
        Assert.Equal("✅", item.StatusIcon);
    }

    [Fact]
    public void TownDesignItem_DesignSetsComputedProperties()
    {
        var item = new TownDesignItem { GameName = "Test" };
        item.Design = MakeDesign("Test");

        Assert.Equal("Fort Kijkduin", item.LandmarkSummary);
        Assert.Equal(3, item.KeyLocationCount);
        Assert.Equal("compound", item.LayoutStyle);
        Assert.Equal(1, item.HazardCount);
    }

    [Fact]
    public void TownDesignItem_ToCuratedTown_MapsCorrectly()
    {
        var item = new TownDesignItem
        {
            GameName = "Haven-0",
            RealName = "Town0",
            Description = "Desc",
            Size = "Village",
            Inhabitants = 5000,
            Destruction = "Moderate",
            Latitude = 52.3,
            Longitude = 4.6,
        };
        var town = item.ToCuratedTown();

        Assert.Equal("Haven-0", town.GameName);
        Assert.Equal("Town0", town.RealName);
        Assert.Equal(TownCategory.Village, town.Size);
        Assert.Equal(DestructionLevel.Moderate, town.Destruction);
    }

    // --- Selection ---

    [Fact]
    public void SelectTown_SetsHasSelection()
    {
        var vm = MakeVM();
        var state = MakeState();
        SetupCuratedTowns(state, 2);
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        vm.SelectedTown = vm.Towns[0];
        Assert.True(vm.HasSelection);
    }

    // --- Design path ---

    [Fact]
    public void GetDesignPath_ReturnsCorrectPath()
    {
        var vm = MakeVM();
        var state = MakeState();
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        var path = vm.GetDesignPath("Haven-0");
        var expected = Path.Combine(state.ContentPackPath, "towns", "Haven-0", "design.json");
        Assert.Equal(expected, path);
    }

    // --- SaveDesign ---

    [Fact]
    public void SaveDesign_WritesFile()
    {
        var vm = MakeVM();
        var state = MakeState();
        SetupCuratedTowns(state, 1);
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        var item = vm.Towns[0];
        item.Design = MakeDesign("Haven-0");
        item.HasPendingDesign = true;

        vm.SaveDesign(item);

        Assert.True(item.IsDesigned);
        Assert.False(item.HasPendingDesign);
        Assert.True(File.Exists(vm.GetDesignPath("Haven-0")));
        Assert.Equal(1, vm.DesignedCount);
    }

    [Fact]
    public void SaveDesign_RoundTrips()
    {
        var vm = MakeVM();
        var state = MakeState();
        SetupCuratedTowns(state, 1);
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        var item = vm.Towns[0];
        item.Design = MakeDesign("Haven-0");
        vm.SaveDesign(item);

        // Reload
        var loaded = TownDesign.Load(vm.GetDesignPath("Haven-0"));

        Assert.Equal("Haven-0", loaded.TownName);
        Assert.Equal("Fort Kijkduin", loaded.Landmarks[0].Name);
        Assert.Equal("large", loaded.Landmarks[0].SizeCategory);
        Assert.Equal(3, loaded.KeyLocations.Count);
        Assert.Equal("compound", loaded.LayoutStyle);
        Assert.Single(loaded.Hazards);
    }

    // --- RefreshDesignedCount ---

    [Fact]
    public void RefreshDesignedCount_TracksCorrectly()
    {
        var vm = MakeVM();
        var state = MakeState();
        SetupCuratedTowns(state, 3);
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        Assert.Equal(0, vm.DesignedCount);
        Assert.False(vm.AllDesigned);

        vm.Towns[0].IsDesigned = true;
        vm.RefreshDesignedCount();
        Assert.Equal(1, vm.DesignedCount);

        vm.Towns[1].IsDesigned = true;
        vm.Towns[2].IsDesigned = true;
        vm.RefreshDesignedCount();
        Assert.Equal(3, vm.DesignedCount);
        Assert.True(vm.AllDesigned);
    }

    // --- Pipeline state ---

    [Fact]
    public void GetState_ReturnsPipelineState()
    {
        var vm = MakeVM();
        var state = MakeState();
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        Assert.Same(state, vm.GetState());
    }

    // --- StatusText ---

    [Fact]
    public void Default_StatusText()
    {
        var vm = MakeVM();
        Assert.Equal("Select a town and click Design.", vm.StatusText);
    }

    // --- Next command ---

    [Fact]
    public void OnNext_SetsCompleted_InvokesStepCompleted()
    {
        var vm = MakeVM();
        var state = MakeState();
        SetupCuratedTowns(state, 1);
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        // Mark the only town as designed
        vm.Towns[0].IsDesigned = true;
        vm.Towns[0].Design = MakeDesign();
        vm.SaveDesign(vm.Towns[0]);

        bool invoked = false;
        vm.StepCompleted = () => invoked = true;

        // AllDesigned is true, can call Next
        Assert.True(vm.AllDesigned);
        if (vm.NextCommand.CanExecute(null))
            vm.NextCommand.Execute(null);

        Assert.True(invoked);
        Assert.True(state.TownDesign.Completed);
    }

    // --- Next command enable/disable ---

    [Fact]
    public void NextCommand_EnabledWhenAtLeastOneDesigned()
    {
        var vm = MakeVM();
        var state = MakeState();
        SetupCuratedTowns(state, 3);
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        Assert.False(vm.NextCommand.CanExecute(null)); // 0 designed

        vm.Towns[0].IsDesigned = true;
        vm.RefreshDesignedCount();
        Assert.Equal(1, vm.DesignedCount);
        Assert.True(vm.NextCommand.CanExecute(null)); // 1 of 3 designed — partial is OK
    }

    [Fact]
    public void NextCommand_DisabledWhenNoneDesigned()
    {
        var vm = MakeVM();
        var state = MakeState();
        SetupCuratedTowns(state, 3);
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        Assert.Equal(0, vm.DesignedCount);
        Assert.False(vm.NextCommand.CanExecute(null));
    }

    // --- IncompleteWarning ---

    [Fact]
    public void IncompleteWarning_ShownWhenPartial()
    {
        var vm = MakeVM();
        var state = MakeState();
        SetupCuratedTowns(state, 3);
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        vm.Towns[0].IsDesigned = true;
        vm.RefreshDesignedCount();

        Assert.NotNull(vm.IncompleteWarning);
        Assert.True(vm.HasIncompleteWarning);
        Assert.Contains("2 town(s)", vm.IncompleteWarning);
    }

    [Fact]
    public void IncompleteWarning_NullWhenAllDesigned()
    {
        var vm = MakeVM();
        var state = MakeState();
        SetupCuratedTowns(state, 2);
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        vm.Towns[0].IsDesigned = true;
        vm.Towns[1].IsDesigned = true;
        vm.RefreshDesignedCount();

        Assert.Null(vm.IncompleteWarning);
        Assert.False(vm.HasIncompleteWarning);
    }

    [Fact]
    public void IncompleteWarning_NullWhenNoneDesigned()
    {
        var vm = MakeVM();
        var state = MakeState();
        SetupCuratedTowns(state, 2);
        vm.Initialize(state.ContentPackPath);
        vm.Load(state);

        Assert.Null(vm.IncompleteWarning); // 0 designed → no warning (warning is only for partial)
        Assert.False(vm.HasIncompleteWarning);
    }

    // --- LandmarkSummary multi-landmark ---

    [Fact]
    public void TownDesignItem_MultiLandmark_SummaryJoinsNames()
    {
        var item = new TownDesignItem { GameName = "Test" };
        item.Design = new TownDesign
        {
            TownName = "Test",
            Landmarks =
            [
                new LandmarkBuilding { Name = "Fort Kijkduin", VisualDescription = "A fortress", SizeCategory = "large", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" },
                new LandmarkBuilding { Name = "The Lighthouse", VisualDescription = "A lighthouse", SizeCategory = "medium", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" },
            ],
            KeyLocations = [],
            LayoutStyle = "organic",
            Hazards = [],
        };

        Assert.Equal("Fort Kijkduin, The Lighthouse", item.LandmarkSummary);
        Assert.Equal(2, item.LandmarkCount);
    }
}
