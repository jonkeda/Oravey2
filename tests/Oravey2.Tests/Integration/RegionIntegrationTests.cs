using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Oravey2.Core.World;
using Oravey2.MapGen.RegionTemplates;
using Xunit;

namespace Oravey2.Tests.Integration;

public class RegionIntegrationTests
{
    [Fact]
    public void SrtmGzip_RoundTrip_ProducesIdenticalGrid()
    {
        // Arrange — synthetic 1201×1201 SRTM (3-arcsecond), big-endian int16
        const int dim = 1201;
        var bytes = new byte[dim * dim * 2];
        var rng = new Random(12345);

        for (int i = 0; i < bytes.Length; i += 2)
        {
            short elevation = (short)rng.Next(-50, 500);
            bytes[i] = (byte)(elevation >> 8);
            bytes[i + 1] = (byte)(elevation & 0xFF);
        }

        var geoMapper = new GeoMapper();
        var parser = new SrtmParser(geoMapper);

        // Act — parse raw bytes
        var grid1 = parser.ParseHgtFile(bytes);

        // Act — compress to .hgt.gz, then parse from file
        var tempPath = Path.Combine(Path.GetTempPath(), $"srtm_{Guid.NewGuid()}.hgt.gz");
        try
        {
            using (var fs = File.Create(tempPath))
            using (var gz = new GZipStream(fs, CompressionLevel.Fastest))
            {
                gz.Write(bytes, 0, bytes.Length);
            }

            var grid2 = parser.ParseHgtFile(tempPath);

            // Assert
            Assert.Equal(grid1.GetLength(0), grid2.GetLength(0));
            Assert.Equal(grid1.GetLength(1), grid2.GetLength(1));

            for (int r = 0; r < dim; r++)
            {
                for (int c = 0; c < dim; c++)
                {
                    Assert.Equal(grid1[r, c], grid2[r, c]);
                }
            }
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void GeofabrikGzip_RoundTrip_PreservesJson()
    {
        // Arrange
        var original = """{"type":"FeatureCollection","features":[{"id":"europe/netherlands"}]}""";
        var tempPath = Path.Combine(Path.GetTempPath(), $"geofabrik_{Guid.NewGuid()}.json.gz");

        try
        {
            // Act — write compressed
            using (var fs = File.Create(tempPath))
            using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
            using (var writer = new StreamWriter(gz, Encoding.UTF8))
            {
                writer.Write(original);
            }

            // Act — read back
            string roundTripped;
            using (var fs = File.OpenRead(tempPath))
            using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            using (var reader = new StreamReader(gz, Encoding.UTF8))
            {
                roundTripped = reader.ReadToEnd();
            }

            // Assert
            Assert.Equal(original, roundTripped);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void RegionPreset_SaveAndLoad_RoundTrips()
    {
        // Arrange
        var original = new RegionPreset
        {
            Name = "TestRegion",
            DisplayName = "Test Region Display",
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
            // Act
            original.Save(tempPath);
            var loaded = RegionPreset.Load(tempPath);

            // Assert — all serialized properties match
            Assert.Equal(original.Name, loaded.Name);
            Assert.Equal(original.DisplayName, loaded.DisplayName);
            Assert.Equal(original.NorthLat, loaded.NorthLat);
            Assert.Equal(original.SouthLat, loaded.SouthLat);
            Assert.Equal(original.EastLon, loaded.EastLon);
            Assert.Equal(original.WestLon, loaded.WestLon);
            Assert.Equal(original.OsmDownloadUrl, loaded.OsmDownloadUrl);
            Assert.Equal(original.DefaultCullSettings, loaded.DefaultCullSettings);

            // Assert — computed paths recompute identically
            Assert.Equal(original.RegionDir, loaded.RegionDir);
            Assert.Equal(original.SrtmDir, loaded.SrtmDir);
            Assert.Equal(original.OsmFilePath, loaded.OsmFilePath);
            Assert.Equal(original.OutputFilePath, loaded.OutputFilePath);

            // Assert — computed paths are NOT in the raw JSON
            var json = File.ReadAllText(tempPath);
            Assert.DoesNotContain("\"regionDir\"", json);
            Assert.DoesNotContain("\"srtmDir\"", json);
            Assert.DoesNotContain("\"osmDir\"", json);
            Assert.DoesNotContain("\"outputDir\"", json);
            Assert.DoesNotContain("\"osmFilePath\"", json);
            Assert.DoesNotContain("\"outputFilePath\"", json);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void RegionPreset_EnsureDirectories_CreatesSubdirs()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"region_dirs_{Guid.NewGuid()}");
        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            // Arrange — change CWD so EnsureDirectories creates dirs under temp
            Directory.CreateDirectory(tempRoot);
            Directory.SetCurrentDirectory(tempRoot);

            var preset = new RegionPreset
            {
                Name = "test-region",
                DisplayName = "Test",
                NorthLat = 53.0,
                SouthLat = 52.0,
                EastLon = 6.0,
                WestLon = 4.0,
                OsmDownloadUrl = "https://example.com/test.osm.pbf"
            };

            // Act
            preset.EnsureDirectories();

            // Assert
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "data", "regions", "test-region", "srtm")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "data", "regions", "test-region", "osm")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "data", "regions", "test-region", "output")));
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void CliRegion_ResolvesCorrectPaths()
    {
        // Arrange
        var preset = new RegionPreset
        {
            Name = "test-region",
            DisplayName = "Test",
            NorthLat = 53.0,
            SouthLat = 52.0,
            EastLon = 6.0,
            WestLon = 4.0,
            OsmDownloadUrl = "https://example.com/test.osm.pbf"
        };

        // Assert
        Assert.Equal(
            Path.Combine("data", "regions", "test-region", "srtm"),
            preset.SrtmDir);

        Assert.Equal(
            Path.Combine("data", "regions", "test-region", "osm", "test-region-latest.osm.pbf"),
            preset.OsmFilePath);

        Assert.Equal(
            Path.Combine("data", "regions", "test-region", "output", "test-region.RegionTemplateFile"),
            preset.OutputFilePath);
    }
}
