using Brinell.Stride.Infrastructure;
using Oravey2.Core.Automation;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests;

/// <summary>
/// Tests for M1 Phase 4: Victory overlay when m1_complete flag is set.
/// Uses TownTestFixture (town scenario has VictoryCheckScript wired).
/// </summary>
public class VictoryCheckUITests : IAsyncLifetime
{
    private readonly TownTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public VictoryCheckUITests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void Victory_ShowsOverlay()
    {
        // Set m1_complete flag (normally set when quest 3 completes)
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "m1_complete", true);

        // Advance a few frames for VictoryCheckScript to detect the flag
        _fixture.Context.HoldKey(VirtualKey.Space, 500);

        var overlay = GameQueryHelpers.GetGameOverState(_fixture.Context);
        _output.WriteLine($"Overlay visible={overlay.Visible}, title={overlay.Title}, subtitle={overlay.Subtitle}");
        Assert.True(overlay.Visible);
        Assert.Equal("HAVEN IS SAFE", overlay.Title);
        Assert.Equal("You completed all quests.", overlay.Subtitle);

        var victory = GameQueryHelpers.GetVictoryState(_fixture.Context);
        Assert.True(victory.Achieved);
    }

    [Fact]
    public void Victory_DoesNotEndGame()
    {
        // Set m1_complete flag
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "m1_complete", true);
        _fixture.Context.HoldKey(VirtualKey.Space, 500);

        // Player should still be in Exploring state
        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        _output.WriteLine($"State after victory: {state}");
        Assert.Equal("Exploring", state);

        // Wait for overlay to auto-dismiss (5s + margin)
        _fixture.Context.HoldKey(VirtualKey.Space, 6000);

        // Overlay should be gone
        var overlay = GameQueryHelpers.GetGameOverState(_fixture.Context);
        Assert.False(overlay.Visible, "Victory overlay should auto-dismiss after 5 seconds");
    }

    [Fact]
    public void Victory_AllQuestsComplete()
    {
        // Simulate full quest chain completion by setting all flags
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "q_rat_hunt_done", true);
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "q_raider_camp_done", true);
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "q_safe_passage_done", true);
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "m1_complete", true);
        _fixture.Context.HoldKey(VirtualKey.Space, 500);

        var victory = GameQueryHelpers.GetVictoryState(_fixture.Context);
        _output.WriteLine($"Victory achieved: {victory.Achieved}");
        Assert.True(victory.Achieved);
        Assert.Equal("HAVEN IS SAFE", victory.Title);
    }
}
