using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class CameraDefaultTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void PlayerVisible_AtTunedZoom()
    {
        var psp = GameQueryHelpers.GetPlayerScreenPosition(_fixture.Context);
        Assert.True(psp.OnScreen, "Player should be on screen at tuned camera defaults");
    }
}
