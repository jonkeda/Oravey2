using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.Serialization;

public class WorldJsonFormatTests
{
    [Fact]
    public void WorldJson_Instantiates_WithValidData()
    {
        var start = new PlayerStartJson(0, 0, 12, 17);
        var world = new WorldJson(2, 2, 1.0f, start, null);

        Assert.Equal(2, world.ChunksWide);
        Assert.Equal(2, world.ChunksHigh);
        Assert.Equal(1.0f, world.TileSize);
        Assert.Null(world.DefaultWeather);
    }

    [Fact]
    public void PlayerStartJson_Instantiates_WithValues()
    {
        var start = new PlayerStartJson(1, 0, 5, 10);

        Assert.Equal(1, start.ChunkX);
        Assert.Equal(0, start.ChunkY);
        Assert.Equal(5, start.LocalTileX);
        Assert.Equal(10, start.LocalTileY);
    }
}
