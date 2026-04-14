using System.Numerics;
using Oravey2.Contracts.ContentPack;
using Oravey2.Contracts.Spatial;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.Tests.Pipeline;

public class TownMapsStepViewModelTests
{
    private static TownSpatialSpecification CreateTestSpatialSpec() => new TownSpatialSpecification
    {
        RealWorldBounds = new BoundingBox(52.50, 52.51, 4.96, 4.97),
        BuildingPlacements = new Dictionary<string, BuildingPlacement>
        {
            ["Fort Test"] = new BuildingPlacement { Name = "Fort Test", CenterLat = 52.505, CenterLon = 4.965, WidthMeters = 20, DepthMeters = 30, RotationDegrees = 0, AlignmentHint = "centre" },
            ["Shop"] = new BuildingPlacement { Name = "Shop", CenterLat = 52.504, CenterLon = 4.963, WidthMeters = 8, DepthMeters = 6, RotationDegrees = 0, AlignmentHint = "on_main_road" },
            ["Inn"] = new BuildingPlacement { Name = "Inn", CenterLat = 52.506, CenterLon = 4.964, WidthMeters = 10, DepthMeters = 8, RotationDegrees = 0, AlignmentHint = "on_main_road" },
        },
        RoadNetwork = new RoadNetwork
        {
            Nodes = [new Vector2(52.50f, 4.96f), new Vector2(52.51f, 4.97f)],
            Edges = [new RoadEdge(52.50, 4.96, 52.51, 4.97)],
            RoadWidthMeters = 6.0f
        },
        TerrainDescription = "flat"
    };

    private static TownDesign CreateTestDesign(string townName = "TestTown") => new(
        townName,
        [new LandmarkBuilding("Fort Test", "A ruined fortress", "large", "", "", "")],
        [new KeyLocation("Shop", "shop", "A small shop", "small", "", "", ""),
         new KeyLocation("Inn", "rest", "An inn", "medium", "", "", "")],
        "organic",
        [new EnvironmentalHazard("flooding", "Water rises", "south")],
        CreateTestSpatialSpec());

    private static PipelineState CreateTestState(string tempDir)
    {
        return new PipelineState
        {
            RegionName = "test-region",
            ContentPackPath = tempDir,
            CurrentStep = 6,
        };
    }

    private static string SetupTownFiles(string tempDir, string townName)
    {
        // Create curated-towns.json
        var dataDir = Path.Combine(tempDir, "data");
        Directory.CreateDirectory(dataDir);
        var curatedPath = Path.Combine(dataDir, "curated-towns.json");
        if (!File.Exists(curatedPath))
        {
            var curated = new CuratedTownsFile
            {
                Mode = "A",
                GeneratedAt = DateTime.UtcNow,
                Towns = [new CuratedTownDto
                {
                    GameName = townName, RealName = "RealTest", Latitude = 52.5, Longitude = 4.8,
                    Description = "A test town", Size = "Town", Inhabitants = 5000, Destruction = "Moderate",
                }],
            };
            curated.Save(curatedPath);
        }

        // Create design.json
        var design = CreateTestDesign(townName);
        var designFile = TownDesignFile.FromTownDesign(design);
        var townDir = Path.Combine(tempDir, "towns", townName);
        Directory.CreateDirectory(townDir);
        designFile.Save(Path.Combine(townDir, "design.json"));

        return tempDir;
    }

    [Fact]
    public void Defaults_StatusText_IsPrompt()
    {
        var vm = new TownMapsStepViewModel();
        Assert.Equal("Select a town and click Generate Map.", vm.StatusText);
    }

    [Fact]
    public void Defaults_NothingSelected()
    {
        var vm = new TownMapsStepViewModel();
        Assert.Null(vm.SelectedTown);
        Assert.False(vm.HasSelection);
        Assert.False(vm.IsRunning);
        Assert.Equal(0, vm.GeneratedCount);
    }

    [Fact]
    public void Load_PopulatesTownsFromDesigns()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vm_test_{Guid.NewGuid():N}");
        try
        {
            SetupTownFiles(dir, "Havenburg");
            var vm = new TownMapsStepViewModel();
            vm.Initialize(dir);
            vm.Load(CreateTestState(dir));

            Assert.Single(vm.Towns);
            Assert.Equal("Havenburg", vm.Towns[0].GameName);
            Assert.NotNull(vm.Towns[0].Design);
            Assert.False(vm.Towns[0].IsGenerated);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_DetectsExistingMapFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vm_test_{Guid.NewGuid():N}");
        try
        {
            SetupTownFiles(dir, "Havenburg");

            // Pre-generate map files
            var design = CreateTestDesign("Havenburg");
            var condenser = new TownMapCondenser();
            var region = new Oravey2.MapGen.RegionTemplates.RegionTemplate
            {
                Name = "test", ElevationGrid = new float[1, 1],
                GridOriginLat = 0, GridOriginLon = 0, GridCellSizeMetres = 100,
            };
            var result = condenser.Condense(
                new CuratedTown("Havenburg", "RealTest", 52.5, 4.8,
                    System.Numerics.Vector2.Zero, "",
                    TownCategory.Town, 5000, DestructionLevel.Moderate),
                design, region, 42);
            TownMapFiles.Save(result, Path.Combine(dir, "towns", "Havenburg"));

            var vm = new TownMapsStepViewModel();
            vm.Initialize(dir);
            vm.Load(CreateTestState(dir));

            Assert.True(vm.Towns[0].IsGenerated);
            Assert.NotNull(vm.Towns[0].MapResult);
            Assert.Equal(1, vm.GeneratedCount);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GenerateMap_ProducesResult_AndSaves()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vm_test_{Guid.NewGuid():N}");
        try
        {
            SetupTownFiles(dir, "Havenburg");
            var vm = new TownMapsStepViewModel();
            vm.Initialize(dir);
            vm.Load(CreateTestState(dir));

            vm.SelectedTown = vm.Towns[0];
            vm.GenerateMap(vm.Towns[0]);

            Assert.True(vm.Towns[0].IsGenerated);
            Assert.NotNull(vm.Towns[0].MapResult);
            Assert.Equal(1, vm.GeneratedCount);
            Assert.True(File.Exists(Path.Combine(dir, "towns", "Havenburg", "layout.json")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GenerateAllMaps_ProcessesAllTowns()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vm_test_{Guid.NewGuid():N}");
        try
        {
            // Setup two towns
            var dataDir = Path.Combine(dir, "data");
            Directory.CreateDirectory(dataDir);
            var curated = new CuratedTownsFile
            {
                Mode = "A", GeneratedAt = DateTime.UtcNow,
                Towns = [
                    new CuratedTownDto { GameName = "TownA", RealName = "A", Latitude = 0, Longitude = 0, Description = "a", Size = "Town", Inhabitants = 5000, Destruction = "Moderate" },
                    new CuratedTownDto { GameName = "TownB", RealName = "B", Latitude = 0, Longitude = 0, Description = "b", Size = "Town", Inhabitants = 3000, Destruction = "Heavy" },
                ],
            };
            curated.Save(Path.Combine(dataDir, "curated-towns.json"));

            foreach (var name in new[] { "TownA", "TownB" })
            {
                var design = CreateTestDesign(name);
                var townDir = Path.Combine(dir, "towns", name);
                Directory.CreateDirectory(townDir);
                TownDesignFile.FromTownDesign(design).Save(Path.Combine(townDir, "design.json"));
            }

            var vm = new TownMapsStepViewModel();
            vm.Initialize(dir);
            vm.Load(CreateTestState(dir));

            Assert.Equal(2, vm.Towns.Count);
            vm.GenerateAllMaps();

            Assert.True(vm.Towns[0].IsGenerated);
            Assert.True(vm.Towns[1].IsGenerated);
            Assert.Equal(2, vm.GeneratedCount);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TownMapItem_StatusIcon_Ungenerated()
    {
        var item = new TownMapItem { GameName = "Test" };
        Assert.Equal("—", item.StatusIcon);
    }

    [Fact]
    public void TownMapItem_StatusIcon_Generated()
    {
        var item = new TownMapItem { GameName = "Test", IsGenerated = true };
        Assert.Equal("✅", item.StatusIcon);
    }

    [Fact]
    public void TownMapItem_StatsText_WithResult()
    {
        var item = new TownMapItem
        {
            GameName = "Test",
            MapResult = new TownMapResult(
                new TownLayout(16, 16, []),
                [new PlacedBuilding("b_0", "Fort", "m.glb", "large", [], 2, 0.5f, new(0, 0, 0, 0))],
                [new PlacedProp("p_0", "m.glb", new(0, 0, 1, 1), 0, 1, false)],
                [new TownZone("z_0", "Main", 0, 0, 1, true, 0, 0, 0, 0)]),
        };

        Assert.Equal("16×16", item.GridSize);
        Assert.Equal(1, item.BuildingCount);
        Assert.Equal(1, item.PropCount);
        Assert.Equal(1, item.ZoneCount);
        Assert.Contains("16×16", item.StatsText);
    }

    [Fact]
    public void TownMapItem_StatsText_NoResult()
    {
        var item = new TownMapItem { GameName = "Test" };
        Assert.Equal("—", item.GridSize);
        Assert.Equal("", item.StatsText);
    }

    [Fact]
    public void ProgressText_ReflectsCounts()
    {
        var vm = new TownMapsStepViewModel();
        Assert.Equal("0/0 generated", vm.ProgressText);
    }

    [Fact]
    public void GetState_ReturnsPipelineState()
    {
        var vm = new TownMapsStepViewModel();
        var state = new PipelineState { RegionName = "test" };
        vm.Load(state);
        Assert.Same(state, vm.GetState());
    }

    [Fact]
    public void SaveMap_WritesFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vm_test_{Guid.NewGuid():N}");
        try
        {
            SetupTownFiles(dir, "TestTown");
            var vm = new TownMapsStepViewModel();
            vm.Initialize(dir);
            vm.Load(CreateTestState(dir));

            var item = vm.Towns[0];
            item.MapResult = new TownMapResult(
                new TownLayout(16, 16, [[0, 0]]),
                [], [], []);
            vm.SaveMap(item);

            var townDir = Path.Combine(dir, "towns", "TestTown");
            Assert.True(File.Exists(Path.Combine(townDir, "layout.json")));
            Assert.True(item.IsGenerated);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void RegenerateSelected_ResetsAndRegens()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vm_test_{Guid.NewGuid():N}");
        try
        {
            SetupTownFiles(dir, "Havenburg");
            var vm = new TownMapsStepViewModel();
            vm.Initialize(dir);
            vm.Load(CreateTestState(dir));

            vm.SelectedTown = vm.Towns[0];
            vm.GenerateMap(vm.Towns[0]);
            Assert.True(vm.Towns[0].IsGenerated);

            vm.RegenerateSelected();
            // Should still be generated after re-generation (auto-accept)
            Assert.True(vm.Towns[0].IsGenerated);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void BuildParams_DefaultValues()
    {
        var vm = new TownMapsStepViewModel();
        var p = vm.BuildParams();

        Assert.Equal(GridSizeMode.Auto, p.GridSize);
        Assert.Equal(0.01f, p.ScaleFactor);
        Assert.Equal(70, p.PropDensityPercent);
        Assert.Equal(30, p.MaxProps);
        Assert.Equal(40, p.BuildingFillPercent);
        Assert.Null(p.Seed);
    }

    [Fact]
    public void BuildParams_ExplicitSeed()
    {
        var vm = new TownMapsStepViewModel { SeedText = "123" };
        var p = vm.BuildParams();
        Assert.Equal(123, p.Seed);
    }

    [Fact]
    public void BuildParams_InvalidSeed_IsNull()
    {
        var vm = new TownMapsStepViewModel { SeedText = "abc" };
        var p = vm.BuildParams();
        Assert.Null(p.Seed);
    }

    [Fact]
    public void GridSizeModes_Has5Options()
    {
        var vm = new TownMapsStepViewModel();
        Assert.Equal(5, vm.GridSizeModes.Count);
    }

    [Fact]
    public void ShowCustomDimension_WhenCustomSelected()
    {
        var vm = new TownMapsStepViewModel();
        Assert.False(vm.ShowCustomDimension);
        vm.SelectedGridSize = GridSizeMode.Custom;
        Assert.True(vm.ShowCustomDimension);
    }
}
