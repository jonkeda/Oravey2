using Oravey2.MapGen.Pipeline;

namespace Oravey2.Tests.Pipeline;

public class PipelineStateServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PipelineStateService _service;

    public PipelineStateServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "oravey2-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _service = new PipelineStateService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task LoadAsync_FileDoesNotExist_ReturnsDefaultState()
    {
        var state = await _service.LoadAsync("nonexistent");

        Assert.Equal("nonexistent", state.RegionName);
        Assert.Equal(1, state.CurrentStep);
        Assert.False(state.Region.Completed);
    }

    [Fact]
    public async Task SaveAsync_CreatesIntermediateDirectories()
    {
        var state = new PipelineState
        {
            RegionName = "test-region",
            ContentPackPath = "content/Oravey2.Apocalyptic.NL.NH",
            CurrentStep = 2,
        };
        state.Region.Completed = true;

        await _service.SaveAsync(state);

        var path = _service.GetStatePath("test-region");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task RoundTrip_SerializeDeserialize_PreservesAllFields()
    {
        var state = new PipelineState
        {
            RegionName = "noord-holland",
            ContentPackPath = "content/Oravey2.Apocalyptic.NL.NH",
            CurrentStep = 5,
        };
        state.Region.Completed = true;
        state.Region.PresetName = "noord-holland";
        state.Region.NorthLat = 53.28945;
        state.Region.SouthLat = 52.16497;
        state.Region.EastLon = 5.378479;
        state.Region.WestLon = 3.896906;
        state.Region.OsmDownloadUrl = "https://download.geofabrik.de/europe/netherlands/noord-holland-latest.osm.pbf";
        state.Download.Completed = true;
        state.Download.SrtmDownloaded = true;
        state.Download.OsmDownloaded = true;
        state.Parse.Completed = true;
        state.Parse.TownCount = 847;
        state.Parse.RoadCount = 12340;
        state.Parse.WaterBodyCount = 234;
        state.Parse.SrtmTileCount = 4;
        state.Parse.FilteredTownCount = 142;
        state.Parse.FilteredRoadCount = 3201;
        state.Parse.FilteredWaterBodyCount = 87;
        state.TownSelection.Completed = true;
        state.TownSelection.Mode = "B";
        state.TownSelection.TownCount = 12;
        state.TownDesign.Designed.AddRange(["havenburg", "marsdiep"]);
        state.TownDesign.Remaining = 3;

        await _service.SaveAsync(state);
        var loaded = await _service.LoadAsync("noord-holland");

        Assert.Equal("noord-holland", loaded.RegionName);
        Assert.Equal("content/Oravey2.Apocalyptic.NL.NH", loaded.ContentPackPath);
        Assert.Equal(5, loaded.CurrentStep);
        Assert.True(loaded.Region.Completed);
        Assert.Equal("noord-holland", loaded.Region.PresetName);
        Assert.Equal(53.28945, loaded.Region.NorthLat);
        Assert.Equal(52.16497, loaded.Region.SouthLat);
        Assert.Equal(5.378479, loaded.Region.EastLon);
        Assert.Equal(3.896906, loaded.Region.WestLon);
        Assert.True(loaded.Download.Completed);
        Assert.True(loaded.Download.SrtmDownloaded);
        Assert.True(loaded.Download.OsmDownloaded);
        Assert.True(loaded.Parse.Completed);
        Assert.Equal(847, loaded.Parse.TownCount);
        Assert.Equal(12340, loaded.Parse.RoadCount);
        Assert.Equal(234, loaded.Parse.WaterBodyCount);
        Assert.Equal(4, loaded.Parse.SrtmTileCount);
        Assert.Equal(142, loaded.Parse.FilteredTownCount);
        Assert.Equal(3201, loaded.Parse.FilteredRoadCount);
        Assert.Equal(87, loaded.Parse.FilteredWaterBodyCount);
        Assert.True(loaded.TownSelection.Completed);
        Assert.Equal("B", loaded.TownSelection.Mode);
        Assert.Equal(12, loaded.TownSelection.TownCount);
        Assert.Equal(["havenburg", "marsdiep"], loaded.TownDesign.Designed);
        Assert.Equal(3, loaded.TownDesign.Remaining);
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingFile()
    {
        var state1 = new PipelineState { RegionName = "test", CurrentStep = 1 };
        await _service.SaveAsync(state1);

        var state2 = new PipelineState { RegionName = "test", CurrentStep = 4 };
        await _service.SaveAsync(state2);

        var loaded = await _service.LoadAsync("test");
        Assert.Equal(4, loaded.CurrentStep);
    }

    [Fact]
    public void GetStatePath_ReturnsCorrectPath()
    {
        var path = _service.GetStatePath("noord-holland");

        var expected = Path.Combine(_tempDir, "regions", "noord-holland", "pipeline-state.json");
        Assert.Equal(expected, path);
    }
}
