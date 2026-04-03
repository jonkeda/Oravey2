using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class TileMapDataExtendedTests
{
    [Fact]
    public void SetTileData_GetTileData_RoundTrip()
    {
        var map = new TileMapData(4, 4);
        var data = new TileData(SurfaceType.Asphalt, 2, 0, 0, TileFlags.Walkable, 42);
        map.SetTileData(1, 2, data);
        Assert.Equal(data, map.GetTileData(1, 2));
    }

    [Fact]
    public void SetTile_Legacy_GetTileData_ReturnsMatchingData()
    {
        var map = new TileMapData(4, 4);
        map.SetTile(1, 1, TileType.Road);
        var data = map.GetTileData(1, 1);
        Assert.Equal(TileType.Road, data.LegacyTileType);
        Assert.Equal(SurfaceType.Asphalt, data.Surface);
        Assert.True(data.IsWalkable);
    }

    [Fact]
    public void SetTileData_Rich_GetTile_ReturnsCorrectLegacyType()
    {
        var map = new TileMapData(4, 4);
        var data = new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 10);
        map.SetTileData(2, 2, data);
        Assert.Equal(TileType.Rubble, map.GetTile(2, 2));
    }

    [Fact]
    public void IsWalkable_UsesTileDataFlag()
    {
        var map = new TileMapData(4, 4);
        // Set a tile that would be "Ground" legacy but without walkable flag
        var nonWalkable = new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.None, 0);
        map.SetTileData(1, 1, nonWalkable);
        Assert.False(map.IsWalkable(1, 1));

        // Set a walkable tile
        var walkable = new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0);
        map.SetTileData(1, 1, walkable);
        Assert.True(map.IsWalkable(1, 1));
    }

    [Fact]
    public void GetTileData_OutOfBounds_ReturnsEmpty()
    {
        var map = new TileMapData(4, 4);
        Assert.Equal(TileData.Empty, map.GetTileData(-1, 0));
        Assert.Equal(TileData.Empty, map.GetTileData(4, 0));
        Assert.Equal(TileData.Empty, map.GetTileData(0, -1));
        Assert.Equal(TileData.Empty, map.GetTileData(0, 4));
    }

    [Fact]
    public void SetTileData_OutOfBounds_IsIgnored()
    {
        var map = new TileMapData(4, 4);
        var data = TileDataFactory.Ground();
        // Should not throw
        map.SetTileData(-1, 0, data);
        map.SetTileData(4, 0, data);
    }

    [Fact]
    public void Tiles_ShimArray_StaysInSync()
    {
        var map = new TileMapData(4, 4);
        map.SetTileData(1, 1, TileDataFactory.Road());
        Assert.Equal(TileType.Road, map.Tiles[1, 1]);

        map.SetTile(2, 2, TileType.Wall);
        Assert.Equal(TileType.Wall, map.Tiles[2, 2]);
    }
}
