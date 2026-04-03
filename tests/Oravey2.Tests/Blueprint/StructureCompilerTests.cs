using Oravey2.Core.World;
using Oravey2.Core.World.Blueprint;

namespace Oravey2.Tests.Blueprint;

public class StructureCompilerTests
{
    private static TileData[,] MakeFlatGrid(int w = 16, int h = 16)
    {
        var grid = new TileData[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                grid[x, y] = new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0);
        return grid;
    }

    [Fact]
    public void Building_FootprintTilesHaveStructureId()
    {
        var grid = MakeFlatGrid();
        var buildings = new[]
        {
            new BuildingBlueprint("b1", "Shop", "meshes/b1.glb", "Small", 3, 3, 2, 2, 1, 1f, null)
        };

        var (defs, _) = StructureCompiler.CompileStructures(grid, buildings, null);

        Assert.Single(defs);
        Assert.NotEqual(0, grid[3, 3].StructureId);
        Assert.NotEqual(0, grid[4, 4].StructureId);
    }

    [Fact]
    public void Building_FootprintTilesNonWalkable()
    {
        var grid = MakeFlatGrid();
        var buildings = new[]
        {
            new BuildingBlueprint("b1", "Shop", "meshes/b1.glb", "Small", 5, 5, 2, 2, 1, 1f, null)
        };

        StructureCompiler.CompileStructures(grid, buildings, null);

        Assert.False(grid[5, 5].IsWalkable);
        Assert.False(grid[6, 6].IsWalkable);
    }

    [Fact]
    public void NonBlockingProp_TileUnchanged()
    {
        var grid = MakeFlatGrid();
        var props = new[]
        {
            new PropBlueprint("p1", "meshes/barrel.glb", 8, 8, 0f, 1f, false, 0, 0)
        };

        StructureCompiler.CompileStructures(grid, null, props);

        Assert.True(grid[8, 8].IsWalkable);
    }

    [Fact]
    public void BlockingProp_TileNonWalkable()
    {
        var grid = MakeFlatGrid();
        var props = new[]
        {
            new PropBlueprint("p1", "meshes/car.glb", 8, 8, 0f, 1f, true, 2, 1)
        };

        StructureCompiler.CompileStructures(grid, null, props);

        Assert.False(grid[8, 8].IsWalkable);
        Assert.False(grid[9, 8].IsWalkable);
    }
}
