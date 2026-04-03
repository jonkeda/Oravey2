using Oravey2.Core.AI.Pathfinding;
using Oravey2.Core.World;

namespace Oravey2.Tests.AI;

public class TileGridPathfinderHeightTests
{
    private readonly TileGridPathfinder _pathfinder = new();

    private static TileMapData CreateFlatMap(int size = 10, byte height = 1)
    {
        var map = new TileMapData(size, size);
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.SetTileData(x, y, new TileData(SurfaceType.Dirt, height, 0, 0, TileFlags.Walkable, 0));
        return map;
    }

    [Fact]
    public void PathAvoids_CliffEdge()
    {
        var map = CreateFlatMap(10);
        // Create a cliff wall at x=5: height jumps from 1 to 10
        for (int y = 0; y < 10; y++)
            map.SetTileData(5, y, new TileData(SurfaceType.Dirt, 10, 0, 0, TileFlags.Walkable, 0));
        // Leave a gentle ramp at y=9: heights 1, 2, 3, 4, 5, 6, 7, 8, 9, 10
        for (int x = 0; x < 10; x++)
            map.SetTileData(x, 9, new TileData(SurfaceType.Dirt, (byte)(x + 1), 0, 0, TileFlags.Walkable, 0));

        var result = _pathfinder.FindPath(2, 2, 7, 2, map);
        Assert.True(result.Found);
        // Path must use the ramp at y=9 to get across
        Assert.True(result.Path.Any(p => p.Y == 9));
    }

    [Fact]
    public void PathPrefers_FlatOverSteep()
    {
        var map = CreateFlatMap(12);
        // Create steep column at x=5, y=3..8 (height 5 = delta 4 = steep from height 1)
        for (int y = 3; y <= 8; y++)
            map.SetTileData(5, y, new TileData(SurfaceType.Dirt, 5, 0, 0, TileFlags.Walkable, 0));
        // Flat bypass at y=1 and y=10
        // (already flat from CreateFlatMap)

        var result = _pathfinder.FindPath(2, 5, 8, 5, map);
        Assert.True(result.Found);
        // Path should prefer going around the steep section
        // Either through y<=2 or y>=9
        Assert.True(result.Path.Any(p => p.Y <= 2 || p.Y >= 9));
    }

    [Fact]
    public void PathGoesOver_GentleSlope()
    {
        var map = CreateFlatMap(10);
        // Gentle hill at x=5: height 2 (delta 1 from height 1)
        for (int y = 0; y < 10; y++)
            map.SetTileData(5, y, new TileData(SurfaceType.Dirt, 2, 0, 0, TileFlags.Walkable, 0));

        var result = _pathfinder.FindPath(2, 5, 8, 5, map);
        Assert.True(result.Found);
        // Should go through x=5 since gentle slope is walkable
        Assert.True(result.Path.Any(p => p.X == 5));
    }

    [Fact]
    public void SurroundedByCliffs_NoPath()
    {
        var map = CreateFlatMap(10);
        // Surround tile (5,5) with cliff tiles
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    map.SetTileData(5 + dx, 5 + dy,
                        new TileData(SurfaceType.Dirt, 20, 0, 0, TileFlags.Walkable, 0));

        var result = _pathfinder.FindPath(1, 1, 5, 5, map);
        Assert.False(result.Found);
    }

    [Fact]
    public void ExistingFlatMaps_StillWorkIdentically()
    {
        // All existing maps use height=1 from factory methods, so delta is always 0
        var map = CreateFlatMap(10);
        var result = _pathfinder.FindPath(0, 0, 9, 9, map);
        Assert.True(result.Found);
        // Should find diagonal path
        Assert.True(result.Path.Count <= 11);
    }
}
