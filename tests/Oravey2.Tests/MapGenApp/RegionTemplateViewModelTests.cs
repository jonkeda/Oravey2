using System.Numerics;
using Oravey2.MapGen.Download;
using Oravey2.MapGen.ViewModels;
using Oravey2.MapGen.ViewModels.RegionTemplates;
using Oravey2.MapGen.RegionTemplates;
using Xunit;

namespace Oravey2.Tests.MapGenApp;

public class RegionTemplateViewModelTests
{
    private static readonly RegionPreset NoordHolland = new()
    {
        Name = "noordholland",
        DisplayName = "Noord-Holland",
        NorthLat = 53.0,
        SouthLat = 52.2,
        EastLon = 5.5,
        WestLon = 4.0,
        OsmDownloadUrl = "https://download.geofabrik.de/europe/netherlands/noord-holland-latest.osm.pbf",
        DefaultCullSettings = new CullSettings
        {
            TownMinCategory = TownCategory.Village,
            TownMinPopulation = 1_000,
            TownMaxCount = 30,
            RoadMinClass = RoadClass.Primary,
            WaterMinAreaKm2 = 0.1
        }
    };

    private static RegionTemplateViewModel CreateVm()
        => new(new FakeDownloadService());

    // --- 4.4 Preset selection ---

    [Fact]
    public void PresetSelection_FillsAllFields()
    {
        var vm = CreateVm();

        vm.SelectedPreset = NoordHolland;

        Assert.Equal(NoordHolland.SrtmDir, vm.SrtmDirectory);
        Assert.Equal(NoordHolland.OsmFilePath, vm.OsmFilePath);
        Assert.Equal("noordholland", vm.RegionName);
        Assert.Equal(NoordHolland.OutputFilePath, vm.OutputPath);
        Assert.Equal(NoordHolland.DefaultCullSettings, vm.CullSettings);
    }

    [Fact]
    public void PresetSelection_ClearsOldData()
    {
        var vm = CreateVm();

        vm.Towns.Add(MakeTown("A", 52.0, 4.0, 500, TownCategory.Village));
        vm.Roads.Add(MakeRoad(RoadClass.Primary));
        vm.WaterBodies.Add(MakeWater(WaterType.Lake));

        Assert.NotEmpty(vm.Towns);
        Assert.NotEmpty(vm.Roads);
        Assert.NotEmpty(vm.WaterBodies);

        vm.SelectedPreset = NoordHolland;

        Assert.Empty(vm.Towns);
        Assert.Empty(vm.Roads);
        Assert.Empty(vm.WaterBodies);
    }

    // --- 4.3 AutoCull ---

    [Fact]
    public void AutoCull_AppliesSettings()
    {
        var vm = CreateVm();
        vm.CullSettings = new CullSettings
        {
            TownMinCategory = TownCategory.Town,
            TownMinPopulation = 5_000,
            TownMaxCount = 100,
            TownAlwaysKeepCities = true,
            TownAlwaysKeepMetropolis = true,
            RoadMinClass = RoadClass.Primary,
            RoadSimplifyGeometry = false
        };

        vm.Towns.Add(MakeTown("Hamlet", 52.0, 4.0, 100, TownCategory.Hamlet));
        vm.Towns.Add(MakeTown("BigTown", 52.1, 4.1, 20_000, TownCategory.Town, new Vector2(10_000, 0)));
        vm.Towns.Add(MakeTown("SmallVillage", 52.2, 4.2, 500, TownCategory.Village, new Vector2(20_000, 0)));
        vm.Towns.Add(MakeTown("City", 52.3, 4.3, 100_000, TownCategory.City, new Vector2(30_000, 0)));

        vm.AutoCull();

        Assert.False(vm.Towns[0].IsIncluded);  // Hamlet — below min category
        Assert.True(vm.Towns[1].IsIncluded);   // BigTown — meets category + pop
        Assert.False(vm.Towns[2].IsIncluded);  // SmallVillage — below min category
        Assert.True(vm.Towns[3].IsIncluded);   // City — always-keep
    }

    // --- Select all / none ---

    [Fact]
    public void SelectAll_SetsAllIncluded()
    {
        var vm = CreateVm();
        AddExcluded(vm);

        vm.SelectAll();

        Assert.All(vm.Towns, t => Assert.True(t.IsIncluded));
        Assert.All(vm.Roads, r => Assert.True(r.IsIncluded));
        Assert.All(vm.WaterBodies, w => Assert.True(w.IsIncluded));
    }

    [Fact]
    public void SelectNone_ClearsAllIncluded()
    {
        var vm = CreateVm();
        vm.Towns.Add(MakeTown("A", 52.0, 4.0, 100, TownCategory.Village));
        vm.Roads.Add(MakeRoad(RoadClass.Primary));
        vm.WaterBodies.Add(MakeWater(WaterType.Lake));

        vm.SelectNone();

        Assert.All(vm.Towns, t => Assert.False(t.IsIncluded));
        Assert.All(vm.Roads, r => Assert.False(r.IsIncluded));
        Assert.All(vm.WaterBodies, w => Assert.False(w.IsIncluded));
    }

    // --- Summary ---

    [Fact]
    public void Summary_ReflectsCollectionCounts()
    {
        var vm = CreateVm();
        vm.Towns.Add(MakeTown("A", 52.0, 4.0, 100, TownCategory.Village));
        vm.Towns.Add(MakeTown("B", 52.1, 4.1, 200, TownCategory.Town));
        vm.Roads.Add(MakeRoad(RoadClass.Primary));

        Assert.Contains("2 towns", vm.Summary);
        Assert.Contains("1 roads", vm.Summary);
        Assert.Contains("0 water", vm.Summary);
    }

    // --- Helpers ---

    private static TownItem MakeTown(string name, double lat, double lon, int pop,
        TownCategory cat, Vector2? gamePos = null)
        => new(new TownEntry(name, lat, lon, pop, gamePos ?? Vector2.Zero, cat));

    private static RoadItem MakeRoad(RoadClass cls)
        => new(new RoadSegment(cls, [Vector2.Zero, new Vector2(1000, 0)]));

    private static WaterItem MakeWater(WaterType type)
        => new(new WaterBody(type, [Vector2.Zero, new Vector2(1000, 0), new Vector2(1000, 1000)]));

    private static void AddExcluded(RegionTemplateViewModel vm)
    {
        var t = MakeTown("A", 52.0, 4.0, 100, TownCategory.Village); t.IsIncluded = false; vm.Towns.Add(t);
        var r = MakeRoad(RoadClass.Primary); r.IsIncluded = false; vm.Roads.Add(r);
        var w = MakeWater(WaterType.Lake); w.IsIncluded = false; vm.WaterBodies.Add(w);
    }

    // --- Test doubles ---

    private sealed class FakeDownloadService : IDataDownloadService
    {
        public Task DownloadSrtmTilesAsync(SrtmDownloadRequest request,
            IProgress<DownloadProgress> progress, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DownloadOsmExtractAsync(OsmDownloadRequest request,
            IProgress<DownloadProgress> progress, CancellationToken ct = default)
            => Task.CompletedTask;

        public List<string> GetRequiredSrtmTileNames(double northLat, double southLat,
            double eastLon, double westLon) => [];

        public List<string> GetExistingSrtmTiles(string directory) => [];
    }
}
