using System.Text.Json;
using Microsoft.Data.Sqlite;
using Oravey2.Core.Data;
using Oravey2.Core.World;
using Oravey2.MapGen.Pipeline;

namespace Oravey2.Tests.Pipeline;

public class ContentPackExporterTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _tempDir;

    public ContentPackExporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cpe_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Export_CreatesDbFile()
    {
        var packDir = WriteSingleTownPack();
        var dbPath = Path.Combine(_tempDir, "world.db");

        var exporter = new ContentPackExporter();
        exporter.Export(packDir, dbPath);

        Assert.True(File.Exists(dbPath));
    }

    [Fact]
    public void Export_PopulatesRegion()
    {
        var packDir = WriteSingleTownPack();
        var dbPath = Path.Combine(_tempDir, "world.db");

        var exporter = new ContentPackExporter();
        exporter.Export(packDir, dbPath);

        using var store = new WorldMapStore(dbPath);
        var region = store.GetRegionByName("Test Region");
        Assert.NotNull(region);
    }

    [Fact]
    public void Export_ReturnsImportResult()
    {
        var packDir = WriteSingleTownPack();
        var dbPath = Path.Combine(_tempDir, "world.db");

        var exporter = new ContentPackExporter();
        var result = exporter.Export(packDir, dbPath);

        Assert.Equal("Test Region", result.RegionName);
        Assert.Equal(1, result.TownsImported);
        Assert.True(result.ChunksWritten > 0);
        Assert.Equal(2, result.EntitySpawnsInserted);
        Assert.Equal(1, result.PoisInserted);
    }

    // ── Helpers ──

    private string WriteSingleTownPack()
    {
        var packDir = Path.Combine(_tempDir, "single_town");
        Directory.CreateDirectory(packDir);
        WriteManifest(packDir);

        var townDir = Path.Combine(packDir, "towns", "testtown");
        Directory.CreateDirectory(townDir);

        var surface = new int[16][];
        for (int y = 0; y < 16; y++)
        {
            surface[y] = new int[16];
            for (int x = 0; x < 16; x++)
                surface[y][x] = (int)SurfaceType.Dirt;
        }

        File.WriteAllText(Path.Combine(townDir, "layout.json"),
            JsonSerializer.Serialize(new { width = 16, height = 16, surface }, JsonOptions));

        var buildings = new[]
        {
            new { id = "bld_001", name = "Warehouse", meshAsset = "warehouse.glb",
                  placement = new { chunkX = 0, chunkY = 0, localTileX = 5, localTileY = 5 } },
        };
        File.WriteAllText(Path.Combine(townDir, "buildings.json"),
            JsonSerializer.Serialize(buildings, JsonOptions));

        var props = new[]
        {
            new { id = "prp_001", meshAsset = "barrel.glb", rotation = 0f, scale = 1f, blocksWalkability = false,
                  placement = new { chunkX = 0, chunkY = 0, localTileX = 3, localTileY = 3 } },
        };
        File.WriteAllText(Path.Combine(townDir, "props.json"),
            JsonSerializer.Serialize(props, JsonOptions));

        var zones = new[]
        {
            new { id = "zone_market", name = "Market Square", chunkStartX = 0, chunkStartY = 0, chunkEndX = 0, chunkEndY = 0 },
        };
        File.WriteAllText(Path.Combine(townDir, "zones.json"),
            JsonSerializer.Serialize(zones, JsonOptions));

        return packDir;
    }

    private static void WriteManifest(string packDir)
    {
        var manifest = new { id = "test_pack", name = "Test Region", version = "0.1.0", description = "A test content pack" };
        File.WriteAllText(Path.Combine(packDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, JsonOptions));
    }
}
