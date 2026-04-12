using System.Text.Json;
using Oravey2.Core.World;
using Oravey2.MapGen.RegionTemplates;
using Xunit;

namespace Oravey2.Tests.RegionTemplates;

public class RegionPresetTests
{
    [Fact]
    public void NoordHolland_Preset_LoadsCorrectly()
    {
        var presetPath = Path.Combine(FindRepoRoot(), "data", "presets", "noordholland.regionpreset");
        var preset = RegionPreset.Load(presetPath);

        Assert.Equal("NoordHolland", preset.Name);
        Assert.Equal("Noord-Holland", preset.DisplayName);
        Assert.Equal(53.0, preset.NorthLat);
        Assert.Equal(52.2, preset.SouthLat);
        Assert.Equal(5.5, preset.EastLon);
        Assert.Equal(4.0, preset.WestLon);
        Assert.Contains("geofabrik.de", preset.OsmDownloadUrl);
    }

    [Fact]
    public void RoundTrip_Json_PreservesAllFields()
    {
        var original = new RegionPreset
        {
            Name = "TestRegion",
            DisplayName = "Test Region",
            NorthLat = 53.5,
            SouthLat = 52.0,
            EastLon = 6.0,
            WestLon = 3.5,
            OsmDownloadUrl = "https://example.com/test.osm.pbf",
            DefaultCullSettings = new CullSettings
            {
                TownMinPopulation = 2000,
                RoadMinClass = LinearFeatureType.Secondary,
                WaterMinAreaKm2 = 0.5
            }
        };

        var tempPath = Path.Combine(Path.GetTempPath(), $"preset_{Guid.NewGuid()}.regionpreset");
        try
        {
            original.Save(tempPath);
            var loaded = RegionPreset.Load(tempPath);

            Assert.Equal(original.Name, loaded.Name);
            Assert.Equal(original.DisplayName, loaded.DisplayName);
            Assert.Equal(original.NorthLat, loaded.NorthLat);
            Assert.Equal(original.SouthLat, loaded.SouthLat);
            Assert.Equal(original.EastLon, loaded.EastLon);
            Assert.Equal(original.WestLon, loaded.WestLon);
            Assert.Equal(original.OsmDownloadUrl, loaded.OsmDownloadUrl);
            Assert.Equal(original.DefaultCullSettings, loaded.DefaultCullSettings);

            // Computed paths recompute from Name
            Assert.Equal(original.RegionDir, loaded.RegionDir);
            Assert.Equal(original.OsmFilePath, loaded.OsmFilePath);
            Assert.Equal(original.OutputFilePath, loaded.OutputFilePath);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void DefaultCullSettings_IsEmbedded()
    {
        var presetPath = Path.Combine(FindRepoRoot(), "data", "presets", "noordholland.regionpreset");
        var preset = RegionPreset.Load(presetPath);

        var cull = preset.DefaultCullSettings;
        Assert.Equal(TownCategory.Village, cull.TownMinCategory);
        Assert.Equal(1000, cull.TownMinPopulation);
        Assert.Equal(LinearFeatureType.Secondary, cull.RoadMinClass);
        Assert.True(cull.RoadAlwaysKeepMotorways);
        Assert.Equal(0.01, cull.WaterMinAreaKm2);
        Assert.True(cull.WaterAlwaysKeepSea);
    }

    [Fact]
    public void RegionDir_ComputesFromName()
    {
        var preset = CreateTestPreset("MyRegion");

        Assert.Equal(Path.Combine("data", "regions", "MyRegion"), preset.RegionDir);
    }

    [Fact]
    public void SrtmDir_IsUnderRegionDir()
    {
        var preset = CreateTestPreset("MyRegion");

        Assert.Equal(Path.Combine("data", "regions", "MyRegion", "srtm"), preset.SrtmDir);
        Assert.StartsWith(preset.RegionDir, preset.SrtmDir);
    }

    [Fact]
    public void OsmFilePath_UsesName()
    {
        var preset = CreateTestPreset("MyRegion");

        Assert.Equal(Path.Combine("data", "regions", "MyRegion", "osm", "MyRegion-latest.osm.pbf"), preset.OsmFilePath);
    }

    [Fact]
    public void OutputFilePath_UsesName()
    {
        var preset = CreateTestPreset("MyRegion");

        Assert.Equal(Path.Combine("data", "regions", "MyRegion", "output", "MyRegion.RegionTemplateFile"), preset.OutputFilePath);
    }

    [Fact]
    public void PresetFilePath_IsRegionJson()
    {
        var preset = CreateTestPreset("MyRegion");

        Assert.Equal(Path.Combine("data", "regions", "MyRegion", "region.json"), preset.PresetFilePath);
    }

    [Fact]
    public void RoundTrip_Json_OmitsComputedPaths()
    {
        var preset = CreateTestPreset("TestRegion");

        var tempPath = Path.Combine(Path.GetTempPath(), $"preset_{Guid.NewGuid()}.regionpreset");
        try
        {
            preset.Save(tempPath);
            var json = File.ReadAllText(tempPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.False(root.TryGetProperty("regionDir", out _));
            Assert.False(root.TryGetProperty("srtmDir", out _));
            Assert.False(root.TryGetProperty("osmDir", out _));
            Assert.False(root.TryGetProperty("outputDir", out _));
            Assert.False(root.TryGetProperty("osmFilePath", out _));
            Assert.False(root.TryGetProperty("outputFilePath", out _));
            Assert.False(root.TryGetProperty("presetFilePath", out _));
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void EnsureDirectories_CreatesAllSubdirs()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"preset_dirs_{Guid.NewGuid()}");
        try
        {
            // Use a name that makes RegionDir point under tempRoot
            var preset = new RegionPreset
            {
                Name = Path.Combine(tempRoot, "TestRegion"),
                DisplayName = "Test",
                NorthLat = 53.0,
                SouthLat = 52.0,
                EastLon = 6.0,
                WestLon = 4.0,
                OsmDownloadUrl = "https://example.com/test.osm.pbf"
            };

            preset.EnsureDirectories();

            Assert.True(Directory.Exists(preset.SrtmDir));
            Assert.True(Directory.Exists(preset.OsmDir));
            Assert.True(Directory.Exists(preset.OutputDir));
        }
        finally
        {
            if (Directory.Exists(Path.Combine("data", "regions", tempRoot)))
                Directory.Delete(Path.Combine("data", "regions", tempRoot), true);
        }
    }

    private static RegionPreset CreateTestPreset(string name) => new()
    {
        Name = name,
        DisplayName = "Test",
        NorthLat = 53.0,
        SouthLat = 52.0,
        EastLon = 6.0,
        WestLon = 4.0,
        OsmDownloadUrl = "https://example.com/test.osm.pbf"
    };

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Oravey2.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not find repo root (Oravey2.sln)");
    }
}
