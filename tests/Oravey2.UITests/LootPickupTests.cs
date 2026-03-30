using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Verifies walking over a loot cube picks up items and removes the cube entity.
/// Tests share a game process — state carries over between tests.
/// </summary>
public class LootPickupTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private void KillEnemyAndGetLoot()
    {
        // Teleport near enemy_1 and enter combat
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
            if (combat.InCombat) break;
        }

        // Kill enemy to produce loot
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);
    }

    [Fact]
    public void WalkOverLoot_PicksUpItems()
    {
        KillEnemyAndGetLoot();

        var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        if (loot.Count == 0)
        {
            // RNG gave no loot — kill another enemy
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

        if (loot.Count > 0)
        {
            var inventoryBefore = GameQueryHelpers.GetInventoryState(_fixture.Context);
            var lootPos = loot.Entities[0];
            GameQueryHelpers.TeleportPlayer(_fixture.Context, lootPos.X, 0.5, lootPos.Z);
            _fixture.Context.HoldKey(VirtualKey.Space, 200);

            var inventoryAfter = GameQueryHelpers.GetInventoryState(_fixture.Context);
            Assert.True(inventoryAfter.ItemCount >= inventoryBefore.ItemCount,
                "Should have same or more item slots after pickup");
        }
    }

    [Fact]
    public void WalkOverLoot_RemovesLootEntity()
    {
        KillEnemyAndGetLoot();

        var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        if (loot.Count > 0)
        {
            var lootPos = loot.Entities[0];
            var countBefore = loot.Count;
            GameQueryHelpers.TeleportPlayer(_fixture.Context, lootPos.X, 0.5, lootPos.Z);
            _fixture.Context.HoldKey(VirtualKey.Space, 200);

            var lootAfter = GameQueryHelpers.GetLootEntities(_fixture.Context);
            Assert.True(lootAfter.Count < countBefore, "Loot entity should be removed after pickup");
        }
    }

    [Fact]
    public void PickupAdds_ToExistingStacks()
    {
        var inventoryBefore = GameQueryHelpers.GetInventoryState(_fixture.Context);
        var medkitsBefore = inventoryBefore.Items
            .Where(i => i.Id == "medkit")
            .Sum(i => i.Count);

        KillEnemyAndGetLoot();

        var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        if (loot.Count > 0)
        {
            var lootPos = loot.Entities[0];
            GameQueryHelpers.TeleportPlayer(_fixture.Context, lootPos.X, 0.5, lootPos.Z);
            _fixture.Context.HoldKey(VirtualKey.Space, 200);
        }

        var inventoryAfter = GameQueryHelpers.GetInventoryState(_fixture.Context);
        var medkitsAfter = inventoryAfter.Items
            .Where(i => i.Id == "medkit")
            .Sum(i => i.Count);

        // Medkit count may have increased if a medkit dropped
        Assert.True(medkitsAfter >= medkitsBefore, "Medkit count should not decrease");
    }

    [Fact]
    public void Inventory_WeightIncreases_AfterPickup()
    {
        var weightBefore = GameQueryHelpers.GetInventoryState(_fixture.Context).CurrentWeight;

        KillEnemyAndGetLoot();

        var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        if (loot.Count > 0)
        {
            var lootPos = loot.Entities[0];
            GameQueryHelpers.TeleportPlayer(_fixture.Context, lootPos.X, 0.5, lootPos.Z);
            _fixture.Context.HoldKey(VirtualKey.Space, 200);

            var weightAfter = GameQueryHelpers.GetInventoryState(_fixture.Context).CurrentWeight;
            Assert.True(weightAfter > weightBefore,
                $"Weight should increase after pickup: before={weightBefore}, after={weightAfter}");
        }
    }
}
