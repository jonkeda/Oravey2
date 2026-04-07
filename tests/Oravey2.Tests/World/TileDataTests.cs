using Oravey2.Core.World;
using System.Runtime.CompilerServices;

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

    [Fact]
    public void TileData_DefaultEmpty_AllFieldsZero()
    {
        var empty = TileData.Empty;
        Assert.Equal(SurfaceType.Dirt, empty.Surface);
        Assert.Equal(0, empty.HeightLevel);
        Assert.Equal(0, empty.WaterLevel);
        Assert.Equal(0, empty.StructureId);
        Assert.Equal(TileFlags.None, empty.Flags);
        Assert.Equal(0, empty.VariantSeed);
        Assert.Equal(LiquidType.None, empty.Liquid);
        Assert.Equal(CoverEdges.None, empty.HalfCover);
        Assert.Equal(CoverEdges.None, empty.FullCover);
    }

    [Fact]
    public void TileData_Size_FitsExpectedByteCount()
    {
        var size = Unsafe.SizeOf<TileData>();
        Assert.InRange(size, 12, 24);
    }

    [Fact]
    public void TileData_HasWater_WhenLiquidTypeSet()
    {
        var tile = new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0,
            Liquid: LiquidType.Toxic);
        Assert.True(tile.HasWater);
    }

    [Fact]
    public void TileData_HasWater_WhenWaterLevelAboveHeight_NoLiquid()
    {
        var tile = new TileData(SurfaceType.Mud, 0, 2, 0, TileFlags.None, 0);
        Assert.True(tile.HasWater);
    }

    [Fact]
    public void TileData_HasWater_False_WhenNoLiquidAndLevelBelow()
    {
        var tile = new TileData(SurfaceType.Dirt, 3, 1, 0, TileFlags.Walkable, 0);
        Assert.False(tile.HasWater);
    }

    [Fact]
    public void LinearFeature_ConstructsWithNodes()
    {
        var nodes = new[]
        {
            new LinearFeatureNode(new System.Numerics.Vector2(0, 0)),
            new LinearFeatureNode(new System.Numerics.Vector2(10, 5), OverrideHeight: 2.5f)
        };
        var feature = new LinearFeature(LinearFeatureType.Road, "paved", 3.0f, nodes);

        Assert.Equal(LinearFeatureType.Road, feature.Type);
        Assert.Equal("paved", feature.Style);
        Assert.Equal(3.0f, feature.Width);
        Assert.Equal(2, feature.Nodes.Count);
        Assert.Null(feature.Nodes[0].OverrideHeight);
        Assert.Equal(2.5f, feature.Nodes[1].OverrideHeight);
    }

    [Fact]
    public void TerrainModifier_Subtypes_AreRecords()
    {
        var flatten = new FlattenStrip(
            new[] { new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(10, 0) },
            4.0f, 5.0f);
        var channel = new ChannelCut(
            new[] { new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(5, 5) },
            2.0f, 1.5f);
        var level = new LevelRect(
            new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(10, 10), 3.0f);
        var crater = new Crater(
            new System.Numerics.Vector2(5, 5), 8.0f, 4.0f);

        Assert.IsAssignableFrom<TerrainModifier>(flatten);
        Assert.IsAssignableFrom<TerrainModifier>(channel);
        Assert.IsAssignableFrom<TerrainModifier>(level);
        Assert.IsAssignableFrom<TerrainModifier>(crater);
    }

    [Fact]
    public void LiquidType_AllValues_AreDistinct()
    {
        var values = Enum.GetValues<LiquidType>();
        Assert.Equal(values.Length, values.Distinct().Count());
    }

    [Fact]
    public void CoverEdges_AllDirections_AreSingleBits()
    {
        var combined = CoverEdges.North | CoverEdges.East | CoverEdges.South | CoverEdges.West;
        Assert.Equal((CoverEdges)0b1111, combined);
    }
}
