using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class TileDataTests
{
    [Fact]
    public void Empty_HasAllZeroValues()
    {
        var empty = TileData.Empty;
        Assert.Equal(SurfaceType.Dirt, empty.Surface);
        Assert.Equal(0, empty.HeightLevel);
        Assert.Equal(0, empty.WaterLevel);
        Assert.Equal(0, empty.StructureId);
        Assert.Equal(TileFlags.None, empty.Flags);
        Assert.Equal(0, empty.VariantSeed);
    }

    [Fact]
    public void Empty_IsNotWalkable()
    {
        Assert.False(TileData.Empty.IsWalkable);
    }

    [Fact]
    public void IsWalkable_TrueWhenFlagSet()
    {
        var tile = new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0);
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void IsWalkable_FalseWhenFlagNotSet()
    {
        var tile = new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.None, 0);
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void HasWater_TrueWhenWaterLevelAboveHeight()
    {
        var tile = new TileData(SurfaceType.Mud, 0, 2, 0, TileFlags.None, 0);
        Assert.True(tile.HasWater);
    }

    [Fact]
    public void HasWater_FalseWhenWaterLevelAtOrBelowHeight()
    {
        var tile = new TileData(SurfaceType.Mud, 2, 2, 0, TileFlags.None, 0);
        Assert.False(tile.HasWater);

        var tile2 = new TileData(SurfaceType.Mud, 3, 1, 0, TileFlags.None, 0);
        Assert.False(tile2.HasWater);
    }

    [Fact]
    public void WaterDepth_CalculatedCorrectly()
    {
        var tile = new TileData(SurfaceType.Mud, 1, 5, 0, TileFlags.None, 0);
        Assert.Equal(4, tile.WaterDepth);
    }

    [Fact]
    public void WaterDepth_ZeroWhenNoWater()
    {
        var tile = new TileData(SurfaceType.Dirt, 3, 1, 0, TileFlags.Walkable, 0);
        Assert.Equal(0, tile.WaterDepth);
    }

    [Fact]
    public void LegacyTileType_DirtWalkable_ReturnsGround()
    {
        var tile = new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0);
        Assert.Equal(TileType.Ground, tile.LegacyTileType);
    }

    [Fact]
    public void LegacyTileType_AsphaltWalkable_ReturnsRoad()
    {
        var tile = new TileData(SurfaceType.Asphalt, 1, 0, 0, TileFlags.Walkable, 0);
        Assert.Equal(TileType.Road, tile.LegacyTileType);
    }

    [Fact]
    public void LegacyTileType_RockWalkable_ReturnsRubble()
    {
        var tile = new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 0);
        Assert.Equal(TileType.Rubble, tile.LegacyTileType);
    }

    [Fact]
    public void LegacyTileType_WaterPresent_ReturnsWater()
    {
        var tile = new TileData(SurfaceType.Mud, 0, 2, 0, TileFlags.None, 0);
        Assert.Equal(TileType.Water, tile.LegacyTileType);
    }

    [Fact]
    public void LegacyTileType_StructureNonWalkable_ReturnsWall()
    {
        var tile = new TileData(SurfaceType.Concrete, 1, 0, 1, TileFlags.None, 0);
        Assert.Equal(TileType.Wall, tile.LegacyTileType);
    }

    [Fact]
    public void LegacyTileType_NonWalkableNoStructureNoWater_ReturnsEmpty()
    {
        var tile = new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.None, 0);
        Assert.Equal(TileType.Empty, tile.LegacyTileType);
    }
}
