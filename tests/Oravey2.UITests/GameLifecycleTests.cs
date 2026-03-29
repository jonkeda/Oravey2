using Brinell.Stride.Communication;
using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Tests that verify the game launches, connects to automation,
/// and responds to basic commands. These are integration tests
/// that require the built game executable.
/// </summary>
public class GameLifecycleTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void Game_StartsAndConnects()
    {
        // If InitializeAsync succeeded, the game is running and connected.
        Assert.True(_fixture.Context.IsGameReady);
    }

    [Fact]
    public void Game_IsNotBusy_AfterStartup()
    {
        Assert.False(_fixture.Context.IsGameBusy());
    }

    [Fact]
    public void Game_CanTakeScreenshot()
    {
        var screenshotBytes = _fixture.Context.TakeScreenshot();

        // Screenshot may be empty if the game doesn't support it yet,
        // but the command should not throw.
        Assert.NotNull(screenshotBytes);
    }

    [Fact]
    public void Game_RespondsToRawCommand()
    {
        // Send a game query to verify the pipe is alive.
        var response = _fixture.Context.SendCommand(
            AutomationCommand.GameQuery("IsReady"));

        Assert.True(response.Success);
    }
}
