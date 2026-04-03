using Oravey2.Core.World;
using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.Serialization;

public class MapFixtureTests
{
    private static string GetFixturesDir()
    {
        // Fixtures are copied to output directory via csproj Content item
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Fixtures", "Maps");
    }

    [Fact]
    public void LoadMinimal_CorrectDimensions()
    {
        var dir = Path.Combine(GetFixturesDir(), "test_minimal");
        var world = MapLoader.LoadWorldFull(dir);

        Assert.Equal(1, world.ChunksWide);
        Assert.Equal(1, world.ChunksHigh);

        var chunk = world.GetChunk(0, 0);
        Assert.NotNull(chunk);
        Assert.Equal(4, chunk!.Tiles.Width);
        Assert.Equal(4, chunk.Tiles.Height);
    }

    [Fact]
    public void LoadMinimal_TileAtKnownPosition_MatchesExpected()
    {
        var dir = Path.Combine(GetFixturesDir(), "test_minimal");
        var world = MapLoader.LoadWorldFull(dir);
        var chunk = world.GetChunk(0, 0)!;

        // (0,0) has surface=Concrete(2), structure=1, flags=0 → Wall
        Assert.Equal(TileType.Wall, chunk.Tiles.GetTile(0, 0));

        // (1,1) has surface=Dirt(0), flags=Walkable(1) → Ground
        Assert.Equal(TileType.Ground, chunk.Tiles.GetTile(1, 1));
        Assert.True(chunk.Tiles.IsWalkable(1, 1));
    }

    [Fact]
    public void LoadMinimal_WallCorners_NotWalkable()
    {
        var dir = Path.Combine(GetFixturesDir(), "test_minimal");
        var world = MapLoader.LoadWorldFull(dir);
        var chunk = world.GetChunk(0, 0)!;

        // Corners: (0,0), (3,0), (0,3), (3,3) all have structure=1, flags=0
        Assert.False(chunk.Tiles.IsWalkable(0, 0));
        Assert.False(chunk.Tiles.IsWalkable(3, 0));
        Assert.False(chunk.Tiles.IsWalkable(0, 3));
        Assert.False(chunk.Tiles.IsWalkable(3, 3));
    }

    [Fact]
    public void LoadTownFixture_KnownPosition_MatchesBuilder()
    {
        var dir = Path.Combine(GetFixturesDir(), "test_town");
        var world = MapLoader.LoadWorldFull(dir);
        var chunk = world.GetChunk(0, 0)!;

        // Border wall
        Assert.Equal(TileType.Wall, chunk.Tiles.GetTile(0, 0));
        // Road strip
        Assert.Equal(TileType.Road, chunk.Tiles.GetTile(10, 8));
        // Player spawn area is walkable
        Assert.True(chunk.Tiles.IsWalkable(12, 17));
        // Elder's house corner
        Assert.Equal(TileType.Wall, chunk.Tiles.GetTile(2, 2));
    }

    [Fact]
    public void LoadWastelandFixture_WestGate_IsWalkable()
    {
        var dir = Path.Combine(GetFixturesDir(), "test_wasteland");
        var world = MapLoader.LoadWorldFull(dir);
        var chunk = world.GetChunk(0, 0)!;

        // West gate at (0,17) and (0,18)
        Assert.True(chunk.Tiles.IsWalkable(0, 17));
        Assert.True(chunk.Tiles.IsWalkable(0, 18));
        // Water obstacle
        Assert.Equal(TileType.Water, chunk.Tiles.GetTile(13, 5));
    }
}
