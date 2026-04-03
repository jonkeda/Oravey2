using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class TileDataFactoryTests
{
    [Fact]
    public void Ground_ProducesCorrectLegacyType()
    {
        var tile = TileDataFactory.Ground();
        Assert.Equal(TileType.Ground, tile.LegacyTileType);
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void Road_ProducesCorrectLegacyType()
    {
        var tile = TileDataFactory.Road();
        Assert.Equal(TileType.Road, tile.LegacyTileType);
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void Rubble_ProducesCorrectLegacyType()
    {
        var tile = TileDataFactory.Rubble();
        Assert.Equal(TileType.Rubble, tile.LegacyTileType);
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void Water_ProducesCorrectLegacyType()
    {
        var tile = TileDataFactory.Water();
        Assert.Equal(TileType.Water, tile.LegacyTileType);
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void Wall_ProducesCorrectLegacyType()
    {
        var tile = TileDataFactory.Wall();
        Assert.Equal(TileType.Wall, tile.LegacyTileType);
        Assert.False(tile.IsWalkable);
    }

    [Theory]
    [InlineData(TileType.Empty)]
    [InlineData(TileType.Ground)]
    [InlineData(TileType.Road)]
    [InlineData(TileType.Rubble)]
    [InlineData(TileType.Water)]
    [InlineData(TileType.Wall)]
    public void FromLegacy_RoundTrips(TileType type)
    {
        var tile = TileDataFactory.FromLegacy(type);
        Assert.Equal(type, tile.LegacyTileType);
    }

    [Fact]
    public void Ground_SetsCorrectDefaults()
    {
        var tile = TileDataFactory.Ground();
        Assert.Equal(SurfaceType.Dirt, tile.Surface);
        Assert.Equal(1, tile.HeightLevel);
        Assert.Equal(TileFlags.Walkable, tile.Flags);
    }

    [Fact]
    public void Road_SetsCorrectDefaults()
    {
        var tile = TileDataFactory.Road();
        Assert.Equal(SurfaceType.Asphalt, tile.Surface);
        Assert.Equal(1, tile.HeightLevel);
        Assert.Equal(TileFlags.Walkable, tile.Flags);
    }

    [Fact]
    public void Wall_SetsCorrectDefaults()
    {
        var tile = TileDataFactory.Wall();
        Assert.Equal(SurfaceType.Concrete, tile.Surface);
        Assert.Equal(1, tile.HeightLevel);
        Assert.Equal(1, tile.StructureId);
        Assert.Equal(TileFlags.None, tile.Flags);
    }

    [Fact]
    public void Water_SetsCorrectDefaults()
    {
        var tile = TileDataFactory.Water();
        Assert.Equal(SurfaceType.Mud, tile.Surface);
        Assert.Equal(2, tile.WaterLevel);
        Assert.Equal(0, tile.HeightLevel);
    }
}
