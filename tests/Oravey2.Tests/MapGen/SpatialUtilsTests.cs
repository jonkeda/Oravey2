using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Spatial;

namespace Oravey2.Tests.MapGen;

public class SpatialUtilsTests
{
    [Fact]
    public void FindOverlaps_NonOverlapping_ReturnsEmpty()
    {
        var buildings = new[]
        {
            new BuildingFootprint("a", 0, 0, 3, 3),
            new BuildingFootprint("b", 10, 10, 3, 3)
        };

        var overlaps = SpatialUtils.FindOverlaps(buildings);
        Assert.Empty(overlaps);
    }

    [Fact]
    public void FindOverlaps_Overlapping_ReturnsPair()
    {
        var buildings = new[]
        {
            new BuildingFootprint("a", 0, 0, 5, 5),
            new BuildingFootprint("b", 3, 3, 5, 5)
        };

        var overlaps = SpatialUtils.FindOverlaps(buildings);
        Assert.Single(overlaps);
        Assert.Equal("a", overlaps[0].A);
        Assert.Equal("b", overlaps[0].B);
    }

    [Fact]
    public void IsTileWithinBounds_Inside_ReturnsTrue()
    {
        Assert.True(SpatialUtils.IsTileWithinBounds(0, 0, 5, 5, 2, 2));
    }

    [Fact]
    public void IsTileWithinBounds_Outside_ReturnsFalse()
    {
        Assert.False(SpatialUtils.IsTileWithinBounds(3, 0, 0, 0, 2, 2));
    }

    [Fact]
    public void IsTileWithinBounds_NegativeChunk_ReturnsFalse()
    {
        Assert.False(SpatialUtils.IsTileWithinBounds(-1, 0, 0, 0, 2, 2));
    }

    [Fact]
    public void IsTileWithinBounds_NegativeLocal_ReturnsFalse()
    {
        Assert.False(SpatialUtils.IsTileWithinBounds(0, 0, -1, 0, 2, 2));
    }

    [Fact]
    public void IsTileOnWater_OnRiver_ReturnsTrue()
    {
        var water = new WaterBlueprint(
            new[] { new RiverBlueprint("r1", new[] { new[] { 5, 5 } }, 3, 4, null) },
            null);

        Assert.True(SpatialUtils.IsTileOnWater(0, 0, 5, 5, water));
    }

    [Fact]
    public void IsTileOnWater_OnLake_ReturnsTrue()
    {
        var water = new WaterBlueprint(
            null,
            new[] { new LakeBlueprint("l1", 10, 10, 3, 5, 3) });

        Assert.True(SpatialUtils.IsTileOnWater(0, 0, 10, 10, water));
    }

    [Fact]
    public void IsTileOnWater_NoWater_ReturnsFalse()
    {
        Assert.False(SpatialUtils.IsTileOnWater(0, 0, 5, 5, null));
    }

    [Fact]
    public void IsTileOnBuilding_OnFootprint_ReturnsTrue()
    {
        var buildings = new[] { new BuildingFootprint("b1", 5, 5, 3, 3) };
        Assert.True(SpatialUtils.IsTileOnBuilding(0, 0, 6, 6, buildings));
    }

    [Fact]
    public void IsTileOnBuilding_OffFootprint_ReturnsFalse()
    {
        var buildings = new[] { new BuildingFootprint("b1", 5, 5, 3, 3) };
        Assert.False(SpatialUtils.IsTileOnBuilding(0, 0, 0, 0, buildings));
    }

    [Fact]
    public void IsWalkable_OpenTile_ReturnsTrue()
    {
        Assert.True(SpatialUtils.IsWalkable(
            0, 0, 0, 0, 2, 2, null, Array.Empty<BuildingFootprint>()));
    }

    [Fact]
    public void IsWalkable_WaterTile_ReturnsFalse()
    {
        var water = new WaterBlueprint(
            null,
            new[] { new LakeBlueprint("l1", 0, 0, 5, 5, 3) });

        Assert.False(SpatialUtils.IsWalkable(
            0, 0, 0, 0, 2, 2, water, Array.Empty<BuildingFootprint>()));
    }

    [Fact]
    public void BuildingFootprint_Overlaps_ReturnsTrue()
    {
        var a = new BuildingFootprint("a", 0, 0, 5, 5);
        var b = new BuildingFootprint("b", 3, 3, 5, 5);
        Assert.True(a.Overlaps(b));
    }

    [Fact]
    public void BuildingFootprint_NoOverlap_ReturnsFalse()
    {
        var a = new BuildingFootprint("a", 0, 0, 3, 3);
        var b = new BuildingFootprint("b", 10, 10, 3, 3);
        Assert.False(a.Overlaps(b));
    }

    [Fact]
    public void BuildingFootprint_OccupiedTiles_ReturnsAllTiles()
    {
        var fp = new BuildingFootprint("b1", 2, 3, 2, 2);
        var tiles = fp.OccupiedTiles().ToList();

        Assert.Equal(4, tiles.Count);
        Assert.Contains((2, 3), tiles);
        Assert.Contains((3, 3), tiles);
        Assert.Contains((2, 4), tiles);
        Assert.Contains((3, 4), tiles);
    }
}
