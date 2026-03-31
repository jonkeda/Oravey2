using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class CameraDefaultTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    [Trait("Category", "Smoke")]
    public void CameraZoom_MatchesTunedDefault()
    {
        var cam = GameQueryHelpers.GetCameraState(_fixture.Context);
        // Phase D: FOV tuned to 28°
        Assert.Equal(28.0, cam.Zoom, 2.0);
    }

    [Fact]
    public void CameraPitch_Is30Degrees()
    {
        var cam = GameQueryHelpers.GetCameraState(_fixture.Context);
        Assert.Equal(30.0, cam.Pitch, 1.0);
    }

    [Fact]
    public void CameraYaw_Is45Degrees()
    {
        var cam = GameQueryHelpers.GetCameraState(_fixture.Context);
        Assert.Equal(45.0, cam.Yaw, 1.0);
    }

    [Fact]
    public void PlayerVisible_AtTunedZoom()
    {
        var psp = GameQueryHelpers.GetPlayerScreenPosition(_fixture.Context);
        Assert.True(psp.OnScreen, "Player should be on screen at tuned camera defaults");
    }
}
