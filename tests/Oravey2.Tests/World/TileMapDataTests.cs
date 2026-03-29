using Oravey2.Core.World;
using Xunit;

namespace Oravey2.Tests.World;

public class TileMapDataTests
{
    [Fact]
    public void Constructor_creates_correct_dimensions()
    {
        var map = new TileMapData(8, 10);
        Assert.Equal(8, map.Width);
        Assert.Equal(10, map.Height);
    }

    [Fact]
    public void Default_tiles_are_Empty()
    {
        var map = new TileMapData(4, 4);
        Assert.Equal(TileType.Empty, map.GetTile(0, 0));
        Assert.Equal(TileType.Empty, map.GetTile(3, 3));
    }

    [Fact]
    public void SetTile_and_GetTile_roundtrip()
    {
        var map = new TileMapData(4, 4);
        map.SetTile(2, 3, TileType.Road);
        Assert.Equal(TileType.Road, map.GetTile(2, 3));
    }

    [Fact]
    public void GetTile_out_of_bounds_returns_Empty()
    {
        var map = new TileMapData(4, 4);
        Assert.Equal(TileType.Empty, map.GetTile(-1, 0));
        Assert.Equal(TileType.Empty, map.GetTile(0, -1));
        Assert.Equal(TileType.Empty, map.GetTile(4, 0));
        Assert.Equal(TileType.Empty, map.GetTile(0, 4));
    }

    [Fact]
    public void SetTile_out_of_bounds_is_ignored()
    {
        var map = new TileMapData(4, 4);
        // Should not throw
        map.SetTile(-1, 0, TileType.Wall);
        map.SetTile(4, 0, TileType.Wall);
    }

    [Fact]
    public void CreateDefault_has_correct_dimensions()
    {
        var map = TileMapData.CreateDefault(16, 16);
        Assert.Equal(16, map.Width);
        Assert.Equal(16, map.Height);
    }

    [Fact]
    public void CreateDefault_has_border_walls()
    {
        var map = TileMapData.CreateDefault(16, 16);

        // Top and bottom rows
        for (int x = 0; x < 16; x++)
        {
            Assert.Equal(TileType.Wall, map.GetTile(x, 0));
            Assert.Equal(TileType.Wall, map.GetTile(x, 15));
        }

        // Left and right columns
        for (int y = 0; y < 16; y++)
        {
            Assert.Equal(TileType.Wall, map.GetTile(0, y));
            Assert.Equal(TileType.Wall, map.GetTile(15, y));
        }
    }

    [Fact]
    public void CreateDefault_has_roads()
    {
        var map = TileMapData.CreateDefault(16, 16);

        // Middle column and row should be roads (not on borders)
        Assert.Equal(TileType.Road, map.GetTile(8, 5));
        Assert.Equal(TileType.Road, map.GetTile(5, 8));
    }

    [Fact]
    public void CreateDefault_is_deterministic()
    {
        var map1 = TileMapData.CreateDefault(16, 16);
        var map2 = TileMapData.CreateDefault(16, 16);

        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
            Assert.Equal(map1.GetTile(x, y), map2.GetTile(x, y));
    }
}
