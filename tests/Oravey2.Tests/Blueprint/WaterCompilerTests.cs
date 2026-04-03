using Oravey2.Core.World;
using Oravey2.Core.World.Blueprint;

namespace Oravey2.Tests.Blueprint;

public class WaterCompilerTests
{
    private static TileData[,] MakeFlatGrid(int w = 32, int h = 32, byte elevation = 3)
    {
        var grid = new TileData[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                grid[x, y] = new TileData(SurfaceType.Dirt, elevation, 0, 0, TileFlags.Walkable, 0);
        return grid;
    }

    [Fact]
    public void River_TilesAlongPath_HaveWater()
    {
        var grid = MakeFlatGrid();
        var water = new WaterBlueprint(
            new[] { new RiverBlueprint("r1", new[] { new[] {5,0}, new[] {5,15} }, 1, 4, null) },
            null);

        WaterCompiler.CompileWater(grid, water);

        Assert.True(grid[5, 8].HasWater);
        Assert.Equal(4, grid[5, 8].WaterLevel);
    }

    [Fact]
    public void River_CorrectWidth()
    {
        var grid = MakeFlatGrid();
        var water = new WaterBlueprint(
            new[] { new RiverBlueprint("r1", new[] { new[] {10,0}, new[] {10,15} }, 3, 4, null) },
            null);

        WaterCompiler.CompileWater(grid, water);

        // Width 3 → center ± 1
        Assert.True(grid[9, 8].HasWater);
        Assert.True(grid[10, 8].HasWater);
        Assert.True(grid[11, 8].HasWater);
        // Tile outside width
        Assert.False(grid[8, 8].HasWater);
    }

    [Fact]
    public void Lake_CircularRegionHasWater()
    {
        var grid = MakeFlatGrid();
        var water = new WaterBlueprint(
            null,
            new[] { new LakeBlueprint("lake1", 16, 16, 4, 5, 3) });

        WaterCompiler.CompileWater(grid, water);

        Assert.True(grid[16, 16].HasWater);
        Assert.True(grid[16, 14].HasWater); // Within radius
    }

    [Fact]
    public void Lake_CenterDeeperThanEdge()
    {
        var grid = MakeFlatGrid();
        var water = new WaterBlueprint(
            null,
            new[] { new LakeBlueprint("lake1", 16, 16, 4, 5, 3) });

        WaterCompiler.CompileWater(grid, water);

        // Center should have lower terrain (more depth)
        Assert.True(grid[16, 16].HeightLevel <= grid[16, 13].HeightLevel);
    }

    [Fact]
    public void Bridge_TilesAreWalkable()
    {
        var grid = MakeFlatGrid();
        var water = new WaterBlueprint(
            new[]
            {
                new RiverBlueprint("r1",
                    new[] { new[] {10,0}, new[] {10,15} }, 1, 4,
                    new[] { new BridgeBlueprint(8, 5) }) // Bridge at path index 8
            },
            null);

        WaterCompiler.CompileWater(grid, water);

        // Bridge tile should be walkable
        Assert.True(grid[10, 8].IsWalkable);
    }

    [Fact]
    public void NoWater_OutsideDefinedRegions()
    {
        var grid = MakeFlatGrid();
        var water = new WaterBlueprint(
            new[] { new RiverBlueprint("r1", new[] { new[] {5,0}, new[] {5,15} }, 1, 4, null) },
            null);

        WaterCompiler.CompileWater(grid, water);

        // Far from river
        Assert.False(grid[20, 8].HasWater);
    }
}
