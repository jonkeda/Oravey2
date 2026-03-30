using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class ScenarioResetTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void ResetScenario_RemovesAllEnemies()
    {
        var result = GameQueryHelpers.ResetScenario(_fixture.Context);

        Assert.True(result.Success);
        Assert.Equal(0, result.EnemyCount);

        var combatState = GameQueryHelpers.GetCombatState(_fixture.Context);
        Assert.False(combatState.InCombat);
        Assert.Equal(0, combatState.EnemyCount);
    }

    [Fact]
    public void ResetScenario_HealsPlayerToMax()
    {
        // Damage the player first
        GameQueryHelpers.DamagePlayer(_fixture.Context, 50);
        var hudBefore = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.True(hudBefore.Hp < hudBefore.MaxHp, "Player should be damaged before reset");

        var result = GameQueryHelpers.ResetScenario(_fixture.Context);

        Assert.True(result.Success);
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(hud.MaxHp, hud.Hp);
        Assert.Equal(hud.MaxHp, result.PlayerHp);
    }

    [Fact]
    public void ResetScenario_ExitsCombat()
    {
        // Teleport near enemies to trigger combat
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 8, 0.5, 8);
        _fixture.Context.HoldKey(VirtualKey.Space, 500);

        var stateBefore = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("InCombat", stateBefore);

        var result = GameQueryHelpers.ResetScenario(_fixture.Context);

        Assert.True(result.Success);
        var stateAfter = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", stateAfter);
    }

    [Fact]
    public void ResetAfterGameOver_RestoresToExploring()
    {
        // Kill the player to trigger GameOver
        GameQueryHelpers.DamagePlayer(_fixture.Context, 1000);
        var stateBefore = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("GameOver", stateBefore);

        var result = GameQueryHelpers.ResetScenario(_fixture.Context);

        Assert.True(result.Success);
        var stateAfter = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", stateAfter);

        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(hud.MaxHp, hud.Hp);
    }
}
