using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class WaterHelperTests
{
    // --- HasWater / GetDepth ---

    [Fact]
    public void DryTile_NoWater_DepthZero()
    {
        var tile = new TileData(SurfaceType.Dirt, HeightLevel: 1, WaterLevel: 0, 0, TileFlags.Walkable, 0);
        Assert.False(WaterHelper.HasWater(tile));
        Assert.Equal(0, WaterHelper.GetDepth(tile));
    }

    [Fact]
    public void ShallowWater_Depth1_SurfaceY()
    {
        // WaterLevel=2, HeightLevel=1 → depth=1, surfaceY=0.5
        var tile = TileDataFactory.Water(waterLevel: 2, terrainHeight: 1);
        Assert.True(WaterHelper.HasWater(tile));
        Assert.Equal(1, WaterHelper.GetDepth(tile));
        Assert.Equal(0.5f, WaterHelper.GetWaterSurfaceY(tile));
    }

    [Fact]
    public void DeepWater_Depth5_SurfaceY()
    {
        // WaterLevel=6, HeightLevel=1 → depth=5, surfaceY=1.5
        var tile = new TileData(SurfaceType.Mud, HeightLevel: 1, WaterLevel: 6, 0, TileFlags.None, 0);
        Assert.True(WaterHelper.HasWater(tile));
        Assert.Equal(5, WaterHelper.GetDepth(tile));
        Assert.Equal(1.5f, WaterHelper.GetWaterSurfaceY(tile));
    }

    [Fact]
    public void WaterLevelEqualsHeight_NoDryLand()
    {
        // WaterLevel == HeightLevel → NOT water (must be strictly greater)
        var tile = new TileData(SurfaceType.Dirt, HeightLevel: 3, WaterLevel: 3, 0, TileFlags.Walkable, 0);
        Assert.False(WaterHelper.HasWater(tile));
        Assert.Equal(0, WaterHelper.GetDepth(tile));
    }

    // --- IsShore ---

    [Fact]
    public void Shore_WaterNextToDry_IsShoreTrue()
    {
        var map = new TileMapData(3, 3);
        // All dry except center
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, TileDataFactory.Ground());

        // Center is water
        map.SetTileData(1, 1, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));

        Assert.True(WaterHelper.IsShore(map, 1, 1));
    }

    [Fact]
    public void InteriorWater_SurroundedByWater_NotShore()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));

        Assert.False(WaterHelper.IsShore(map, 1, 1));
    }

    [Fact]
    public void DryTile_IsShore_ReturnsFalse()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, TileDataFactory.Ground());

        Assert.False(WaterHelper.IsShore(map, 1, 1));
    }

    [Fact]
    public void EdgeWaterTile_IsShore_BoundsCountAsDry()
    {
        var map = new TileMapData(2, 2);
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                map.SetTileData(x, y, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));

        // (0,0) has out-of-bounds neighbors → counts as shore
        Assert.True(WaterHelper.IsShore(map, 0, 0));
    }

    // --- GetShoreDirections ---

    [Fact]
    public void ShoreDirections_DryNorthAndEast()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, TileDataFactory.Water(waterLevel: 3, terrainHeight: 1));

        // Make (1,0) North and (2,1) East dry
        map.SetTileData(1, 0, TileDataFactory.Ground());
        map.SetTileData(2, 1, TileDataFactory.Ground());

        var dirs = WaterHelper.GetShoreDirections(map, 1, 1);
        Assert.Contains(Direction.North, dirs);
        Assert.Contains(Direction.East, dirs);
        Assert.Equal(2, dirs.Length);
    }

    [Fact]
    public void ShoreDirections_DryTile_ReturnsEmpty()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, TileDataFactory.Ground());

        var dirs = WaterHelper.GetShoreDirections(map, 1, 1);
        Assert.Empty(dirs);
    }
}
