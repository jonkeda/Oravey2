using Oravey2.UITests.Pages;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Tests that verify the GameWorldPage page object works correctly.
/// As UI elements are added to the game (HUD, inventory, dialogue),
/// corresponding page objects and tests should be added here.
/// </summary>
public class GameWorldTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void GameWorld_PageObject_CanBeCreated()
    {
        var page = new GameWorldPage(_fixture.Context);
        Assert.NotNull(page);
        Assert.Equal("GameWorld", page.Name);
    }

    [Fact]
    public void GameWorld_WaitForReady_Succeeds()
    {
        var page = new GameWorldPage(_fixture.Context);
        var isReady = page.WaitReady(timeoutMs: 10000);

        Assert.True(isReady);
    }

    [Fact]
    public void GameWorld_IsLoaded_AfterStartup()
    {
        var page = new GameWorldPage(_fixture.Context);
        Assert.True(page.IsLoaded());
    }
}
