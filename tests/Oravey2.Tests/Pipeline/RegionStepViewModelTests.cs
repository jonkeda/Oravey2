using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.Tests.Pipeline;

public class RegionStepViewModelTests : IDisposable
{
    private readonly string _tempDir;

    public RegionStepViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"oravey2_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static RegionPreset MakePreset() => new()
    {
        Name = "noord-holland",
        DisplayName = "Noord-Holland",
        NorthLat = 52.9,
        SouthLat = 52.2,
        EastLon = 5.2,
        WestLon = 4.5,
        OsmDownloadUrl = "https://example.com/nh.osm.pbf",
    };

    [Fact]
    public void CanComplete_DefaultIsFalse()
    {
        var vm = new RegionStepViewModel();
        Assert.False(vm.CanComplete);
    }

    [Fact]
    public void CanComplete_FalseWhenOnlyRegionSet()
    {
        var vm = new RegionStepViewModel();
        vm.ApplyRegion(MakePreset());
        Assert.False(vm.CanComplete);
    }

    [Fact]
    public void CanComplete_FalseWhenOnlyPackSet()
    {
        var vm = new RegionStepViewModel();
        vm.SelectedContentPack = "Oravey2.Apocalyptic.NL.NH";
        Assert.False(vm.CanComplete);
    }

    [Fact]
    public void CanComplete_TrueWhenRegionAndPackSet()
    {
        var vm = new RegionStepViewModel();
        vm.ApplyRegion(MakePreset());
        vm.SelectedContentPack = "Oravey2.Apocalyptic.NL.NH";
        Assert.True(vm.CanComplete);
    }

    [Fact]
    public void ApplyRegion_SetsAllProperties()
    {
        var vm = new RegionStepViewModel();
        var preset = MakePreset();

        vm.ApplyRegion(preset);

        Assert.Equal("noord-holland", vm.RegionName);
        Assert.Equal("Noord-Holland", vm.RegionDisplayName);
        Assert.Equal(52.9, vm.NorthLat);
        Assert.Equal(52.2, vm.SouthLat);
        Assert.Equal(5.2, vm.EastLon);
        Assert.Equal(4.5, vm.WestLon);
        Assert.Equal("https://example.com/nh.osm.pbf", vm.OsmUrl);
        Assert.True(vm.HasRegion);
    }

    [Fact]
    public void ApplyRegion_UpdatesPipelineState()
    {
        var vm = new RegionStepViewModel();
        var state = new PipelineState();
        vm.Initialize(state);

        vm.ApplyRegion(MakePreset());

        Assert.Equal("noord-holland", state.RegionName);
        Assert.Equal("noord-holland", state.Region.PresetName);
        Assert.Equal(52.9, state.Region.NorthLat);
        Assert.Equal("https://example.com/nh.osm.pbf", state.Region.OsmDownloadUrl);
    }

    [Fact]
    public void OnNext_MarksStepCompleted()
    {
        var vm = new RegionStepViewModel();
        var state = new PipelineState();
        vm.Initialize(state);
        vm.ApplyRegion(MakePreset());
        vm.SelectedContentPack = "Oravey2.Apocalyptic.NL.NH";

        vm.NextCommand.Execute(null);

        Assert.True(state.Region.Completed);
        Assert.Equal("Oravey2.Apocalyptic.NL.NH", state.ContentPackPath);
    }

    [Fact]
    public void OnNext_InvokesStepCompletedCallback()
    {
        var vm = new RegionStepViewModel();
        var state = new PipelineState();
        vm.Initialize(state);
        vm.ApplyRegion(MakePreset());
        vm.SelectedContentPack = "Oravey2.Apocalyptic.NL.NH";

        var invoked = false;
        vm.StepCompleted = () => invoked = true;

        vm.NextCommand.Execute(null);

        Assert.True(invoked);
    }

    [Fact]
    public void Initialize_RestoresFromState()
    {
        var state = new PipelineState
        {
            RegionName = "noord-holland",
            ContentPackPath = "Oravey2.Apocalyptic.NL.NH",
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

        var vm = new RegionStepViewModel();
        vm.Initialize(state);

        Assert.Equal("noord-holland", vm.RegionName);
        Assert.Equal(52.9, vm.NorthLat);
        Assert.Equal("Oravey2.Apocalyptic.NL.NH", vm.SelectedContentPack);
        Assert.True(vm.HasRegion);
    }

    [Fact]
    public void ScanContentPacks_FindsPacksWithManifest()
    {
        // Create two content pack dirs: one with manifest, one without
        var pack1 = Path.Combine(_tempDir, "Oravey2.Apocalyptic");
        Directory.CreateDirectory(pack1);
        File.WriteAllText(Path.Combine(pack1, "manifest.json"), "{}");

        var pack2 = Path.Combine(_tempDir, "Oravey2.Fantasy");
        Directory.CreateDirectory(pack2);
        File.WriteAllText(Path.Combine(pack2, "manifest.json"), "{}");

        var noPack = Path.Combine(_tempDir, "RandomFolder");
        Directory.CreateDirectory(noPack);

        var vm = new RegionStepViewModel();
        vm.ScanContentPacks(_tempDir);

        Assert.Equal(2, vm.ContentPacks.Count);
        Assert.Contains("Oravey2.Apocalyptic", vm.ContentPacks);
        Assert.Contains("Oravey2.Fantasy", vm.ContentPacks);
    }

    [Fact]
    public void ScanContentPacks_EmptyWhenDirMissing()
    {
        var vm = new RegionStepViewModel();
        vm.ScanContentPacks(Path.Combine(_tempDir, "nonexistent"));

        Assert.Empty(vm.ContentPacks);
    }

    [Fact]
    public void BoundingBoxText_EmptyWhenNoRegion()
    {
        var vm = new RegionStepViewModel();
        Assert.Empty(vm.BoundingBoxText);
    }

    [Fact]
    public void BoundingBoxText_FormattedWhenRegionSet()
    {
        var vm = new RegionStepViewModel();
        vm.ApplyRegion(MakePreset());

        Assert.Contains("52.9000", vm.BoundingBoxText);
        Assert.Contains("52.2000", vm.BoundingBoxText);
    }
}
