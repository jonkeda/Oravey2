using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class HeightHelperTests
{
    // --- SlopeType classification ---

    [Fact]
    public void GetSlopeType_Flat_Delta0()
    {
        Assert.Equal(SlopeType.Flat, HeightHelper.GetSlopeType(0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-1)]
    [InlineData(-2)]
    public void GetSlopeType_Gentle_Delta1Or2(int delta)
    {
        Assert.Equal(SlopeType.Gentle, HeightHelper.GetSlopeType(delta));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(-3)]
    [InlineData(-6)]
    public void GetSlopeType_Steep_Delta3To6(int delta)
    {
        Assert.Equal(SlopeType.Steep, HeightHelper.GetSlopeType(delta));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(20)]
    [InlineData(-7)]
    [InlineData(-20)]
    public void GetSlopeType_Cliff_Delta7Plus(int delta)
    {
        Assert.Equal(SlopeType.Cliff, HeightHelper.GetSlopeType(delta));
    }

    // --- Passability ---

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(20, false)]
    [InlineData(-6, true)]
    [InlineData(-7, false)]
    public void IsPassable_CorrectForDelta(int delta, bool expected)
    {
        Assert.Equal(expected, HeightHelper.IsPassable(delta));
    }

    // --- Movement cost ---

    [Fact]
    public void GetSlopeMovementCost_Flat_Returns1()
    {
        Assert.Equal(1.0f, HeightHelper.GetSlopeMovementCost(0));
    }

    [Fact]
    public void GetSlopeMovementCost_Gentle1_Returns1_25()
    {
        Assert.Equal(1.25f, HeightHelper.GetSlopeMovementCost(1));
    }

    [Fact]
    public void GetSlopeMovementCost_Gentle2_Returns1_5()
    {
        Assert.Equal(1.5f, HeightHelper.GetSlopeMovementCost(2));
    }

    [Fact]
    public void GetSlopeMovementCost_Steep3_Returns2_5()
    {
        Assert.Equal(2.5f, HeightHelper.GetSlopeMovementCost(3));
    }

    [Fact]
    public void GetSlopeMovementCost_Steep6_Returns4()
    {
        Assert.Equal(4.0f, HeightHelper.GetSlopeMovementCost(6));
    }

    [Fact]
    public void GetSlopeMovementCost_Cliff7_ReturnsInfinity()
    {
        Assert.Equal(float.PositiveInfinity, HeightHelper.GetSlopeMovementCost(7));
    }

    [Fact]
    public void GetSlopeMovementCost_Cliff20_ReturnsInfinity()
    {
        Assert.Equal(float.PositiveInfinity, HeightHelper.GetSlopeMovementCost(20));
    }

    [Fact]
    public void GetSlopeMovementCost_NegativeDelta_SameMagnitudeRules()
    {
        // Going downhill uses same magnitude
        Assert.Equal(1.25f, HeightHelper.GetSlopeMovementCost(-1));
        Assert.Equal(2.5f, HeightHelper.GetSlopeMovementCost(-3));
        Assert.Equal(float.PositiveInfinity, HeightHelper.GetSlopeMovementCost(-7));
    }

    // --- GetHeightDelta with map ---

    [Fact]
    public void GetHeightDelta_ReadsFromMap()
    {
        var map = new TileMapData(4, 4);
        map.SetTileData(0, 0, new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0));
        map.SetTileData(1, 0, new TileData(SurfaceType.Dirt, 5, 0, 0, TileFlags.Walkable, 0));

        Assert.Equal(4, HeightHelper.GetHeightDelta(map, 0, 0, 1, 0));
        Assert.Equal(-4, HeightHelper.GetHeightDelta(map, 1, 0, 0, 0));
    }
}
