using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class ShoreConfigTests
{
    [Fact]
    public void WaterWithDryNorth_OnlyNorthTrue()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));

        // Make North neighbor dry
        map.SetTileData(1, 0, TileDataFactory.Ground());

        var config = WaterHelper.GetShoreConfig(map, 1, 1);
        Assert.True(config.North);
        Assert.False(config.East);
        Assert.False(config.South);
        Assert.False(config.West);
        Assert.False(config.SouthEast);
        Assert.False(config.SouthWest);
    }

    [Fact]
    public void WaterInCorner_DryNorthEastAndNE()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));

        // Dry: North (1,0), East (2,1), NorthEast (2,0)
        map.SetTileData(1, 0, TileDataFactory.Ground());
        map.SetTileData(2, 1, TileDataFactory.Ground());
        map.SetTileData(2, 0, TileDataFactory.Ground());

        var config = WaterHelper.GetShoreConfig(map, 1, 1);
        Assert.True(config.North);
        Assert.True(config.East);
        Assert.True(config.NorthEast);
        Assert.False(config.South);
        Assert.False(config.West);
        Assert.False(config.SouthWest);
    }

    [Fact]
    public void InteriorWater_AllFalse()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));

        var config = WaterHelper.GetShoreConfig(map, 1, 1);
        Assert.False(config.North);
        Assert.False(config.East);
        Assert.False(config.South);
        Assert.False(config.West);
        Assert.False(config.NorthEast);
        Assert.False(config.SouthEast);
        Assert.False(config.SouthWest);
        Assert.False(config.NorthWest);
    }

    [Fact]
    public void DryTile_ReturnsDefault()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, TileDataFactory.Ground());

        var config = WaterHelper.GetShoreConfig(map, 1, 1);
        Assert.Equal(default(ShoreConfig), config);
    }
}
