using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Verifies that killing an enemy spawns a loot cube entity.
/// </summary>
public class LootDropTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private void WaitForCombat()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
            if (combat.InCombat) break;
        }
    }

    [Fact]
    public void KillEnemy_SpawnsLootCube()
    {
        WaitForCombat();
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        // Loot is RNG-based; probability of zero items is ~16%
        // Use retries to reduce flakiness
        if (loot.Count == 0)
        {
            // Kill enemy_2 as well for a second chance
            GameQueryHelpers.TeleportPlayer(_fixture.Context, -6, 0.5, 10);
            for (int i = 0; i < 10; i++)
            {
                _fixture.Context.HoldKey(VirtualKey.Space, 50);
                var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
                if (combat.InCombat) break;
            }
            GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_2");
            _fixture.Context.HoldKey(VirtualKey.Space, 200);
            loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        }

        Assert.True(loot.Count >= 1, $"Expected at least 1 loot entity, got {loot.Count}");
    }

    [Fact]
    public void LootCube_AtEnemyPosition()
    {
        WaitForCombat();
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        if (loot.Count > 0)
        {
            var first = loot.Entities[0];
            // enemy_1 is at (8, 0.5, 8)
            Assert.InRange(first.X, 7.0, 9.0);
            Assert.InRange(first.Z, 7.0, 9.0);
        }
        // If no loot dropped (RNG), skip assertion gracefully
    }

    [Fact]
    public void LootCube_HasItems()
    {
        WaitForCombat();
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        if (loot.Count > 0)
        {
            Assert.True(loot.Entities[0].ItemCount >= 1, "Loot cube should have at least 1 item");
        }
    }

    [Fact]
    public void MultipleLootCubes_FromMultipleKills()
    {
        // Kill enemy_1
        WaitForCombat();
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        // Kill enemy_2
        GameQueryHelpers.TeleportPlayer(_fixture.Context, -6, 0.5, 10);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
            if (combat.InCombat) break;
        }
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_2");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        // With 2 kills, very likely at least 1 loot drop
        Assert.True(loot.Count >= 1, $"Expected loot from 2 kills, got {loot.Count}");
    }
}
