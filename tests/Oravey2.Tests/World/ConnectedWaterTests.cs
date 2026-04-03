using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class ConnectedWaterTests
{
    [Fact]
    public void SingleWaterTile_SetOf1()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, TileDataFactory.Ground());

        map.SetTileData(1, 1, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));

        var connected = WaterHelper.FindConnectedWater(map, 1, 1);
        Assert.Single(connected);
        Assert.Contains((1, 1), connected);
    }

    [Fact]
    public void WaterBlock3x3_SetOf9()
    {
        var map = new TileMapData(5, 5);
        for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++)
                map.SetTileData(x, y, TileDataFactory.Ground());

        for (int x = 1; x <= 3; x++)
            for (int y = 1; y <= 3; y++)
                map.SetTileData(x, y, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));

        var connected = WaterHelper.FindConnectedWater(map, 2, 2);
        Assert.Equal(9, connected.Count);
    }

    [Fact]
    public void LShapedRiver_CorrectConnectedSet()
    {
        // L shape: (0,0)-(1,0)-(2,0) then down (2,1)-(2,2)
        var map = new TileMapData(4, 4);
        for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
                map.SetTileData(x, y, TileDataFactory.Ground());

        map.SetTileData(0, 0, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));
        map.SetTileData(1, 0, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));
        map.SetTileData(2, 0, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));
        map.SetTileData(2, 1, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));
        map.SetTileData(2, 2, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));

        var connected = WaterHelper.FindConnectedWater(map, 0, 0);
        Assert.Equal(5, connected.Count);
        Assert.Contains((2, 2), connected);
    }

    [Fact]
    public void TwoSeparateWaterBodies_SeparateSets()
    {
        var map = new TileMapData(5, 1);
        for (int x = 0; x < 5; x++)
            map.SetTileData(x, 0, TileDataFactory.Ground());

        // Body A at (0,0)
        map.SetTileData(0, 0, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));
        // Body B at (4,0) — separated by dry tiles
        map.SetTileData(4, 0, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));

        var bodyA = WaterHelper.FindConnectedWater(map, 0, 0);
        var bodyB = WaterHelper.FindConnectedWater(map, 4, 0);

        Assert.Single(bodyA);
        Assert.Single(bodyB);
        Assert.DoesNotContain((4, 0), bodyA);
        Assert.DoesNotContain((0, 0), bodyB);
    }

    [Fact]
    public void DiagonalWater_NotConnected()
    {
        // Diagonal tiles should NOT connect (4-directional flood fill)
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, TileDataFactory.Ground());

        map.SetTileData(0, 0, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));
        map.SetTileData(1, 1, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));

        var connected = WaterHelper.FindConnectedWater(map, 0, 0);
        Assert.Single(connected);
        Assert.DoesNotContain((1, 1), connected);
    }

    [Fact]
    public void DryStart_ReturnsEmptySet()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, TileDataFactory.Ground());

        var connected = WaterHelper.FindConnectedWater(map, 1, 1);
        Assert.Empty(connected);
    }
}
