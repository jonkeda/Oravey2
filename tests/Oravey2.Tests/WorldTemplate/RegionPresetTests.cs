using Oravey2.MapGen.WorldTemplate;
using Xunit;

namespace Oravey2.Tests.WorldTemplate;

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
        Assert.Equal("noordholland.osm.pbf", preset.OsmFileName);
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
            OsmFileName = "test.osm.pbf",
            DefaultSrtmDir = "custom/srtm",
            DefaultOutputDir = "custom/output",
            DefaultCullSettings = new CullSettings
            {
                TownMinPopulation = 2000,
                RoadMinClass = RoadClass.Secondary,
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
            Assert.Equal(original.OsmFileName, loaded.OsmFileName);
            Assert.Equal(original.DefaultSrtmDir, loaded.DefaultSrtmDir);
            Assert.Equal(original.DefaultOutputDir, loaded.DefaultOutputDir);
            Assert.Equal(original.DefaultCullSettings, loaded.DefaultCullSettings);
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
        Assert.Equal(RoadClass.Secondary, cull.RoadMinClass);
        Assert.True(cull.RoadAlwaysKeepMotorways);
        Assert.Equal(0.01, cull.WaterMinAreaKm2);
        Assert.True(cull.WaterAlwaysKeepSea);
    }

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
