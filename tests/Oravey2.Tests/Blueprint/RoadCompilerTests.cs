using Oravey2.Core.World;
using Oravey2.Core.World.Blueprint;

namespace Oravey2.Tests.Blueprint;

public class RoadCompilerTests
{
    private static TileData[,] MakeFlatGrid(int w = 16, int h = 16, byte elevation = 1)
    {
        var grid = new TileData[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                grid[x, y] = new TileData(SurfaceType.Dirt, elevation, 0, 0, TileFlags.Walkable, 0);
        return grid;
    }

    [Fact]
    public void StraightRoad_Width3_Correct()
    {
        var grid = MakeFlatGrid();
        var roads = new[]
        {
            new RoadBlueprint("r1", new[] { new[] {0,8}, new[] {15,8} }, 3, "Asphalt", 1f)
        };

        RoadCompiler.CompileRoads(grid, roads);

        // Center and ±1 row should be road
        Assert.Equal(SurfaceType.Asphalt, grid[7, 8].Surface);
        Assert.Equal(SurfaceType.Asphalt, grid[7, 7].Surface);
        Assert.Equal(SurfaceType.Asphalt, grid[7, 9].Surface);
        // Two rows away should not be changed
        Assert.Equal(SurfaceType.Dirt, grid[7, 6].Surface);
    }

    [Fact]
    public void RoadTiles_AreWalkable()
    {
        var grid = MakeFlatGrid();
        // Make all tiles non-walkable first
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                grid[x, y] = new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.None, 0);

        var roads = new[]
        {
            new RoadBlueprint("r1", new[] { new[] {0,8}, new[] {15,8} }, 1, "Asphalt", 1f)
        };

        RoadCompiler.CompileRoads(grid, roads);

        Assert.True(grid[5, 8].IsWalkable);
    }

    [Fact]
    public void RoadSetsCorrectSurface()
    {
        var grid = MakeFlatGrid();
        var roads = new[]
        {
            new RoadBlueprint("r1", new[] { new[] {0,5}, new[] {15,5} }, 1, "Concrete", 1f)
        };

        RoadCompiler.CompileRoads(grid, roads);

        Assert.Equal(SurfaceType.Concrete, grid[8, 5].Surface);
    }

    [Fact]
    public void RoadCondition03_SomeTilesRubble()
    {
        var grid = MakeFlatGrid(32, 1);
        var roads = new[]
        {
            new RoadBlueprint("r1", new[] { new[] {0,0}, new[] {31,0} }, 1, "Asphalt", 0.3f)
        };

        RoadCompiler.CompileRoads(grid, roads);

        int asphaltCount = 0, rubbleCount = 0;
        for (int x = 0; x < 32; x++)
        {
            if (grid[x, 0].Surface == SurfaceType.Asphalt) asphaltCount++;
            if (grid[x, 0].Surface == SurfaceType.Rock) rubbleCount++;
        }

        Assert.True(rubbleCount > 0, "Some tiles should be rubble at condition 0.3");
        Assert.True(asphaltCount > 0, "Some tiles should remain asphalt at condition 0.3");
    }

    [Fact]
    public void RoadCondition10_AllTilesRoadSurface()
    {
        var grid = MakeFlatGrid();
        var roads = new[]
        {
            new RoadBlueprint("r1", new[] { new[] {0,8}, new[] {15,8} }, 1, "Asphalt", 1.0f)
        };

        RoadCompiler.CompileRoads(grid, roads);

        for (int x = 0; x < 16; x++)
            Assert.Equal(SurfaceType.Asphalt, grid[x, 8].Surface);
    }
}
