using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.Tests.Pipeline;

public class ParseStepViewModelTests
{
    private static ParseStepViewModel MakeVM() => new();

    private static PipelineState MakeState() => new()
    {
        RegionName = "noord-holland",
        Region = new RegionStepState
        {
            PresetName = "noord-holland",
            NorthLat = 52.9,
            SouthLat = 52.2,
            EastLon = 5.2,
            WestLon = 4.5,
        }
    };

    // --- Default state ---

    [Fact]
    public void ParseStepViewModel_Default_IsNotParsed()
    {
        var vm = MakeVM();
        Assert.False(vm.IsParsed);
    }

    [Fact]
    public void ParseStepViewModel_Default_IsNotParsing()
    {
        var vm = MakeVM();
        Assert.False(vm.IsParsing);
    }

    [Fact]
    public void ParseStepViewModel_Default_StatusTextIsReady()
    {
        var vm = MakeVM();
        Assert.Equal("Ready to parse.", vm.StatusText);
    }

    [Fact]
    public void ParseStepViewModel_Default_LazyPanelsCollapsed()
    {
        var vm = MakeVM();
        Assert.False(vm.ShowTownList);
        Assert.False(vm.ShowSummaryTables);
        Assert.False(vm.ShowMapPreview);
    }

    [Fact]
    public void ParseStepViewModel_Default_CountsAreZero()
    {
        var vm = MakeVM();
        Assert.Equal(0, vm.RawTownCount);
        Assert.Equal(0, vm.RawRoadCount);
        Assert.Equal(0, vm.RawWaterCount);
        Assert.Equal(0, vm.SrtmTileCount);
        Assert.Equal(0, vm.FilteredTownCount);
        Assert.Equal(0, vm.FilteredRoadCount);
        Assert.Equal(0, vm.FilteredWaterCount);
    }

    // --- Command can-execute ---

    [Fact]
    public void ParseCommand_Default_CanExecute()
    {
        var vm = MakeVM();
        Assert.True(vm.ParseCommand.CanExecute(null));
    }

    [Fact]
    public void NextCommand_Default_CannotExecute()
    {
        var vm = MakeVM();
        Assert.False(vm.NextCommand.CanExecute(null));
    }

    // --- Initialize with completed state ---

    [Fact]
    public void Initialize_WithCompleted_RestoresCounts()
    {
        var vm = MakeVM();
        var state = MakeState();
        state.Parse.Completed = true;
        state.Parse.TownCount = 42;
        state.Parse.RoadCount = 100;
        state.Parse.WaterBodyCount = 15;
        state.Parse.SrtmTileCount = 2;
        state.Parse.FilteredTownCount = 30;
        state.Parse.FilteredRoadCount = 80;
        state.Parse.FilteredWaterBodyCount = 10;

        vm.Initialize("/tmp");
        vm.Load(state);

        Assert.Equal(42, vm.RawTownCount);
        Assert.Equal(100, vm.RawRoadCount);
        Assert.Equal(15, vm.RawWaterCount);
        Assert.Equal(2, vm.SrtmTileCount);
        Assert.Equal(30, vm.FilteredTownCount);
        Assert.Equal(80, vm.FilteredRoadCount);
        Assert.Equal(10, vm.FilteredWaterCount);
    }

    [Fact]
    public void Initialize_WithCompleted_IsParsedRemainsFalse()
    {
        var vm = MakeVM();
        var state = MakeState();
        state.Parse.Completed = true;
        state.Parse.TownCount = 5;

        vm.Initialize("/tmp");
        vm.Load(state);

        // Template not persisted, so IsParsed stays false until re-parse
        Assert.False(vm.IsParsed);
    }

    [Fact]
    public void Initialize_WithCompleted_ShowsReparsPrompt()
    {
        var vm = MakeVM();
        var state = MakeState();
        state.Parse.Completed = true;

        vm.Initialize("/tmp");
        vm.Load(state);

        Assert.Contains("Parse again", vm.StatusText);
    }

    // --- GetState ---

    [Fact]
    public void GetState_ReturnsInitializedState()
    {
        var vm = MakeVM();
        var state = MakeState();
        vm.Initialize("/tmp");
        vm.Load(state);

        Assert.Same(state, vm.GetState());
    }

    // --- PopulateTownList ---

    [Fact]
    public void PopulateTownList_PopulatesTownsCollection()
    {
        var vm = MakeVM();
        var towns = new List<TownEntry>
        {
            new("Amsterdam", 52.37, 4.90, 900000, Vector2.Zero, TownCategory.City),
            new("Haarlem", 52.38, 4.64, 160000, Vector2.Zero, TownCategory.Town),
            new("Volendam", 52.49, 5.07, 22000, Vector2.Zero, TownCategory.Village),
        };

        vm.PopulateTownList(towns);

        Assert.Equal(3, vm.Towns.Count);
    }

    [Fact]
    public void PopulateTownList_SortsDescendingByPopulation()
    {
        var vm = MakeVM();
        var towns = new List<TownEntry>
        {
            new("Small", 52.0, 4.0, 100, Vector2.Zero, TownCategory.Hamlet),
            new("Big", 52.0, 4.0, 50000, Vector2.Zero, TownCategory.Town),
            new("Medium", 52.0, 4.0, 5000, Vector2.Zero, TownCategory.Village),
        };

        vm.PopulateTownList(towns);

        Assert.Equal("Big", vm.Towns[0].Name);
        Assert.Equal("Medium", vm.Towns[1].Name);
        Assert.Equal("Small", vm.Towns[2].Name);
    }

    [Fact]
    public void PopulateTownList_MapsFieldsCorrectly()
    {
        var vm = MakeVM();
        var towns = new List<TownEntry>
        {
            new("TestTown", 52.5, 4.8, 1234, Vector2.Zero, TownCategory.Village),
        };

        vm.PopulateTownList(towns);

        var item = vm.Towns[0];
        Assert.Equal("TestTown", item.Name);
        Assert.Equal(1234, item.Population);
        Assert.Equal(TownCategory.Village, item.Category);
        Assert.Equal(52.5, item.Lat);
        Assert.Equal(4.8, item.Lon);
    }

    [Fact]
    public void PopulateTownList_CalledTwice_ReplacesItems()
    {
        var vm = MakeVM();
        vm.PopulateTownList(
        [
            new("A", 52.0, 4.0, 100, Vector2.Zero, TownCategory.Hamlet),
            new("B", 52.0, 4.0, 200, Vector2.Zero, TownCategory.Village),
        ]);

        vm.PopulateTownList(
        [
            new("C", 52.0, 4.0, 300, Vector2.Zero, TownCategory.Town),
        ]);

        Assert.Single(vm.Towns);
        Assert.Equal("C", vm.Towns[0].Name);
    }

    // --- PopulateSummaryTables ---

    [Fact]
    public void PopulateSummaryTables_GroupsRoadsByClass()
    {
        var vm = MakeVM();
        var roads = new List<RoadSegment>
        {
            new(LinearFeatureType.Primary, [Vector2.Zero, Vector2.One]),
            new(LinearFeatureType.Primary, [Vector2.Zero, Vector2.One]),
            new(LinearFeatureType.Tertiary, [Vector2.Zero, Vector2.One]),
        };

        vm.PopulateSummaryTables(roads, []);

        Assert.Equal(2, vm.RoadsByClass.Count);
        var primary = vm.RoadsByClass.First(r => r.Category == "Primary");
        Assert.Equal(2, primary.Count);
        var tertiary = vm.RoadsByClass.First(r => r.Category == "Tertiary");
        Assert.Equal(1, tertiary.Count);
    }

    [Fact]
    public void PopulateSummaryTables_GroupsWaterByType()
    {
        var vm = MakeVM();
        var water = new List<WaterBody>
        {
            new(WaterType.Lake, [Vector2.Zero]),
            new(WaterType.River, [Vector2.Zero]),
            new(WaterType.Lake, [Vector2.Zero]),
        };

        vm.PopulateSummaryTables([], water);

        Assert.Equal(2, vm.WaterByType.Count);
        var lakes = vm.WaterByType.First(w => w.Category == "Lake");
        Assert.Equal(2, lakes.Count);
    }

    [Fact]
    public void PopulateSummaryTables_CalledTwice_ReplacesItems()
    {
        var vm = MakeVM();
        vm.PopulateSummaryTables(
            [new(LinearFeatureType.Primary, [Vector2.Zero, Vector2.One])],
            [new(WaterType.Lake, [Vector2.Zero])]);

        vm.PopulateSummaryTables([], []);

        Assert.Empty(vm.RoadsByClass);
        Assert.Empty(vm.WaterByType);
    }

    // --- Toggle commands ---

    [Fact]
    public void ToggleTownListCommand_TogglesBoolProperty()
    {
        var vm = MakeVM();
        Assert.False(vm.ShowTownList);

        vm.ToggleTownListCommand.Execute(null);
        Assert.True(vm.ShowTownList);

        vm.ToggleTownListCommand.Execute(null);
        Assert.False(vm.ShowTownList);
    }

    [Fact]
    public void ToggleSummaryCommand_TogglesBoolProperty()
    {
        var vm = MakeVM();
        Assert.False(vm.ShowSummaryTables);

        vm.ToggleSummaryCommand.Execute(null);
        Assert.True(vm.ShowSummaryTables);

        vm.ToggleSummaryCommand.Execute(null);
        Assert.False(vm.ShowSummaryTables);
    }

    [Fact]
    public void ToggleMapCommand_TogglesBoolProperty()
    {
        var vm = MakeVM();
        Assert.False(vm.ShowMapPreview);

        vm.ToggleMapCommand.Execute(null);
        Assert.True(vm.ShowMapPreview);

        vm.ToggleMapCommand.Execute(null);
        Assert.False(vm.ShowMapPreview);
    }

    // --- Summary text ---

    [Fact]
    public void RawSummary_WhenNotParsed_ReturnsEmpty()
    {
        var vm = MakeVM();
        Assert.Equal(string.Empty, vm.RawSummary);
    }

    [Fact]
    public void FilteredSummary_WhenNotParsed_ReturnsEmpty()
    {
        var vm = MakeVM();
        Assert.Equal(string.Empty, vm.FilteredSummary);
    }

    // --- PropertyChanged notifications ---

    [Fact]
    public void ToggleTownList_RaisesPropertyChanged()
    {
        var vm = MakeVM();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.ShowTownList = true;

        Assert.Contains(nameof(vm.ShowTownList), raised);
    }

    [Fact]
    public void ToggleMapPreview_RaisesPropertyChanged()
    {
        var vm = MakeVM();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.ShowMapPreview = true;

        Assert.Contains(nameof(vm.ShowMapPreview), raised);
    }

    // --- Cache reload (FIX-012) ---

    private static Oravey2.MapGen.RegionTemplates.RegionTemplate MakeTemplate() => new()
    {
        Name = "noord-holland",
        ElevationGrid = new float[1, 1],
        GridOriginLat = 52.5,
        GridOriginLon = 4.8,
        GridCellSizeMetres = 30.0,
        Towns = [new TownEntry("Amsterdam", 52.37, 4.90, 900_000, new Vector2(100, 200), TownCategory.City)],
        Roads = [new RoadSegment(LinearFeatureType.Primary, [Vector2.Zero, Vector2.One])],
        WaterBodies = [new WaterBody(WaterType.Lake, [Vector2.Zero])],
    };

    [Fact]
    public async Task Initialize_LoadsCacheWhenFileExists_EvenIfTemplateSavedIsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var regionDir = Path.Combine(tempDir, "regions", "noord-holland");
            Directory.CreateDirectory(regionDir);
            var binPath = Path.Combine(regionDir, "region-template.bin");
            await RegionTemplateSerializer.SaveAsync(MakeTemplate(), binPath);

            var vm = MakeVM();
            var state = MakeState();
            state.Parse.TemplateSaved = false; // key condition

            vm.Initialize(tempDir);
            vm.Load(state);

            // IsParsed set synchronously before async load
            Assert.True(vm.IsParsed);

            // Wait for async load to finish
            await Task.Delay(500);

            // Template should be loaded and TemplateSaved fixed
            Assert.NotNull(vm.ParsedTemplate);
            Assert.True(state.Parse.TemplateSaved);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Initialize_CacheLoad_SetsTemplateSavedTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var regionDir = Path.Combine(tempDir, "regions", "noord-holland");
            Directory.CreateDirectory(regionDir);
            var binPath = Path.Combine(regionDir, "region-template.bin");
            await RegionTemplateSerializer.SaveAsync(MakeTemplate(), binPath);

            var vm = MakeVM();
            var state = MakeState();
            state.Parse.TemplateSaved = false;

            vm.Initialize(tempDir);
            vm.Load(state);

            // Wait briefly for the fire-and-forget to complete
            await Task.Delay(500);

            Assert.True(state.Parse.TemplateSaved);
            Assert.NotNull(vm.ParsedTemplate);
            Assert.Equal("Loaded from cache.", vm.StatusText);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Initialize_CacheLoad_InvokesTemplateLoadedCallback()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var regionDir = Path.Combine(tempDir, "regions", "noord-holland");
            Directory.CreateDirectory(regionDir);
            var binPath = Path.Combine(regionDir, "region-template.bin");
            await RegionTemplateSerializer.SaveAsync(MakeTemplate(), binPath);

            var vm = MakeVM();
            var state = MakeState();
            Oravey2.MapGen.RegionTemplates.RegionTemplate? received = null;
            vm.TemplateLoaded = t => received = t;

            vm.Initialize(tempDir);
            vm.Load(state);
            await Task.Delay(500);

            Assert.NotNull(received);
            Assert.Equal("noord-holland", received.Name);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ParseAsync_BlockedWhileCacheLoading()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var regionDir = Path.Combine(tempDir, "regions", "noord-holland");
            Directory.CreateDirectory(regionDir);
            var binPath = Path.Combine(regionDir, "region-template.bin");
            await RegionTemplateSerializer.SaveAsync(MakeTemplate(), binPath);

            var vm = MakeVM();
            var state = MakeState();
            vm.Initialize(tempDir);
            vm.Load(state);

            // ParseAsync should return early without throwing
            await vm.ParseAsync();

            // IsParsing should remain false (parse never actually started)
            Assert.False(vm.IsParsing);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
