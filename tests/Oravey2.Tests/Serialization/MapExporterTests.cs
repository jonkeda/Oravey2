using Oravey2.Core.World;
using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.Serialization;

public class MapExporterTests : IDisposable
{
    private readonly string _tempDir;

    public MapExporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"oravey2_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void ExportTownMap_FilesExist()
    {
        var townMap = TownMapBuilder.CreateTownMap();
        var world = new WorldMapData(1, 1);
        world.SetChunk(0, 0, new ChunkData(0, 0, townMap));

        MapExporter.ExportWorld(world, _tempDir);

        Assert.True(File.Exists(Path.Combine(_tempDir, "world.json")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "chunks", "0_0.json")));
    }

    [Fact]
    public void ExportTownMap_LoadRoundTrip_LayoutMatches()
    {
        var townMap = TownMapBuilder.CreateTownMap();
        var world = new WorldMapData(1, 1);
        world.SetChunk(0, 0, new ChunkData(0, 0, townMap));

        MapExporter.ExportWorld(world, _tempDir);
        var loaded = MapLoader.LoadWorldFull(_tempDir);
        var chunk = loaded.GetChunk(0, 0)!;

        // Verify key positions from TownMapBuilder
        Assert.Equal(TileType.Wall, chunk.Tiles.GetTile(0, 0));       // border
        Assert.Equal(TileType.Road, chunk.Tiles.GetTile(10, 8));      // road strip
        Assert.Equal(TileType.Ground, chunk.Tiles.GetTile(12, 17));   // player spawn
        Assert.Equal(TileType.Wall, chunk.Tiles.GetTile(2, 2));       // elder's house corner
        Assert.True(chunk.Tiles.IsWalkable(30, 17));                   // east gate
    }

    [Fact]
    public void ExportWastelandMap_LoadRoundTrip_GatesAtSamePositions()
    {
        var wastelandMap = WastelandMapBuilder.CreateWastelandMap();
        var world = new WorldMapData(1, 1);
        world.SetChunk(0, 0, new ChunkData(0, 0, wastelandMap));

        MapExporter.ExportWorld(world, _tempDir);
        var loaded = MapLoader.LoadWorldFull(_tempDir);
        var chunk = loaded.GetChunk(0, 0)!;

        // West gate at (0,17) and (0,18) should be walkable
        Assert.True(chunk.Tiles.IsWalkable(0, 17));
        Assert.True(chunk.Tiles.IsWalkable(0, 18));
        // Water obstacle
        Assert.Equal(TileType.Water, chunk.Tiles.GetTile(13, 5));
        // Road strip
        Assert.Equal(TileType.Road, chunk.Tiles.GetTile(4, 10));
    }

    [Fact]
    public void ExportBuilderMap_CreatesChunkFile()
    {
        var map = TileMapData.CreateDefault(16, 16);
        MapExporter.ExportBuilderMap(map, 5, 3, _tempDir);

        Assert.True(File.Exists(Path.Combine(_tempDir, "chunks", "5_3.json")));
    }
}
