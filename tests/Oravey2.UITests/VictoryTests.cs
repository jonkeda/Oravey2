using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class VictoryTests : IAsyncLifetime
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
            if (GameQueryHelpers.GetCombatState(_fixture.Context).InCombat) break;
        }
    }

    [Fact]
    public void KillAllEnemies_TransitionsToExploring()
    {
        WaitForCombat();
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 100);
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_2");
        _fixture.Context.HoldKey(VirtualKey.Space, 100);
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_3");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", state);
    }

    [Fact]
    public void KillAllEnemies_ShowsVictoryOverlay()
    {
        WaitForCombat();
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_2");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_3");
        _fixture.Context.HoldKey(VirtualKey.Space, 100);

        var overlay = GameQueryHelpers.GetGameOverState(_fixture.Context);
        Assert.True(overlay.Visible);
        Assert.Equal("ENEMIES DEFEATED", overlay.Title);
    }

    [Fact]
    public void VictoryOverlay_AutoDismisses()
    {
        WaitForCombat();
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_2");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_3");

        // Wait for auto-dismiss (2 seconds + margin)
        // Use HoldKey as a frame-advancing sleep
        _fixture.Context.HoldKey(VirtualKey.Space, 3000);

        var overlay = GameQueryHelpers.GetGameOverState(_fixture.Context);
        Assert.False(overlay.Visible, "Victory overlay should auto-dismiss after 2 seconds");
    }

    [Fact]
    public void VictoryOverlay_EnemyBarsHide()
    {
        WaitForCombat();
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_2");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_3");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var bars = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        Assert.False(bars.Visible, "Enemy HP bars should hide after combat ends");
    }
}
