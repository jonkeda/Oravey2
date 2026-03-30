using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class EnemyHpBarTests : IAsyncLifetime
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
    public void EnemyBars_NotVisible_WhenExploring()
    {
        var bars = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        Assert.False(bars.Visible);
    }

    [Fact]
    public void EnemyBars_Visible_WhenInCombat()
    {
        WaitForCombat();
        var bars = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        Assert.True(bars.Visible);
    }

    [Fact]
    public void EnemyBars_ShowAllLivingEnemies()
    {
        WaitForCombat();
        var bars = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        var combat = GameQueryHelpers.GetCombatState(_fixture.Context);

        var aliveCount = combat.Enemies.Count(e => e.IsAlive);
        Assert.Equal(aliveCount, bars.Bars.Count);
    }

    [Fact]
    public void EnemyBars_MatchCombatState_Hp()
    {
        WaitForCombat();
        var bars = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        var combat = GameQueryHelpers.GetCombatState(_fixture.Context);

        foreach (var bar in bars.Bars)
        {
            var enemy = combat.Enemies.FirstOrDefault(e => e.Id == bar.EnemyId);
            Assert.NotNull(enemy);
            Assert.Equal(enemy.Hp, bar.Hp);
            Assert.Equal(enemy.MaxHp, bar.MaxHp);
        }
    }

    [Fact]
    public void EnemyBars_RemoveDeadEnemy()
    {
        WaitForCombat();
        var barsBefore = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        int countBefore = barsBefore.Bars.Count;

        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var barsAfter = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        Assert.True(barsAfter.Bars.Count < countBefore,
            $"Expected fewer bars after kill: before={countBefore}, after={barsAfter.Bars.Count}");
        Assert.DoesNotContain(barsAfter.Bars, b => b.EnemyId == "enemy_1");
    }
}
