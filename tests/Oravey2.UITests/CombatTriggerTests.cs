using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Tests encounter detection and GameState transitions.
/// Verifies enemies exist, proximity triggers combat, and distance keeps exploring.
/// </summary>
public class CombatTriggerTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void StartState_IsExploring()
    {
        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", state);
    }

    [Fact]
    public void ThreeEnemies_ExistAtStartup()
    {
        var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        Assert.Equal(3, combat.EnemyCount);
        Assert.All(combat.Enemies, e => Assert.True(e.IsAlive));
    }

    [Fact]
    public void AllEnemies_InsideMapBounds()
    {
        var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        foreach (var enemy in combat.Enemies)
        {
            Assert.InRange(enemy.X, -14.5, 14.5);
            Assert.InRange(enemy.Z, -14.5, 14.5);
        }
    }

    [Fact]
    public void PlayerAtOrigin_NoCombat()
    {
        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        Assert.Equal("Exploring", state);
        Assert.False(combat.InCombat);
    }

    [Fact]
    public void TeleportNearEnemy1_TriggersCombat()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);

        // Poll until combat triggers (encounter script runs on next game frame)
        string state = "";
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            state = GameQueryHelpers.GetGameState(_fixture.Context);
            if (state == "InCombat") break;
        }

        Assert.Equal("InCombat", state);
    }

    [Fact]
    public void TeleportFarFromEnemies_StaysExploring()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 0, 0.5, 0);
        _fixture.Context.HoldKey(VirtualKey.W, 50);
        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", state);
    }

    [Fact]
    public void CombatState_ShowsInCombat()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);

        GameQueryHelpers.CombatState combat = null!;
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            combat = GameQueryHelpers.GetCombatState(_fixture.Context);
            if (combat.InCombat) break;
        }

        Assert.True(combat.InCombat);
    }
}
