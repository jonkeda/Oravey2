using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class WastelandMapBuilderTests
{
    private readonly TileMapData _map = WastelandMapBuilder.CreateWastelandMap();

    [Fact]
    public void WastelandMap_Is32x32()
    {
        Assert.Equal(32, _map.Width);
        Assert.Equal(32, _map.Height);
    }

    [Fact]
    public void WastelandMap_Border_IsWall_ExceptGate()
    {
        // All border tiles except the west gate should be walls
        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(TileType.Wall, _map.GetTile(i, 0));   // top row
            Assert.Equal(TileType.Wall, _map.GetTile(i, 31));  // bottom row
            Assert.Equal(TileType.Wall, _map.GetTile(31, i));  // right col
        }

        // Left col except gate
        for (int i = 0; i < 32; i++)
        {
            if (i == 17 || i == 18) continue; // gate tiles
            Assert.Equal(TileType.Wall, _map.GetTile(0, i));
        }
    }

    [Fact]
    public void WastelandMap_WestGate_IsWalkable()
    {
        Assert.True(_map.IsWalkable(0, 17));
        Assert.True(_map.IsWalkable(0, 18));
    }

    [Fact]
    public void WastelandMap_Road_Tiles()
    {
        // Road strip at (4..5, 2..27)
        Assert.Equal(TileType.Road, _map.GetTile(4, 2));
        Assert.Equal(TileType.Road, _map.GetTile(5, 15));
        Assert.Equal(TileType.Road, _map.GetTile(4, 27));
    }

    [Fact]
    public void WastelandMap_Water_IsNonWalkable()
    {
        // Water/rubble at (12..15, 4..7)
        Assert.Equal(TileType.Water, _map.GetTile(12, 4));
        Assert.Equal(TileType.Water, _map.GetTile(15, 7));
        Assert.False(_map.IsWalkable(13, 5));
    }

    [Fact]
    public void WastelandMap_Ruins_HasWalls()
    {
        // Ruins walls at perimeter (20..25, 8..13)
        Assert.Equal(TileType.Wall, _map.GetTile(20, 8));
        Assert.Equal(TileType.Wall, _map.GetTile(25, 8));
        Assert.Equal(TileType.Wall, _map.GetTile(20, 13));
        Assert.Equal(TileType.Wall, _map.GetTile(25, 13));
    }

    [Fact]
    public void WastelandMap_RuinsInterior_IsGround()
    {
        // Inside ruins should be ground
        Assert.Equal(TileType.Ground, _map.GetTile(22, 10));
        Assert.Equal(TileType.Ground, _map.GetTile(23, 11));
    }
}
