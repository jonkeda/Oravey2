using Oravey2.Core.UI.Stride;
using Stride.Core.Mathematics;

namespace Oravey2.Tests.UI;

public class RegionMapOverlayTests
{
    [Theory]
    [InlineData(0, 0, 50f, 37.5f)]    // chunk (0,0) center: 0.5/8 * 800, 0.5/8 * 600
    [InlineData(1, 1, 150f, 112.5f)]  // chunk (1,1) center: 1.5/8 * 800, 1.5/8 * 600
    [InlineData(3, 3, 350f, 262.5f)]  // chunk (3,3) center: 3.5/8 * 800, 3.5/8 * 600
    public void GridToMap_MapsChunkCoordsToCanvasCenter(int gridX, int gridY, float expectedX, float expectedY)
    {
        // 64×64 tiles = 4×4 chunks of 16. Canvas is 800×600 per the script constants.
        // Each chunk cell = 800/4 = 200 wide, 600/4 = 150 high
        // Center of (0,0) = (100, 75), etc.
        // With 8×8 chunks: 800/8 = 100 wide, 600/8 = 75 high → center of (0,0) = (50, 37.5)
        // Let me recalculate for 128×128 tiles = 8 chunks
        var script = CreateScript(worldTilesWide: 128, worldTilesHigh: 128);

        var (mapX, mapY) = script.GridToMap(gridX, gridY);

        // 8 chunks wide → each cell = 100px wide. chunk(0) center at 0.5/8 * 800 = 50
        Assert.Equal(expectedX, mapX, 0.01f);
        Assert.Equal(expectedY, mapY, 0.01f);
    }

    [Fact]
    public void GridToMap_ZeroWorld_ReturnsZero()
    {
        var script = CreateScript(worldTilesWide: 0, worldTilesHigh: 0);

        var (mapX, mapY) = script.GridToMap(0, 0);

        Assert.Equal(0f, mapX);
        Assert.Equal(0f, mapY);
    }

    [Theory]
    [InlineData(0f, 0f, 400f, 300f)]       // world center → canvas center
    [InlineData(-64f, -64f, 0f, 0f)]       // bottom-left corner → canvas (0,0)
    [InlineData(64f, 64f, 800f, 600f)]     // top-right corner → canvas (800,600)
    public void WorldToMap_MapsWorldCoordsToCanvas(float worldX, float worldZ, float expectedX, float expectedY)
    {
        // 128 tiles * 1.0 tileSize = 128 world units. Half = 64.
        var script = CreateScript(worldTilesWide: 128, worldTilesHigh: 128, tileSize: 1f);

        var (mapX, mapY) = script.WorldToMap(worldX, worldZ);

        Assert.Equal(expectedX, mapX, 0.01f);
        Assert.Equal(expectedY, mapY, 0.01f);
    }

    [Fact]
    public void WorldToMap_ZeroDimensions_ReturnsZero()
    {
        var script = CreateScript(worldTilesWide: 0, worldTilesHigh: 0);

        var (mapX, mapY) = script.WorldToMap(10f, 10f);

        Assert.Equal(0f, mapX);
        Assert.Equal(0f, mapY);
    }

    [Theory]
    [InlineData("metropolis", 18)]
    [InlineData("city", 14)]
    [InlineData("town", 10)]
    [InlineData("village", 8)]
    [InlineData("hamlet", 6)]
    [InlineData("unknown", 8)]
    public void GetMarkerStyle_ReturnsCorrectSize(string poiType, float expectedSize)
    {
        var (markerSize, _, _) = RegionMapOverlayScript.GetMarkerStyle(poiType);

        Assert.Equal(expectedSize, markerSize);
    }

    [Fact]
    public void GetMarkerStyle_CaseInsensitive()
    {
        var (size1, _, _) = RegionMapOverlayScript.GetMarkerStyle("CITY");
        var (size2, _, _) = RegionMapOverlayScript.GetMarkerStyle("city");
        var (size3, _, _) = RegionMapOverlayScript.GetMarkerStyle("City");

        Assert.Equal(size1, size2);
        Assert.Equal(size2, size3);
    }

    private static RegionMapOverlayScript CreateScript(
        int worldTilesWide = 128, int worldTilesHigh = 128, float tileSize = 1f)
    {
        return new RegionMapOverlayScript
        {
            WorldTilesWide = worldTilesWide,
            WorldTilesHigh = worldTilesHigh,
            TileSize = tileSize,
        };
    }
}
