using Oravey2.Core.World;
using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.World;

public class WaterMapFixtureTests
{
    private static string GetFixturesDir()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Fixtures", "Maps");
    }

    private static TileMapData LoadWaterMap()
    {
        var dir = Path.Combine(GetFixturesDir(), "test_water");
        var world = MapLoader.LoadWorldFull(dir);
        return world.GetChunk(0, 0)!.Tiles;
    }

    // --- River tiles have water ---

    [Fact]
    public void RiverTiles_HaveWater()
    {
        var map = LoadWaterMap();
        // River at columns 1-3, all rows
        for (int y = 0; y < 16; y++)
            for (int x = 1; x <= 3; x++)
                Assert.True(map.GetTileData(x, y).HasWater, $"River tile ({x},{y}) should have water");
    }

    // --- River connected via flood fill ---

    [Fact]
    public void RiverTiles_ConnectedViaFloodFill()
    {
        var map = LoadWaterMap();
        var connected = WaterHelper.FindConnectedWater(map, 2, 8);
        // River is 3 wide × 16 tall = 48 tiles
        Assert.Equal(48, connected.Count);
    }

    // --- Lake tiles connected via flood fill ---

    [Fact]
    public void LakeTiles_ConnectedViaFloodFill()
    {
        var map = LoadWaterMap();
        // Lake at x=10-13, y=0-3 minus dry island at (11,1) and (12,1)
        var connected = WaterHelper.FindConnectedWater(map, 10, 0);
        Assert.Equal(14, connected.Count); // 4×4=16 minus 2 dry island tiles
    }

    // --- Shore tiles correctly identified at river edges ---

    [Fact]
    public void RiverEdgeTiles_AreShore()
    {
        var map = LoadWaterMap();
        // Column 1 has dry neighbor at x=0 (West) → shore
        Assert.True(WaterHelper.IsShore(map, 1, 8));
        // Column 3 has dry neighbor at x=4 (East) → shore
        Assert.True(WaterHelper.IsShore(map, 3, 8));
    }

    [Fact]
    public void RiverInteriorTile_NotShore()
    {
        var map = LoadWaterMap();
        // Column 2 is surrounded by water (cols 1,3 and rows above/below)
        Assert.False(WaterHelper.IsShore(map, 2, 8));
    }

    // --- Shore tiles correctly identified at lake edges ---

    [Fact]
    public void LakeEdgeTile_IsShore()
    {
        var map = LoadWaterMap();
        // (10,0) has dry neighbor at x=9 (West) → shore
        Assert.True(WaterHelper.IsShore(map, 10, 0));
        // (13,3) at bottom-right corner has dry East and South neighbors
        Assert.True(WaterHelper.IsShore(map, 13, 3));
    }

    // --- Dry island is not water ---

    [Fact]
    public void DryIsland_NotWater()
    {
        var map = LoadWaterMap();
        Assert.False(map.GetTileData(11, 1).HasWater);
        Assert.False(map.GetTileData(12, 1).HasWater);
    }

    [Fact]
    public void DryIsland_IsWalkable()
    {
        var map = LoadWaterMap();
        Assert.True(map.GetTileData(11, 1).IsWalkable);
        Assert.True(map.GetTileData(12, 1).IsWalkable);
    }

    // --- All water tiles are non-walkable ---

    [Fact]
    public void AllWaterTiles_AreNotWalkable()
    {
        var map = LoadWaterMap();
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                var tile = map.GetTileData(x, y);
                if (tile.HasWater)
                    Assert.False(tile.IsWalkable, $"Water tile ({x},{y}) should not be walkable");
            }
    }

    // --- Lake and river are separate water bodies ---

    [Fact]
    public void LakeAndRiver_SeparateWaterBodies()
    {
        var map = LoadWaterMap();
        var river = WaterHelper.FindConnectedWater(map, 2, 8);
        var lake = WaterHelper.FindConnectedWater(map, 11, 0);

        // They should not overlap
        Assert.False(river.Overlaps(lake));
    }
}
