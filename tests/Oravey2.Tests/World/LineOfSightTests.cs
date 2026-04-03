using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class LineOfSightTests
{
    private static TileMapData CreateFlatMap(int size = 10, byte height = 1)
    {
        var map = new TileMapData(size, size);
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.SetTileData(x, y, new TileData(SurfaceType.Dirt, height, 0, 0, TileFlags.Walkable, 0));
        return map;
    }

    [Fact]
    public void FlatTerrain_NoObstacles_AlwaysVisible()
    {
        var map = CreateFlatMap();
        Assert.True(HeightHelper.HasLineOfSight(map, 0, 0, 9, 9));
        Assert.True(HeightHelper.HasLineOfSight(map, 0, 0, 9, 0));
        Assert.True(HeightHelper.HasLineOfSight(map, 5, 5, 5, 9));
    }

    [Fact]
    public void HighUnit_SeesOverLowerWall()
    {
        var map = CreateFlatMap();
        // Observer at height 10
        map.SetTileData(0, 0, new TileData(SurfaceType.Dirt, 10, 0, 0, TileFlags.Walkable, 0));
        // Wall at height 5 in the middle
        map.SetTileData(5, 0, new TileData(SurfaceType.Dirt, 5, 0, 0, TileFlags.Walkable, 0));
        // Target at height 1
        map.SetTileData(9, 0, new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0));

        Assert.True(HeightHelper.HasLineOfSight(map, 0, 0, 9, 0));
    }

    [Fact]
    public void LowUnit_CannotSeePastTallerWall()
    {
        var map = CreateFlatMap();
        // Observer at height 3
        map.SetTileData(0, 0, new TileData(SurfaceType.Dirt, 3, 0, 0, TileFlags.Walkable, 0));
        // Wall at height 5 in the middle
        map.SetTileData(5, 0, new TileData(SurfaceType.Dirt, 5, 0, 0, TileFlags.Walkable, 0));
        // Target at height 1
        map.SetTileData(9, 0, new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0));

        Assert.False(HeightHelper.HasLineOfSight(map, 0, 0, 9, 0));
    }

    [Fact]
    public void DiagonalLOS_ThroughMultipleTiles()
    {
        var map = CreateFlatMap();
        // Observer at height 10
        map.SetTileData(0, 0, new TileData(SurfaceType.Dirt, 10, 0, 0, TileFlags.Walkable, 0));
        // Tall obstacles along diagonal  
        map.SetTileData(3, 3, new TileData(SurfaceType.Dirt, 15, 0, 0, TileFlags.Walkable, 0));

        Assert.False(HeightHelper.HasLineOfSight(map, 0, 0, 6, 6));
    }

    [Fact]
    public void AdjacentTiles_AlwaysVisible()
    {
        var map = CreateFlatMap();
        // Even with different heights, adjacent tiles have no intermediate tiles
        map.SetTileData(0, 0, new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0));
        map.SetTileData(1, 0, new TileData(SurfaceType.Dirt, 20, 0, 0, TileFlags.Walkable, 0));

        Assert.True(HeightHelper.HasLineOfSight(map, 0, 0, 1, 0));
    }
}
