using Oravey2.Core.World;
using Xunit;

namespace Oravey2.Tests.World;

public class TileMapDataTests
{
    [Fact]
    public void Constructor_creates_correct_dimensions()
    {
        var map = new TileMapData(8, 10);
        Assert.Equal(8, map.Width);
        Assert.Equal(10, map.Height);
    }

    [Fact]
    public void Default_tiles_are_Empty()
    {
        var map = new TileMapData(4, 4);
        Assert.Equal(TileType.Empty, map.GetTile(0, 0));
        Assert.Equal(TileType.Empty, map.GetTile(3, 3));
    }

    [Fact]
    public void SetTile_and_GetTile_roundtrip()
    {
        var map = new TileMapData(4, 4);
        map.SetTile(2, 3, TileType.Road);
        Assert.Equal(TileType.Road, map.GetTile(2, 3));
    }

    [Fact]
    public void GetTile_out_of_bounds_returns_Empty()
    {
        var map = new TileMapData(4, 4);
        Assert.Equal(TileType.Empty, map.GetTile(-1, 0));
        Assert.Equal(TileType.Empty, map.GetTile(0, -1));
        Assert.Equal(TileType.Empty, map.GetTile(4, 0));
        Assert.Equal(TileType.Empty, map.GetTile(0, 4));
    }

    [Fact]
    public void SetTile_out_of_bounds_is_ignored()
    {
        var map = new TileMapData(4, 4);
        // Should not throw
        map.SetTile(-1, 0, TileType.Wall);
        map.SetTile(4, 0, TileType.Wall);
    }

    [Fact]
    public void CreateDefault_has_correct_dimensions()
    {
        var map = TileMapData.CreateDefault(16, 16);
        Assert.Equal(16, map.Width);
        Assert.Equal(16, map.Height);
    }

    [Fact]
    public void CreateDefault_has_border_walls()
    {
        var map = TileMapData.CreateDefault(16, 16);

        // Top and bottom rows
        for (int x = 0; x < 16; x++)
        {
            Assert.Equal(TileType.Wall, map.GetTile(x, 0));
            Assert.Equal(TileType.Wall, map.GetTile(x, 15));
        }

        // Left and right columns
        for (int y = 0; y < 16; y++)
        {
            Assert.Equal(TileType.Wall, map.GetTile(0, y));
            Assert.Equal(TileType.Wall, map.GetTile(15, y));
        }
    }

    [Fact]
    public void CreateDefault_has_roads()
    {
        var map = TileMapData.CreateDefault(16, 16);

        // Middle column and row should be roads (not on borders)
        Assert.Equal(TileType.Road, map.GetTile(8, 5));
        Assert.Equal(TileType.Road, map.GetTile(5, 8));
    }

    [Fact]
    public void CreateDefault_is_deterministic()
    {
        var map1 = TileMapData.CreateDefault(16, 16);
        var map2 = TileMapData.CreateDefault(16, 16);

        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
            Assert.Equal(map1.GetTile(x, y), map2.GetTile(x, y));
    }

    // --- IsWalkable ---

    [Theory]
    [InlineData(TileType.Ground, true)]
    [InlineData(TileType.Road, true)]
    [InlineData(TileType.Rubble, true)]
    [InlineData(TileType.Wall, false)]
    [InlineData(TileType.Water, false)]
    [InlineData(TileType.Empty, false)]
    public void IsWalkable_returns_expected(TileType type, bool expected)
    {
        var map = new TileMapData(4, 4);
        map.SetTile(1, 1, type);
        Assert.Equal(expected, map.IsWalkable(1, 1));
    }

    [Fact]
    public void IsWalkable_out_of_bounds_returns_false()
    {
        var map = new TileMapData(4, 4);
        Assert.False(map.IsWalkable(-1, 0));
        Assert.False(map.IsWalkable(4, 0));
    }

    // --- WorldToTile / TileToWorld ---

    [Fact]
    public void TileToWorld_matches_renderer_formula()
    {
        var map = new TileMapData(16, 16);
        // Tile (0,0) → center at (-7.5, -7.5)
        var (wx, wz) = map.TileToWorld(0, 0);
        Assert.Equal(-7.5f, wx);
        Assert.Equal(-7.5f, wz);

        // Tile (8,8) → center at (0.5, 0.5)
        var (wx2, wz2) = map.TileToWorld(8, 8);
        Assert.Equal(0.5f, wx2);
        Assert.Equal(0.5f, wz2);
    }

    [Fact]
    public void WorldToTile_roundtrips_with_TileToWorld()
    {
        var map = new TileMapData(16, 16);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            var (wx, wz) = map.TileToWorld(x, y);
            var (tx, ty) = map.WorldToTile(wx, wz);
            Assert.Equal(x, tx);
            Assert.Equal(y, ty);
        }
    }

    [Fact]
    public void WorldToTile_origin_maps_to_center_tiles()
    {
        var map = new TileMapData(16, 16);
        // World (0,0) should map to tile (8,8) — the center
        var (tx, ty) = map.WorldToTile(0f, 0f);
        Assert.Equal(8, tx);
        Assert.Equal(8, ty);
    }

    [Fact]
    public void WorldToTile_works_with_32x32_map()
    {
        var map = new TileMapData(32, 32);
        // World (0,0) → tile (16,16)
        var (tx, ty) = map.WorldToTile(0f, 0f);
        Assert.Equal(16, tx);
        Assert.Equal(16, ty);

        // Corner tile (0,0) → world (-15.5, -15.5)
        var (wx, wz) = map.TileToWorld(0, 0);
        Assert.Equal(-15.5f, wx);
        Assert.Equal(-15.5f, wz);
    }

    // --- IsWalkableAtWorld ---

    [Fact]
    public void IsWalkableAtWorld_returns_false_for_border_walls()
    {
        var map = TileMapData.CreateDefault(16, 16);
        // Tile (0,0) is a wall → world (-7.5, -7.5)
        Assert.False(map.IsWalkableAtWorld(-7.5f, -7.5f));
    }

    [Fact]
    public void IsWalkableAtWorld_returns_true_for_interior_ground()
    {
        var map = new TileMapData(4, 4);
        map.SetTile(2, 2, TileType.Ground);
        var (wx, wz) = map.TileToWorld(2, 2);
        Assert.True(map.IsWalkableAtWorld(wx, wz));
    }

    [Fact]
    public void IsWalkableAtWorld_returns_false_outside_map()
    {
        var map = new TileMapData(4, 4);
        Assert.False(map.IsWalkableAtWorld(100f, 100f));
    }
}
