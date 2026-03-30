using Oravey2.Core.Audio;
using Oravey2.Core.World;

namespace Oravey2.Tests.Audio;

public class FootstepSurfaceMapTests
{
    [Fact]
    public void GroundReturnsCorrectSfx()
    {
        Assert.Equal("sfx_footstep_ground", FootstepSurfaceMap.GetSfxId(TileType.Ground));
    }

    [Fact]
    public void RoadReturnsCorrectSfx()
    {
        Assert.Equal("sfx_footstep_road", FootstepSurfaceMap.GetSfxId(TileType.Road));
    }

    [Fact]
    public void RubbleReturnsCorrectSfx()
    {
        Assert.Equal("sfx_footstep_rubble", FootstepSurfaceMap.GetSfxId(TileType.Rubble));
    }

    [Fact]
    public void WaterReturnsCorrectSfx()
    {
        Assert.Equal("sfx_footstep_water", FootstepSurfaceMap.GetSfxId(TileType.Water));
    }

    [Fact]
    public void EmptyReturnsNull()
    {
        Assert.Null(FootstepSurfaceMap.GetSfxId(TileType.Empty));
    }

    [Fact]
    public void WallReturnsNull()
    {
        Assert.Null(FootstepSurfaceMap.GetSfxId(TileType.Wall));
    }
}
