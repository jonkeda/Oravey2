using Oravey2.Contracts.ContentPack;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;
using Oravey2.MapGen.ViewModels;
using System.Text.Json;

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

    private static GeofabrikRegion MakeRegion() => new()
    {
        Id = "noord-holland",
        Name = "Noord-Holland",
        Iso3166Alpha2 = ["NL"],
        Iso3166_2 = ["NL-NH"],
        PbfUrl = "https://example.com/nh.osm.pbf",
        Parent = "netherlands",
        Bounds = new BoundingBox(52.9, 52.2, 5.2, 4.5),
    };

    [Fact]
    public void CanComplete_DefaultIsFalse()
    {
        var vm = new RegionStepViewModel();
        Assert.False(vm.CanComplete);
    }

    [Fact]
    public void CanComplete_TrueWhenPackSelected()
    {
        CreateManifest("Oravey2.Apocalyptic.NL.NH", regionCode: "NL-NH", parent: "oravey2.apocalyptic.nl");

        var vm = new RegionStepViewModel();
        vm.ScanContentPacks(_tempDir);
        vm.SelectedPackInfo = vm.ContentPackInfos[0];

        Assert.True(vm.CanComplete);
    }

    [Fact]
    public void ScanContentPacks_FindsPacksWithRegionCode()
    {
        CreateManifest("Oravey2.Apocalyptic.NL.NH", regionCode: "NL-NH", parent: "oravey2.apocalyptic.nl");
        CreateManifest("Oravey2.Apocalyptic", regionCode: null, parent: null); // genre root — no regionCode
        CreateManifest("RandomFolder"); // no manifest

        var vm = new RegionStepViewModel();
        vm.ScanContentPacks(_tempDir);

        Assert.Single(vm.ContentPackInfos);
        Assert.Equal("Oravey2.Apocalyptic.NL.NH", vm.ContentPackInfos[0].DirectoryName);
    }

    [Fact]
    public void ScanContentPacks_EmptyWhenDirMissing()
    {
        var vm = new RegionStepViewModel();
        vm.ScanContentPacks(Path.Combine(_tempDir, "nonexistent"));

        Assert.Empty(vm.ContentPackInfos);
    }

    [Fact]
    public void GetExistingGenres_ReturnsGenreRoots()
    {
        CreateManifest("Oravey2.Apocalyptic", regionCode: null, parent: null);
        CreateManifest("Oravey2.Fantasy", regionCode: null, parent: null);
        CreateManifest("Oravey2.Apocalyptic.NL", regionCode: "NL", parent: "oravey2.apocalyptic");

        var vm = new RegionStepViewModel();
        vm.ScanContentPacks(_tempDir);

        var genres = vm.GetExistingGenres();
        Assert.Equal(2, genres.Count);
        Assert.Contains("Apocalyptic", genres);
        Assert.Contains("Fantasy", genres);
    }

    [Fact]
    public void BuildPackChain_CreatesThreeLevelsForSubRegion()
    {
        var region = MakeRegion();
        var chain = RegionStepViewModel.BuildPackChain("Apocalyptic", region);

        Assert.Equal(3, chain.Count);
        Assert.Equal("Oravey2.Apocalyptic", chain[0].Name);
        Assert.Null(chain[0].RegionCode);
        Assert.Null(chain[0].Parent);

        Assert.Equal("Oravey2.Apocalyptic.NL", chain[1].Name);
        Assert.Equal("NL", chain[1].RegionCode);
        Assert.Equal("oravey2.apocalyptic", chain[1].Parent);

        Assert.Equal("Oravey2.Apocalyptic.NL.NH", chain[2].Name);
        Assert.Equal("NL-NH", chain[2].RegionCode);
        Assert.Equal("oravey2.apocalyptic.nl", chain[2].Parent);
    }

    [Fact]
    public void BuildPackChain_CreatesTwoLevelsForCountry()
    {
        var region = new GeofabrikRegion
        {
            Id = "france",
            Name = "France",
            Iso3166Alpha2 = ["FR"],
            PbfUrl = "https://example.com/france.osm.pbf",
        };
        var chain = RegionStepViewModel.BuildPackChain("Fantasy", region);

        Assert.Equal(2, chain.Count);
        Assert.Equal("Oravey2.Fantasy", chain[0].Name);
        Assert.Equal("Oravey2.Fantasy.FR", chain[1].Name);
        Assert.Equal("FR", chain[1].RegionCode);
    }

    [Fact]
    public void EnsurePackChain_ScaffoldsDirectoriesAndManifests()
    {
        var vm = new RegionStepViewModel();
        vm.ScanContentPacks(_tempDir);

        vm.EnsurePackChain("Apocalyptic", MakeRegion());

        // Genre root
        var genreDir = Path.Combine(_tempDir, "Oravey2.Apocalyptic");
        Assert.True(Directory.Exists(genreDir));
        Assert.True(File.Exists(Path.Combine(genreDir, "manifest.json")));

        // Country
        var countryDir = Path.Combine(_tempDir, "Oravey2.Apocalyptic.NL");
        Assert.True(Directory.Exists(countryDir));

        // Region (leaf)
        var regionDir = Path.Combine(_tempDir, "Oravey2.Apocalyptic.NL.NH");
        Assert.True(Directory.Exists(Path.Combine(regionDir, "data")));
        Assert.True(Directory.Exists(Path.Combine(regionDir, "towns")));
        Assert.True(Directory.Exists(Path.Combine(regionDir, "overworld")));
        Assert.True(Directory.Exists(Path.Combine(regionDir, "assets", "meshes")));

        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(regionDir, "manifest.json")));
        Assert.Equal("oravey2.apocalyptic.nl.nh", manifest.RootElement.GetProperty("id").GetString());
        Assert.Equal("NL-NH", manifest.RootElement.GetProperty("regionCode").GetString());
        Assert.Equal("oravey2.apocalyptic.nl", manifest.RootElement.GetProperty("parent").GetString());
    }

    [Fact]
    public void EnsurePackChain_SelectsLeafPack()
    {
        var vm = new RegionStepViewModel();
        vm.ScanContentPacks(_tempDir);

        vm.EnsurePackChain("Apocalyptic", MakeRegion());

        Assert.NotNull(vm.SelectedPackInfo);
        Assert.Equal("Oravey2.Apocalyptic.NL.NH", vm.SelectedPackInfo!.DirectoryName);
        Assert.True(vm.CanComplete);
    }

    [Fact]
    public void EnsurePackChain_SkipsExistingDirectories()
    {
        // Pre-create the genre root
        var genreDir = Path.Combine(_tempDir, "Oravey2.Apocalyptic");
        Directory.CreateDirectory(genreDir);
        File.WriteAllText(Path.Combine(genreDir, "manifest.json"),
            JsonSerializer.Serialize(new { id = "oravey2.apocalyptic", name = "Post-Apocalyptic", version = "0.1.0", description = "", author = "" },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var vm = new RegionStepViewModel();
        vm.ScanContentPacks(_tempDir);

        vm.EnsurePackChain("Apocalyptic", MakeRegion());

        // Genre root manifest should be unchanged (original)
        var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(genreDir, "manifest.json")));
        Assert.Equal("Post-Apocalyptic", json.RootElement.GetProperty("name").GetString());

        // But country + region should be created
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "Oravey2.Apocalyptic.NL")));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "Oravey2.Apocalyptic.NL.NH")));
    }

    [Fact]
    public void OnNext_SetsContentPackPath()
    {
        CreateManifest("Oravey2.Apocalyptic.NL.NH", regionCode: "NL-NH", parent: "oravey2.apocalyptic.nl");

        var vm = new RegionStepViewModel();
        var state = new PipelineState();
        vm.ScanContentPacks(_tempDir);
        vm.Load(state);
        vm.SelectedPackInfo = vm.ContentPackInfos[0];

        vm.NextCommand.Execute(null);

        Assert.True(state.Region.Completed);
        Assert.Equal(Path.Combine(_tempDir, "Oravey2.Apocalyptic.NL.NH"), state.ContentPackPath);
    }

    [Fact]
    public void OnNext_InvokesStepCompletedCallback()
    {
        CreateManifest("Oravey2.Apocalyptic.NL.NH", regionCode: "NL-NH", parent: "oravey2.apocalyptic.nl");

        var vm = new RegionStepViewModel();
        var state = new PipelineState();
        vm.ScanContentPacks(_tempDir);
        vm.Load(state);
        vm.SelectedPackInfo = vm.ContentPackInfos[0];

        var invoked = false;
        vm.StepCompleted = () => invoked = true;

        vm.NextCommand.Execute(null);

        Assert.True(invoked);
    }

    [Fact]
    public void Load_RestoresSelectedPackFromState()
    {
        CreateManifest("Oravey2.Apocalyptic.NL.NH", regionCode: "NL-NH", parent: "oravey2.apocalyptic.nl");

        var state = new PipelineState
        {
            ContentPackPath = Path.Combine(_tempDir, "Oravey2.Apocalyptic.NL.NH"),
        };

        var vm = new RegionStepViewModel();
        vm.ScanContentPacks(_tempDir);
        vm.Load(state);

        Assert.NotNull(vm.SelectedPackInfo);
        Assert.Equal("Oravey2.Apocalyptic.NL.NH", vm.SelectedPackInfo!.DirectoryName);
        Assert.True(vm.CanComplete);
    }

    [Fact]
    public void HasSelectedPack_FalseByDefault()
    {
        var vm = new RegionStepViewModel();
        Assert.False(vm.HasSelectedPack);
    }

    [Fact]
    public void ContentRoot_SetByScan()
    {
        var vm = new RegionStepViewModel();
        vm.ScanContentPacks(_tempDir);
        Assert.Equal(_tempDir, vm.ContentRoot);
    }

    private void CreateManifest(string packName, string? regionCode = null, string? parent = null)
    {
        var dir = Path.Combine(_tempDir, packName);
        Directory.CreateDirectory(dir);
        var manifest = new { id = packName.ToLowerInvariant(), name = packName, version = "0.1.0", description = "", author = "", parent = parent ?? "", regionCode };
        File.WriteAllText(
            Path.Combine(dir, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
    }
}

