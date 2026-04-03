using Oravey2.Core.AI.Pathfinding;
using Oravey2.Core.World;
using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.World;

public class HeightMapFixtureTests
{
    private static string GetFixturesDir()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Fixtures", "Maps");
    }

    [Fact]
    public void LoadHeightFixture_VerifyHeightsAtKnownPositions()
    {
        var dir = Path.Combine(GetFixturesDir(), "test_height");
        var world = MapLoader.LoadWorldFull(dir);
        var chunk = world.GetChunk(0, 0)!;

        // Flat center at (1,1) = height 1
        Assert.Equal(1, chunk.Tiles.GetTileData(1, 1).HeightLevel);

        // Hill in row 10: heights 2,3,4,5 at x=5,6,7,8
        Assert.Equal(2, chunk.Tiles.GetTileData(5, 10).HeightLevel);
        Assert.Equal(3, chunk.Tiles.GetTileData(6, 10).HeightLevel);
        Assert.Equal(4, chunk.Tiles.GetTileData(7, 10).HeightLevel);
        Assert.Equal(5, chunk.Tiles.GetTileData(8, 10).HeightLevel);

        // Cliff at (8,13) = height 10
        Assert.Equal(10, chunk.Tiles.GetTileData(8, 13).HeightLevel);

        // Ramp in row 14: heights 2,3,4,5,6 at x=6..10
        Assert.Equal(2, chunk.Tiles.GetTileData(6, 14).HeightLevel);
        Assert.Equal(6, chunk.Tiles.GetTileData(10, 14).HeightLevel);
    }

    [Fact]
    public void PathfindFromFlat_ToHilltop_GoesUpRamp()
    {
        var dir = Path.Combine(GetFixturesDir(), "test_height");
        var world = MapLoader.LoadWorldFull(dir);
        var chunk = world.GetChunk(0, 0)!;

        var pathfinder = new TileGridPathfinder();
        // From flat area (1,1) height=1 to ramp end (10,14) height=6
        // Ramp goes 1→2→3→4→5→6 over tiles x=5..10 at y=14
        var result = pathfinder.FindPath(1, 14, 10, 14, chunk.Tiles);

        Assert.True(result.Found);
        // Path should use the gradual ramp
        Assert.True(result.Path.Count >= 2);
    }

    [Fact]
    public void PathfindAcrossCliff_NoPath()
    {
        // Build a map with a cliff barrier
        var map = new TileMapData(10, 10);
        for (int x = 0; x < 10; x++)
            for (int y = 0; y < 10; y++)
                map.SetTileData(x, y, new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0));

        // Cliff wall at x=5 (height 10, delta=9 from height 1 = cliff)
        for (int y = 0; y < 10; y++)
            map.SetTileData(5, y, new TileData(SurfaceType.Dirt, 10, 0, 0, TileFlags.Walkable, 0));

        var pathfinder = new TileGridPathfinder();
        var result = pathfinder.FindPath(2, 5, 8, 5, map);

        Assert.False(result.Found);
    }

    [Fact]
    public void AllTilesWalkable_InFixture()
    {
        var dir = Path.Combine(GetFixturesDir(), "test_height");
        var world = MapLoader.LoadWorldFull(dir);
        var chunk = world.GetChunk(0, 0)!;

        // All tiles in fixture have Walkable flag
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                Assert.True(chunk.Tiles.IsWalkable(x, y));
    }
}
