using Oravey2.Core.AI.Pathfinding;
using Oravey2.Core.World;

namespace Oravey2.Tests.AI;

public class TileGridPathfinderTests
{
    private readonly TileGridPathfinder _pathfinder = new();

    private static TileMapData CreateOpenMap(int size = 10)
    {
        var map = new TileMapData(size, size);
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.SetTile(x, y, TileType.Ground);
        return map;
    }

    [Fact]
    public void StraightPath_Found()
    {
        var map = CreateOpenMap();
        var result = _pathfinder.FindPath(1, 1, 5, 1, map);
        Assert.True(result.Found);
        Assert.Equal((1, 1), result.Path[0]);
        Assert.Equal((5, 1), result.Path[^1]);
        Assert.True(result.Path.Count >= 2);
    }

    [Fact]
    public void PathAroundWall()
    {
        var map = CreateOpenMap();
        // Place a wall blocking direct path
        for (int y = 0; y < 8; y++)
            map.SetTile(5, y, TileType.Wall);

        var result = _pathfinder.FindPath(3, 3, 7, 3, map);
        Assert.True(result.Found);
        // Path must go around the wall
        Assert.True(result.Path.Count > 4);
    }

    [Fact]
    public void NoPath_Surrounded()
    {
        var map = CreateOpenMap();
        // Surround the goal with walls
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    map.SetTile(5 + dx, 5 + dy, TileType.Wall);

        var result = _pathfinder.FindPath(1, 1, 5, 5, map);
        Assert.False(result.Found);
    }

    [Fact]
    public void StartOnWall_NotFound()
    {
        var map = CreateOpenMap();
        map.SetTile(1, 1, TileType.Wall);
        var result = _pathfinder.FindPath(1, 1, 5, 5, map);
        Assert.False(result.Found);
    }

    [Fact]
    public void GoalOnWall_NotFound()
    {
        var map = CreateOpenMap();
        map.SetTile(5, 5, TileType.Wall);
        var result = _pathfinder.FindPath(1, 1, 5, 5, map);
        Assert.False(result.Found);
    }

    [Fact]
    public void DiagonalPath_Allowed()
    {
        var map = CreateOpenMap();
        var result = _pathfinder.FindPath(0, 0, 5, 5, map);
        Assert.True(result.Found);
        // Pure diagonal should be ~6 steps (0,0 to 5,5)
        Assert.True(result.Path.Count <= 7);
    }

    [Fact]
    public void RubbleSlowerThanGround()
    {
        var map = CreateOpenMap(12);
        // Create two corridors: top is ground, bottom is rubble
        for (int x = 1; x < 11; x++)
        {
            map.SetTile(x, 3, TileType.Ground); // top corridor
            map.SetTile(x, 7, TileType.Rubble); // bottom corridor
        }
        // Block middle so path must choose top or bottom
        for (int y = 4; y <= 6; y++)
            for (int x = 1; x < 11; x++)
                map.SetTile(x, y, TileType.Wall);

        var result = _pathfinder.FindPath(1, 3, 10, 3, map);
        Assert.True(result.Found);
        // Path should stay on ground corridor (y=3), not detour through rubble
        Assert.True(result.Path.All(p => p.Y <= 4));
    }

    [Fact]
    public void CornerCutting_Blocked()
    {
        var map = CreateOpenMap();
        // Wall at (3,3), ground around it. Diagonal from (2,2) to (4,4) must not cut corner
        map.SetTile(3, 3, TileType.Wall);
        map.SetTile(3, 2, TileType.Wall); // block the cardinal adjacent

        var result = _pathfinder.FindPath(2, 2, 4, 4, map);
        Assert.True(result.Found);
        // Should not step on (3,3)
        Assert.DoesNotContain((3, 3), result.Path);
    }

    [Fact]
    public void WaterNotWalkable()
    {
        var map = CreateOpenMap();
        // Water row blocking direct path
        for (int x = 0; x < 10; x++)
            map.SetTile(x, 5, TileType.Water);
        // Leave a gap
        map.SetTile(8, 5, TileType.Ground);

        var result = _pathfinder.FindPath(4, 3, 4, 7, map);
        Assert.True(result.Found);
        // Must go through the gap at x=8
        Assert.True(result.Path.Any(p => p.X == 8 && p.Y == 5));
    }

    [Fact]
    public void StartEqualsGoal()
    {
        var map = CreateOpenMap();
        var result = _pathfinder.FindPath(3, 3, 3, 3, map);
        Assert.True(result.Found);
        Assert.Single(result.Path);
        Assert.Equal((3, 3), result.Path[0]);
    }

    [Fact]
    public void LargeMap_Performance()
    {
        var map = CreateOpenMap(100);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = _pathfinder.FindPath(0, 0, 99, 99, map);
        sw.Stop();
        Assert.True(result.Found);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Took {sw.ElapsedMilliseconds}ms");
    }
}
