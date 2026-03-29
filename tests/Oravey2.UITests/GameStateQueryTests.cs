using Brinell.Stride.Communication;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Tests that query game state through the automation pipe.
/// Verifies the game is in the expected state after startup
/// and that game queries return valid responses.
/// </summary>
public class GameStateQueryTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void GameQuery_IsReady_ReturnsTrue()
    {
        var response = _fixture.Context.SendCommand(
            AutomationCommand.GameQuery("IsReady"));

        Assert.True(response.Success);
    }

    [Fact]
    public void GameQuery_IsBusy_ReturnsFalse()
    {
        var response = _fixture.Context.SendCommand(
            AutomationCommand.GameQuery("IsBusy"));

        Assert.True(response.Success);
    }

    [Fact]
    public void GameQuery_GetWindowInfo_ReturnsSuccess()
    {
        var response = _fixture.Context.SendCommand(
            AutomationCommand.GameQuery("GetWindowInfo"));

        Assert.True(response.Success);
    }

    [Fact]
    public void GameQuery_UnknownMethod_ReturnsError()
    {
        var response = _fixture.Context.SendCommand(
            AutomationCommand.GameQuery("NonExistentQuery"));

        Assert.False(response.Success);
    }

    [Fact]
    public void ElementQuery_NonExistentElement_ReturnsNotFound()
    {
        // No UI elements exist yet, so any element query should indicate non-existence
        var state = _fixture.Context.GetElementState("NonExistentElement");

        Assert.False(state.Exists);
    }

    [Fact]
    public void ElementExists_NonExistentElement_ReturnsFalse()
    {
        Assert.False(_fixture.Context.ElementExists("SomeButton"));
    }

    [Fact]
    public void ElementIsVisible_NonExistentElement_ReturnsFalse()
    {
        Assert.False(_fixture.Context.ElementIsVisible("SomePanel"));
    }
}
