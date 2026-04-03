using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class ChunkDataExtendedTests
{
    [Fact]
    public void GetWorldTileData_ReturnsCorrectData()
    {
        var chunk = new ChunkData(1, 0);
        chunk.Tiles.SetTileData(0, 5, TileDataFactory.Road());
        var data = chunk.GetWorldTileData(16, 5);
        Assert.Equal(TileType.Road, data.LegacyTileType);
        Assert.Equal(SurfaceType.Asphalt, data.Surface);
    }

    [Fact]
    public void GetWorldTileData_OutOfBounds_ReturnsEmpty()
    {
        var chunk = new ChunkData(0, 0);
        Assert.Equal(TileData.Empty, chunk.GetWorldTileData(20, 20));
    }

    [Fact]
    public void CreateDefault_HasGroundTileData()
    {
        var chunk = ChunkData.CreateDefault(1, 1);
        var data = chunk.Tiles.GetTileData(0, 0);
        Assert.Equal(TileType.Ground, data.LegacyTileType);
        Assert.Equal(SurfaceType.Dirt, data.Surface);
        Assert.True(data.IsWalkable);
    }
}
