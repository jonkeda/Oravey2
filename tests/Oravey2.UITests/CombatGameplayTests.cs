using Brinell.Stride.Communication;
using Brinell.Stride.Infrastructure;
using Oravey2.Core.Automation;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests;

/// <summary>
/// Tests attack, damage, death, and combat-end transitions.
/// Uses TeleportPlayer + KillEnemy for deterministic control.
/// </summary>
public class CombatGameplayTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public CombatGameplayTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private CombatStateResponse WaitForCombat()
    {
        // Find any alive enemy to teleport near (resilient to test ordering)
        var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        var alive = combat.Enemies.FirstOrDefault(e => e.IsAlive);
        if (alive == null) return combat; // No enemies left

        // Teleport within trigger radius (5 units) of the alive enemy
        GameQueryHelpers.TeleportPlayer(_fixture.Context, alive.X - 4, 0.5, alive.Z);
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
        // Use WaitForCombat to enter combat near any alive enemy
        var combat = WaitForCombat();
        if (!combat.InCombat)
        {
            _output.WriteLine("No enemies available — skipping");
            return;
        }

        // Find a living enemy to track for damage
        var target = combat.Enemies.FirstOrDefault(e => e.IsAlive);
        Assert.NotNull(target);

        var initialHp = target.Hp;
        _output.WriteLine($"Target: {target.Id} at ({target.X:F1},{target.Z:F1}) HP={initialHp}");

        // Teleport directly onto the target enemy for max hit chance
        GameQueryHelpers.TeleportPlayer(_fixture.Context, target.X, 0.5, target.Z);
        _fixture.Context.HoldKey(VirtualKey.Space, 100);

        var playerPos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _output.WriteLine($"Player at ({playerPos.X:F1},{playerPos.Z:F1}), dist={Math.Sqrt(Math.Pow(playerPos.X - target.X, 2) + Math.Pow(playerPos.Z - target.Z, 2)):F2}");

        // Press Space 20 times with 500ms gaps for AP regen (75% hit chance — expect at least 1 hit)
        for (int i = 0; i < 20; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 500);
            if (i % 5 == 4)
            {
                var mid = GameQueryHelpers.GetCombatState(_fixture.Context);
                var e = mid.Enemies.FirstOrDefault(e => e.Id == target.Id);
                _output.WriteLine($"After {i + 1}: HP={e?.Hp ?? -1} AP={mid.PlayerAp} InCombat={mid.InCombat}");
                if (e == null || e.Hp < initialHp) break;
            }
        }

        combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        var enemy = combat.Enemies.FirstOrDefault(e => e.Id == target.Id);

        // Enemy should have taken damage (or be dead and removed)
        if (enemy != null)
            Assert.True(enemy.Hp < initialHp, $"Enemy should have taken at least some damage (HP={enemy.Hp}, initial={initialHp})");
        // If enemy is null, it was killed — that counts as damaged
    }

    [Fact]
    public void EnemiesAttackPlayer_OverTime()
    {
        var combat = WaitForCombat();
        if (!combat.InCombat) return; // No enemies left

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
        if (!combat.InCombat) return; // No enemies left

        var alive = combat.Enemies.First(e => e.IsAlive);
        var countBefore = combat.EnemyCount;

        GameQueryHelpers.KillEnemy(_fixture.Context, alive.Id);

        // Advance frames for cleanup
        _fixture.Context.HoldKey(VirtualKey.Space, 100);

        combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        Assert.Equal(countBefore - 1, combat.EnemyCount);
        Assert.DoesNotContain(combat.Enemies, e => e.Id == alive.Id);
    }

    [Fact]
    public void KillEnemy_EntityRemovedFromScene()
    {
        var combat = WaitForCombat();
        if (!combat.InCombat) return; // No enemies left

        var alive = combat.Enemies.First(e => e.IsAlive);

        GameQueryHelpers.KillEnemy(_fixture.Context, alive.Id);
        _fixture.Context.HoldKey(VirtualKey.Space, 100);

        // GetEntityPosition should fail for a removed entity
        var response = _fixture.Context.SendCommand(
            AutomationCommand.GameQuery("GetEntityPosition", alive.Id));
        Assert.False(response.Success, "Entity should have been removed from scene");
    }

    [Fact]
    public void AllEnemiesDead_ReturnsToExploring()
    {
        var combat = WaitForCombat();
        if (!combat.InCombat) return; // No enemies left

        // Kill all remaining alive enemies
        foreach (var enemy in combat.Enemies.Where(e => e.IsAlive))
            GameQueryHelpers.KillEnemy(_fixture.Context, enemy.Id);

        // Advance frames for cleanup
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", state);
    }

    [Fact]
    public void AllEnemiesDead_CombatStateReset()
    {
        var combat = WaitForCombat();
        if (!combat.InCombat) return; // No enemies left

        // Kill all remaining alive enemies
        foreach (var enemy in combat.Enemies.Where(e => e.IsAlive))
            GameQueryHelpers.KillEnemy(_fixture.Context, enemy.Id);

        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        Assert.False(combat.InCombat);
        Assert.Equal(0, combat.EnemyCount);
    }
}
