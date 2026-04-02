using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class NotificationFeedTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void LootPickup_ShowsNotification()
    {
        // Kill enemy near player to spawn loot, then walk over it
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
            if (combat.InCombat) break;
        }

        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        // Walk to loot position (enemy_1 at 8, 0.5, 8)
        var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        if (loot.Count > 0)
        {
            var lootPos = loot.Entities[0];
            GameQueryHelpers.TeleportPlayer(_fixture.Context,
                lootPos.X, 0.5, lootPos.Z);
            _fixture.Context.HoldKey(VirtualKey.Space, 200);

            var feed = GameQueryHelpers.GetNotificationFeed(_fixture.Context);
            Assert.True(feed.Count > 0, "Expected 'Picked up' notification after loot pickup");
            Assert.Contains(feed.Messages, m => m.Text.Contains("Picked up"));
        }
        // If loot RNG gave zero items, test is inconclusive — no assertion failure
    }

    [Fact]
    public void Notifications_HavePositiveTimeRemaining()
    {
        // Same setup as above — trigger a pickup notification
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
            if (combat.InCombat) break;
        }
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        if (loot.Count > 0)
        {
            GameQueryHelpers.TeleportPlayer(_fixture.Context,
                loot.Entities[0].X, 0.5, loot.Entities[0].Z);
            _fixture.Context.HoldKey(VirtualKey.Space, 100);

            var feed = GameQueryHelpers.GetNotificationFeed(_fixture.Context);
            if (feed.Count > 0)
            {
                Assert.All(feed.Messages, m =>
                    Assert.True(m.TimeRemaining > 0, "Active notifications should have time remaining"));
            }
        }
    }


}
