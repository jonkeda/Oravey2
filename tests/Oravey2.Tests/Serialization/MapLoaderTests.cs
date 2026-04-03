using System.Text.Json;
using Oravey2.Core.World;
using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.Serialization;

public class MapLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public MapLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"oravey2_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteWorldJson(int chunksWide, int chunksHigh)
    {
        var worldJson = new WorldJson(chunksWide, chunksHigh, 1.0f,
            new PlayerStartJson(0, 0, 0, 0), null);
        var json = JsonSerializer.Serialize(worldJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        File.WriteAllText(Path.Combine(_tempDir, "world.json"), json);
    }

    private void WriteChunk(ChunkData chunk)
    {
        var chunksDir = Path.Combine(_tempDir, "chunks");
        ChunkSerializer.SaveChunk(chunk, chunksDir);
    }

    [Fact]
    public void LoadWorld_ReadsCorrectDimensions()
    {
        WriteWorldJson(3, 2);

        var world = MapLoader.LoadWorld(_tempDir);

        Assert.Equal(3, world.ChunksWide);
        Assert.Equal(2, world.ChunksHigh);
    }

    [Fact]
    public void LoadChunk_ReadsCorrectTileData()
    {
        var chunk = ChunkData.CreateDefault(1, 0);
        chunk.Tiles.SetTileData(5, 5, TileDataFactory.Road());
        WriteChunk(chunk);

        var loaded = MapLoader.LoadChunk(_tempDir, 1, 0);

        Assert.NotNull(loaded);
        Assert.Equal(TileType.Road, loaded!.Tiles.GetTile(5, 5));
    }

    [Fact]
    public void LoadChunk_NonExistent_ReturnsNull()
    {
        var result = MapLoader.LoadChunk(_tempDir, 99, 99);
        Assert.Null(result);
    }

    [Fact]
    public void LoadWorldFull_PopulatesAllChunks()
    {
        WriteWorldJson(2, 1);
        WriteChunk(ChunkData.CreateDefault(0, 0));
        WriteChunk(ChunkData.CreateDefault(1, 0));

        var world = MapLoader.LoadWorldFull(_tempDir);

        Assert.Equal(2, world.ChunksWide);
        Assert.Equal(1, world.ChunksHigh);
        Assert.NotNull(world.GetChunk(0, 0));
        Assert.NotNull(world.GetChunk(1, 0));
    }

    [Fact]
    public void LoadWorldFull_MissingChunk_LeavesNull()
    {
        WriteWorldJson(2, 1);
        WriteChunk(ChunkData.CreateDefault(0, 0));
        // chunk 1,0 intentionally missing

        var world = MapLoader.LoadWorldFull(_tempDir);

        Assert.NotNull(world.GetChunk(0, 0));
        Assert.Null(world.GetChunk(1, 0));
    }

    [Fact]
    public void LoadWorld_InvalidDirectory_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() =>
            MapLoader.LoadWorld(Path.Combine(_tempDir, "nonexistent")));
    }
}
