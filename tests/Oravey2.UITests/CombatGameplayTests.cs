using Brinell.Stride.Communication;
using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Tests attack, damage, death, and combat-end transitions.
/// Uses TeleportPlayer + KillEnemy for deterministic control.
/// </summary>
public class CombatGameplayTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private GameQueryHelpers.CombatState WaitForCombat()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        GameQueryHelpers.CombatState combat = null!;
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            combat = GameQueryHelpers.GetCombatState(_fixture.Context);
            if (combat.InCombat) break;
        }
        return combat;
    }

    [Fact]
    public void PlayerCanAttack_DamagesEnemy()
    {
        // Teleport within WeaponRange (2 units) of enemy_1 at (8, 0.5, 8)
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 6.5, 0.5, 8);
        GameQueryHelpers.CombatState combat = null!;
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            combat = GameQueryHelpers.GetCombatState(_fixture.Context);
            if (combat.InCombat) break;
        }
        Assert.True(combat.InCombat, "Should enter combat");

        var initialHp = combat.Enemies.First(e => e.Id == "enemy_1").Hp;

        // Press Space 10 times with 500ms gaps for AP regen (75% hit chance — expect at least 1 hit)
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 500);
        }

        combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        var enemy1 = combat.Enemies.FirstOrDefault(e => e.Id == "enemy_1");

        // Enemy should have taken damage (or be dead and removed)
        if (enemy1 != null)
            Assert.True(enemy1.Hp < initialHp, "Enemy should have taken at least some damage");
        // If enemy1 is null, it was killed — that counts as damaged
    }

    [Fact]
    public void EnemiesAttackPlayer_OverTime()
    {
        var combat = WaitForCombat();
        Assert.True(combat.InCombat, "Should enter combat");

        var initialPlayerHp = combat.PlayerHp;

        // Idle for 4 seconds — enemies auto-attack
        _fixture.Context.HoldKey(VirtualKey.W, 4000);

        combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        Assert.True(combat.PlayerHp < initialPlayerHp, "Player should have taken damage from enemy attacks");
    }

    [Fact]
    public void KillEnemy_RemovesFromList()
    {
        var combat = WaitForCombat();
        Assert.True(combat.InCombat, "Should enter combat");

        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");

        // Advance frames for cleanup
        _fixture.Context.HoldKey(VirtualKey.Space, 100);

        combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        Assert.Equal(2, combat.EnemyCount);
        Assert.DoesNotContain(combat.Enemies, e => e.Id == "enemy_1");
    }

    [Fact]
    public void KillEnemy_EntityRemovedFromScene()
    {
        var combat = WaitForCombat();
        Assert.True(combat.InCombat, "Should enter combat");

        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 100);

        // GetEntityPosition should fail for a removed entity
        var response = _fixture.Context.SendCommand(
            AutomationCommand.GameQuery("GetEntityPosition", "enemy_1"));
        Assert.False(response.Success, "Entity should have been removed from scene");
    }

    [Fact]
    public void AllEnemiesDead_ReturnsToExploring()
    {
        var combat = WaitForCombat();
        Assert.True(combat.InCombat, "Should enter combat");

        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_2");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_3");

        // Advance frames for cleanup
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", state);
    }

    [Fact]
    public void AllEnemiesDead_CombatStateReset()
    {
        var combat = WaitForCombat();
        Assert.True(combat.InCombat, "Should enter combat");

        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_2");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_3");

        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        Assert.False(combat.InCombat);
        Assert.Equal(0, combat.EnemyCount);
    }
}
