using Oravey2.Core.World;
using Oravey2.Core.World.Rendering;

namespace Oravey2.Tests.Rendering;

public class SubTileSelectorTests
{
    [Fact]
    public void NE_Fill_Rotation0()
    {
        var config = SubTileSelector.GetSubTileConfig(SubTileShape.Fill, Quadrant.NE, SurfaceType.Dirt);
        Assert.Equal(0, config.RotationDegrees);
        Assert.Equal(SubTileShape.Fill, config.Shape);
        Assert.Equal(SurfaceType.Dirt, config.Surface);
    }

    [Fact]
    public void SE_Fill_Rotation90()
    {
        var config = SubTileSelector.GetSubTileConfig(SubTileShape.Fill, Quadrant.SE, SurfaceType.Dirt);
        Assert.Equal(90, config.RotationDegrees);
    }

    [Fact]
    public void SW_Fill_Rotation180()
    {
        var config = SubTileSelector.GetSubTileConfig(SubTileShape.Fill, Quadrant.SW, SurfaceType.Rock);
        Assert.Equal(180, config.RotationDegrees);
    }

    [Fact]
    public void NW_Fill_Rotation270()
    {
        var config = SubTileSelector.GetSubTileConfig(SubTileShape.Fill, Quadrant.NW, SurfaceType.Asphalt);
        Assert.Equal(270, config.RotationDegrees);
    }

    [Fact]
    public void NE_Edge_StillRotation0()
    {
        var config = SubTileSelector.GetSubTileConfig(SubTileShape.Edge, Quadrant.NE, SurfaceType.Dirt);
        Assert.Equal(0, config.RotationDegrees);
        Assert.Equal(SubTileShape.Edge, config.Shape);
    }

    [Fact]
    public void SW_OuterCorner_Rotation180()
    {
        var config = SubTileSelector.GetSubTileConfig(SubTileShape.OuterCorner, Quadrant.SW, SurfaceType.Sand);
        Assert.Equal(180, config.RotationDegrees);
        Assert.Equal(SubTileShape.OuterCorner, config.Shape);
    }

    [Fact]
    public void PreservesSurfaceType()
    {
        var config = SubTileSelector.GetSubTileConfig(SubTileShape.InnerCorner, Quadrant.SE, SurfaceType.Metal);
        Assert.Equal(SurfaceType.Metal, config.Surface);
    }
}
