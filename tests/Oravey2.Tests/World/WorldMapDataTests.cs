using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class WorldMapDataTests
{
    [Fact]
    public void Constructor_InvalidSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldMapData(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldMapData(5, 0));
    }

    [Fact]
    public void SetGet_Chunk_RoundTrip()
    {
        var world = new WorldMapData(4, 4);
        var chunk = new ChunkData(2, 3);
        world.SetChunk(2, 3, chunk);
        Assert.Same(chunk, world.GetChunk(2, 3));
    }

    [Fact]
    public void GetChunk_OutOfBounds_Null()
    {
        var world = new WorldMapData(4, 4);
        Assert.Null(world.GetChunk(10, 10));
    }

    [Fact]
    public void GetChunk_NegativeCoords_Null()
    {
        var world = new WorldMapData(4, 4);
        Assert.Null(world.GetChunk(-1, 0));
    }

    [Fact]
    public void InBounds_ValidCoords_True()
    {
        var world = new WorldMapData(4, 4);
        Assert.True(world.InBounds(3, 3));
    }

    [Fact]
    public void InBounds_OutOfRange_False()
    {
        var world = new WorldMapData(4, 4);
        Assert.False(world.InBounds(4, 0));
    }

    [Fact]
    public void TileToChunk_Conversion()
    {
        var (cx, cy) = WorldMapData.TileToChunk(33, 17);
        Assert.Equal(2, cx);
        Assert.Equal(1, cy);
    }

    [Fact]
    public void GetAllChunks_ReturnsNonNull()
    {
        var world = new WorldMapData(4, 4);
        world.SetChunk(0, 0, new ChunkData(0, 0));
        world.SetChunk(1, 1, new ChunkData(1, 1));
        Assert.Equal(2, world.GetAllChunks().Count());
    }
}
