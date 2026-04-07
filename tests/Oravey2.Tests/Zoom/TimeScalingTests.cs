using Oravey2.Core.Camera;

namespace Oravey2.Tests.Zoom;

public class TimeScalingTests
{
    [Fact]
    public void Level1_TimeScale_Is1()
    {
        Assert.Equal(1f, ZoomLevelController.GetTimeScale(ZoomLevel.Local));
    }

    [Fact]
    public void Level2_TimeScale_Is60()
    {
        Assert.Equal(60f, ZoomLevelController.GetTimeScale(ZoomLevel.Regional));
    }

    [Fact]
    public void Level3_TimeScale_Is1440()
    {
        Assert.Equal(1440f, ZoomLevelController.GetTimeScale(ZoomLevel.Continental));
    }
}
