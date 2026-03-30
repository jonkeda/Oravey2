using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class GameOverTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void GameOverOverlay_NotVisible_AtStart()
    {
        var state = GameQueryHelpers.GetGameOverState(_fixture.Context);
        Assert.False(state.Visible);
        Assert.Equal("", state.Title);
    }

    [Fact]
    public void PlayerDeath_TransitionsToGameOver()
    {
        // Enter combat first (GameOver only triggers from InCombat)
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            var state = GameQueryHelpers.GetGameState(_fixture.Context);
            if (state == "InCombat") break;
        }

        // Kill the player
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        GameQueryHelpers.DamagePlayer(_fixture.Context, hud.Hp);

        // Wait a frame for state transition
        _fixture.Context.HoldKey(VirtualKey.Space, 100);

        var gameState = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("GameOver", gameState);
    }

    [Fact]
    public void PlayerDeath_ShowsGameOverOverlay()
    {
        // Enter combat
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            if (GameQueryHelpers.GetGameState(_fixture.Context) == "InCombat") break;
        }

        // Kill the player
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        GameQueryHelpers.DamagePlayer(_fixture.Context, hud.Hp);
        _fixture.Context.HoldKey(VirtualKey.Space, 100);

        var overlay = GameQueryHelpers.GetGameOverState(_fixture.Context);
        Assert.True(overlay.Visible);
        Assert.Equal("GAME OVER", overlay.Title);
    }

    [Fact]
    public void DamagePlayer_ReducesHp()
    {
        var before = GameQueryHelpers.GetHudState(_fixture.Context);
        var result = GameQueryHelpers.DamagePlayer(_fixture.Context, 25);
        Assert.Equal(before.Hp - 25, result.NewHp);
        Assert.True(result.IsAlive);
    }

    [Fact]
    public void DamagePlayer_ToZero_NotAlive()
    {
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        var result = GameQueryHelpers.DamagePlayer(_fixture.Context, hud.Hp);
        Assert.Equal(0, result.NewHp);
        Assert.False(result.IsAlive);
    }
}
