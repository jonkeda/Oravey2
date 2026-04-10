using Oravey2.MapGen.Download;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.Tests.Pipeline;

public class DownloadStepViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FakeSettingsService _settings = new();

    public DownloadStepViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"oravey2_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private DownloadStepViewModel MakeVM(IDataDownloadService? svc = null)
        => new(svc ?? new FakeDownloadService(), _settings);

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
            OsmDownloadUrl = "https://example.com/nh.osm.pbf",
        }
    };

    [Fact]
    public void CanComplete_DefaultIsFalse()
    {
        var vm = MakeVM();
        Assert.False(vm.CanComplete);
    }

    [Fact]
    public void CheckExistingFiles_DetectsRequiredSrtmTiles()
    {
        var service = new FakeDownloadService
        {
            RequiredTiles = ["N52E004", "N52E005"],
            ExistingTiles = ["N52E004"],
        };

        var vm = MakeVM(service);
        vm.Initialize(_tempDir);
        vm.Load(MakeState());

        Assert.Equal(2, vm.RequiredSrtmCount);
        Assert.Equal(1, vm.DownloadedSrtmCount);
        Assert.False(vm.SrtmReady);
    }

    [Fact]
    public void CheckExistingFiles_SrtmReadyWhenAllPresent()
    {
        var service = new FakeDownloadService
        {
            RequiredTiles = ["N52E004", "N52E005"],
            ExistingTiles = ["N52E004", "N52E005"],
        };

        var vm = MakeVM(service);
        vm.Initialize(_tempDir);
        vm.Load(MakeState());

        Assert.True(vm.SrtmReady);
    }

    [Fact]
    public void CheckExistingFiles_DetectsOsmFile()
    {
        var service = new FakeDownloadService
        {
            RequiredTiles = ["N52E004"],
            ExistingTiles = ["N52E004"],
        };

        // Create the OSM file
        var osmDir = Path.Combine(_tempDir, "regions", "noord-holland", "osm");
        Directory.CreateDirectory(osmDir);
        File.WriteAllText(Path.Combine(osmDir, "noord-holland-latest.osm.pbf"), "data");

        var vm = MakeVM(service);
        vm.Initialize(_tempDir);
        vm.Load(MakeState());

        Assert.True(vm.OsmReady);
        Assert.Equal("noord-holland-latest.osm.pbf", vm.OsmFileName);
    }

    [Fact]
    public void CanComplete_TrueWhenBothReady()
    {
        var service = new FakeDownloadService
        {
            RequiredTiles = ["N52E004"],
            ExistingTiles = ["N52E004"],
        };

        var osmDir = Path.Combine(_tempDir, "regions", "noord-holland", "osm");
        Directory.CreateDirectory(osmDir);
        File.WriteAllText(Path.Combine(osmDir, "noord-holland-latest.osm.pbf"), "data");

        var vm = MakeVM(service);
        vm.Initialize(_tempDir);
        vm.Load(MakeState());

        Assert.True(vm.CanComplete);
    }

    [Fact]
    public void OnNext_MarksStateCompleted()
    {
        var service = new FakeDownloadService
        {
            RequiredTiles = ["N52E004"],
            ExistingTiles = ["N52E004"],
        };

        var osmDir = Path.Combine(_tempDir, "regions", "noord-holland", "osm");
        Directory.CreateDirectory(osmDir);
        File.WriteAllText(Path.Combine(osmDir, "noord-holland-latest.osm.pbf"), "data");

        var state = MakeState();
        var vm = MakeVM(service);
        vm.Initialize(_tempDir);
        vm.Load(state);

        vm.NextCommand.Execute(null);

        Assert.True(state.Download.Completed);
        Assert.True(state.Download.SrtmDownloaded);
        Assert.True(state.Download.OsmDownloaded);
    }

    [Fact]
    public void OnNext_InvokesStepCompletedCallback()
    {
        var service = new FakeDownloadService
        {
            RequiredTiles = ["N52E004"],
            ExistingTiles = ["N52E004"],
        };

        var osmDir = Path.Combine(_tempDir, "regions", "noord-holland", "osm");
        Directory.CreateDirectory(osmDir);
        File.WriteAllText(Path.Combine(osmDir, "noord-holland-latest.osm.pbf"), "data");

        var vm = MakeVM(service);
        vm.Initialize(_tempDir);
        vm.Load(MakeState());

        var invoked = false;
        vm.StepCompleted = () => invoked = true;
        vm.NextCommand.Execute(null);

        Assert.True(invoked);
    }

    [Fact]
    public void CheckExistingFiles_SkipsWhenNoRegionName()
    {
        var vm = MakeVM();
        vm.Initialize(_tempDir);
        vm.Load(new PipelineState());

        Assert.Equal(0, vm.RequiredSrtmCount);
        Assert.Equal("Not checked", vm.SrtmStatusText);
    }

    [Fact]
    public void GetSrtmDirectory_ReturnsCorrectPath()
    {
        var vm = MakeVM();
        vm.Initialize(_tempDir);
        vm.Load(MakeState());

        var expected = Path.Combine(_tempDir, "regions", "noord-holland", "srtm");
        Assert.Equal(expected, vm.GetSrtmDirectory());
    }

    [Fact]
    public void GetOsmFilePath_ReturnsCorrectPath()
    {
        var vm = MakeVM();
        vm.Initialize(_tempDir);
        vm.Load(MakeState());

        var expected = Path.Combine(_tempDir, "regions", "noord-holland", "osm", "noord-holland-latest.osm.pbf");
        Assert.Equal(expected, vm.GetOsmFilePath());
    }

    /// <summary>
    /// Minimal fake for testing without network or file I/O.
    /// </summary>
    private class FakeDownloadService : IDataDownloadService
    {
        public List<string> RequiredTiles { get; set; } = [];
        public List<string> ExistingTiles { get; set; } = [];

        public List<string> GetRequiredSrtmTileNames(double northLat, double southLat, double eastLon, double westLon)
            => RequiredTiles;

        public List<string> GetExistingSrtmTiles(string directory)
            => ExistingTiles;

        public Task DownloadSrtmTilesAsync(SrtmDownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DownloadOsmExtractAsync(OsmDownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private class FakeSettingsService : ISettingsService
    {
        public string Get(string key, string defaultValue) => defaultValue;
        public void Set(string key, string value) { }
        public Task<string?> GetSecureAsync(string key) => Task.FromResult<string?>(null);
        public Task SetSecureAsync(string key, string value) => Task.CompletedTask;
    }
}
