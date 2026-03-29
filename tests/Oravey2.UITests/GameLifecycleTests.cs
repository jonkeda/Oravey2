using Brinell.Stride.Communication;
using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Tests that verify the game launches, connects to automation,
/// and responds to basic commands.
/// </summary>
public class GameLifecycleTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void Game_StartsAndConnects()
    {
        Assert.True(_fixture.Context.IsGameReady);
    }

    [Fact]
    public void Game_IsNotBusy_AfterStartup()
    {
        Assert.False(_fixture.Context.IsGameBusy());
    }

    [Fact]
    public void Game_IsInExploringState()
    {
        var response = _fixture.Context.SendCommand(
            AutomationCommand.GameQuery("GetGameState"));

        Assert.True(response.Success);
        Assert.Equal("Exploring", response.Result?.ToString());
    }

    [Fact]
    public void Game_PlayerEntityExists()
    {
        var response = _fixture.Context.SendCommand(
            AutomationCommand.GameQuery("GetPlayerPosition"));

        Assert.True(response.Success, $"GetPlayerPosition failed: {response.Error}");
    }

    [Fact]
    public void Game_CameraEntityExists()
    {
        var response = _fixture.Context.SendCommand(
            AutomationCommand.GameQuery("GetCameraState"));

        Assert.True(response.Success, $"GetCameraState failed: {response.Error}");
    }
}
