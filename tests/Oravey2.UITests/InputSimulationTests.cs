using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Tests that verify keyboard input simulation works through the
/// automation pipe. The game must be in Exploring state for movement keys.
/// </summary>
public class InputSimulationTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void PressKey_W_DoesNotThrow()
    {
        // Simulate pressing W (forward movement)
        _fixture.Context.PressKey(VirtualKey.W);
    }

    [Fact]
    public void PressKey_WASD_DoesNotThrow()
    {
        _fixture.Context.PressKey(VirtualKey.W);
        _fixture.Context.PressKey(VirtualKey.A);
        _fixture.Context.PressKey(VirtualKey.S);
        _fixture.Context.PressKey(VirtualKey.D);
    }

    [Fact]
    public void HoldKey_W_ForHalfSecond()
    {
        // Hold W for 500ms — player should move forward
        _fixture.Context.HoldKey(VirtualKey.W, 500);

        // Allow a frame for the key release to complete
        Thread.Sleep(100);
    }

    [Fact]
    public void PressKey_Escape_DoesNotThrow()
    {
        // Escape could open a pause menu in the future
        _fixture.Context.PressKey(VirtualKey.Escape);
    }

    [Fact]
    public void PressKey_Space_DoesNotThrow()
    {
        _fixture.Context.PressKey(VirtualKey.Space);
    }
}
