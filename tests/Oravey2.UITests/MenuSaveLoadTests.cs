using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests;

/// <summary>
/// M1 Phase 1 tests: menu system and save/load pipeline.
/// Game starts in Exploring (automation mode skips start menu),
/// so we test pause menu, save/load round-trips, and menu state queries.
/// </summary>
public class MenuSaveLoadTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public MenuSaveLoadTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void PauseMenu_NotVisible_Initially()
    {
        var menu = GameQueryHelpers.GetMenuState(_fixture.Context);
        _output.WriteLine($"Menu: screen={menu.Screen}, visible={menu.Visible}");
        Assert.Equal("None", menu.Screen);
        Assert.False(menu.Visible);
    }

    [Fact]
    public void PauseMenu_EscapeOpens()
    {
        // Press Escape to open pause menu
        GameQueryHelpers.ClickMenuButton(_fixture.Context, "PauseMenu", "Resume"); // ensure starts unpaused
        _fixture.Context.PressKey(Brinell.Stride.Infrastructure.VirtualKey.Escape);

        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        _output.WriteLine($"GameState after Escape: {state}");
        Assert.Equal("Paused", state);

        // Resume
        GameQueryHelpers.ClickMenuButton(_fixture.Context, "PauseMenu", "Resume");
        state = GameQueryHelpers.GetGameState(_fixture.Context);
        _output.WriteLine($"GameState after Resume: {state}");
        Assert.Equal("Exploring", state);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void SaveLoad_RoundTrip_Position()
    {
        // Teleport to known position
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 5.0, 0.5, -3.0);
        var posBefore = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _output.WriteLine($"Before save: x={posBefore.X:F1}, z={posBefore.Z:F1}");

        // Save
        var saveResult = GameQueryHelpers.TriggerSave(_fixture.Context);
        Assert.True(saveResult.Success);
        _output.WriteLine($"Saved to: {saveResult.Path}");

        // Teleport somewhere else
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 0, 0.5, 0);
        var posMoved = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _output.WriteLine($"After move: x={posMoved.X:F1}, z={posMoved.Z:F1}");

        // Load
        var loadResult = GameQueryHelpers.TriggerLoad(_fixture.Context);
        Assert.True(loadResult.Success);

        // Verify position restored
        var posAfter = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _output.WriteLine($"After load: x={posAfter.X:F1}, z={posAfter.Z:F1}");
        Assert.Equal(5.0, posAfter.X, 0.5);
        Assert.Equal(-3.0, posAfter.Z, 0.5);
    }

    [Fact]
    public void PauseMenu_SaveGame_ShowsNotification()
    {
        // Open pause, trigger save via menu
        _fixture.Context.PressKey(Brinell.Stride.Infrastructure.VirtualKey.Escape);
        GameQueryHelpers.ClickMenuButton(_fixture.Context, "PauseMenu", "Save Game");

        // Check notification appeared
        var feed = GameQueryHelpers.GetNotificationFeed(_fixture.Context);
        _output.WriteLine($"Notification count: {feed.Count}");
        Assert.True(feed.Count > 0);
        Assert.Contains(feed.Messages, m => m.Text.Contains("Saved", StringComparison.OrdinalIgnoreCase));

        // Resume back to exploring
        GameQueryHelpers.ClickMenuButton(_fixture.Context, "PauseMenu", "Resume");
    }
}
