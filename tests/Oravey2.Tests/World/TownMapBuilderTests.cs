using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class TownMapBuilderTests
{
    private readonly TileMapData _map = TownMapBuilder.CreateTownMap();

    [Fact]
    public void TownMap_Is32x32()
    {
        Assert.Equal(32, _map.Width);
        Assert.Equal(32, _map.Height);
    }

    [Fact]
    public void TownMap_Border_IsWall()
    {
        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(TileType.Wall, _map.GetTile(i, 0));   // top row
            Assert.Equal(TileType.Wall, _map.GetTile(i, 31));  // bottom row
            Assert.Equal(TileType.Wall, _map.GetTile(0, i));   // left col
            Assert.Equal(TileType.Wall, _map.GetTile(31, i));  // right col
        }
    }

    [Fact]
    public void TownMap_PlayerSpawn_IsWalkable()
    {
        // Player spawn at tile (12, 17)
        Assert.True(_map.IsWalkable(12, 17));
    }

    [Fact]
    public void TownMap_EldersHouse_HasWalls()
    {
        // Elder's house perimeter at (2..7, 2..7)
        // Check corners
        Assert.Equal(TileType.Wall, _map.GetTile(2, 2));
        Assert.Equal(TileType.Wall, _map.GetTile(7, 2));
        Assert.Equal(TileType.Wall, _map.GetTile(2, 7));
        Assert.Equal(TileType.Wall, _map.GetTile(7, 7));
        // Check mid-edge
        Assert.Equal(TileType.Wall, _map.GetTile(4, 2)); // top edge
        Assert.Equal(TileType.Wall, _map.GetTile(2, 4)); // left edge
    }

    [Fact]
    public void TownMap_MerchantStall_HasRoad()
    {
        // Road strip at tiles (8..15, 7..10)
        Assert.Equal(TileType.Road, _map.GetTile(10, 8));
        Assert.Equal(TileType.Road, _map.GetTile(12, 9));
        Assert.Equal(TileType.Road, _map.GetTile(8, 7));
        Assert.Equal(TileType.Road, _map.GetTile(15, 10));
    }

    [Fact]
    public void TownMap_GateTile_IsWalkable()
    {
        // East gate at (30, 17) and (30, 18)
        Assert.True(_map.IsWalkable(30, 17));
        Assert.True(_map.IsWalkable(30, 18));
    }

    [Fact]
    public void TownMap_InsideBuilding_IsGround()
    {
        // Interior of Elder's house: (4, 4) should be ground (inside the wall perimeter)
        Assert.Equal(TileType.Ground, _map.GetTile(4, 4));
        Assert.Equal(TileType.Ground, _map.GetTile(5, 5));
    }

    [Fact]
    public void TownMap_CenterArea_AllWalkable()
    {
        // Open area around player spawn
        Assert.True(_map.IsWalkable(10, 17));
        Assert.True(_map.IsWalkable(14, 17));
        Assert.True(_map.IsWalkable(12, 15));
        Assert.True(_map.IsWalkable(12, 19));
    }

    [Fact]
    public void TownMap_SecondBuilding_WallsNotOverwrittenByRoad()
    {
        // Second building bottom wall at y=7, x=13..18
        // Road covers x=8..15, y=7..10 — must NOT overwrite building walls
        Assert.Equal(TileType.Wall, _map.GetTile(13, 7));
        Assert.Equal(TileType.Wall, _map.GetTile(14, 7));
        Assert.Equal(TileType.Wall, _map.GetTile(15, 7));
        Assert.Equal(TileType.Wall, _map.GetTile(16, 7));
        Assert.Equal(TileType.Wall, _map.GetTile(17, 7));
        Assert.Equal(TileType.Wall, _map.GetTile(18, 7));
    }
}
