using Oravey2.Core.Camera;
using Xunit;

namespace Oravey2.Tests.Config;

public class CameraConfigTests
{
    [Fact]
    public void CameraConfig_DefaultYaw_Is45()
    {
        var camera = new TacticalCameraScript();
        Assert.Equal(45f, camera.Yaw);
    }

    [Fact]
    public void CameraConfig_DefaultPitch_Is30()
    {
        var camera = new TacticalCameraScript();
        Assert.Equal(30f, camera.Pitch);
    }

    [Fact]
    public void CameraConfig_DefaultFov_Is25()
    {
        var camera = new TacticalCameraScript();
        Assert.Equal(25f, camera.CurrentFov);
    }
}
