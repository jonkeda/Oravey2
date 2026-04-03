using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class WaterRenderDataTests
{
    [Fact]
    public void WaterTile_SurfaceY_MatchesWaterLevel()
    {
        var tile = TileDataFactory.Water(waterLevel: 4, terrainHeight: 1);
        Assert.True(WaterHelper.HasWater(tile));
        Assert.Equal(1.0f, WaterHelper.GetWaterSurfaceY(tile)); // 4 * 0.25
    }

    [Fact]
    public void DryTile_NoRenderData()
    {
        var tile = TileDataFactory.Ground();
        Assert.False(WaterHelper.HasWater(tile));
        Assert.Equal(0f, WaterHelper.GetWaterSurfaceY(tile)); // WaterLevel=0 → 0*0.25=0
    }

    [Fact]
    public void AdjacentWaterTiles_SameLevel_SameSurfaceY()
    {
        var map = new TileMapData(3, 1);
        map.SetTileData(0, 0, TileDataFactory.Water(waterLevel: 4, terrainHeight: 1));
        map.SetTileData(1, 0, TileDataFactory.Water(waterLevel: 4, terrainHeight: 1));
        map.SetTileData(2, 0, TileDataFactory.Water(waterLevel: 4, terrainHeight: 1));

        var y0 = WaterHelper.GetWaterSurfaceY(map.GetTileData(0, 0));
        var y1 = WaterHelper.GetWaterSurfaceY(map.GetTileData(1, 0));
        var y2 = WaterHelper.GetWaterSurfaceY(map.GetTileData(2, 0));

        Assert.Equal(y0, y1);
        Assert.Equal(y1, y2);
    }

    [Fact]
    public void DifferentWaterLevels_DifferentSurfaceY()
    {
        var shallow = TileDataFactory.Water(waterLevel: 2, terrainHeight: 1);
        var deep = TileDataFactory.Water(waterLevel: 6, terrainHeight: 1);

        Assert.NotEqual(
            WaterHelper.GetWaterSurfaceY(shallow),
            WaterHelper.GetWaterSurfaceY(deep));
    }
}
