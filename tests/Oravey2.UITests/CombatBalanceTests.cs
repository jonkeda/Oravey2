using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class CombatBalanceTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void PlayerWeapon_MatchesPipeWrench()
    {
        var config = GameQueryHelpers.GetCombatConfig(_fixture.Context);
        Assert.Equal(14, config.Player.Damage);
        Assert.Equal(0.80f, config.Player.Accuracy, 0.01f);
        Assert.Equal(2.0f, config.Player.Range, 0.1f);
        Assert.Equal(3, config.Player.ApCost);
    }

    [Fact]
    public void EnemyWeapon_MatchesRustyShiv()
    {
        var config = GameQueryHelpers.GetCombatConfig(_fixture.Context);
        Assert.Equal(4, config.Enemy.Damage);
        Assert.Equal(0.50f, config.Enemy.Accuracy, 0.01f);
        Assert.Equal(1.5f, config.Enemy.Range, 0.1f);
        Assert.Equal(3, config.Enemy.ApCost);
    }

    [Fact]
    public void EnemyWeapon_WeakerThanPlayer()
    {
        var config = GameQueryHelpers.GetCombatConfig(_fixture.Context);
        Assert.True(config.Enemy.Damage < config.Player.Damage,
            $"Enemy damage ({config.Enemy.Damage}) should be less than player ({config.Player.Damage})");
        Assert.True(config.Enemy.Accuracy < config.Player.Accuracy,
            $"Enemy accuracy ({config.Enemy.Accuracy}) should be less than player ({config.Player.Accuracy})");
    }

    [Fact]
    public void MeleeDistance_IsZero()
    {
        var config = GameQueryHelpers.GetCombatConfig(_fixture.Context);
        Assert.Equal(0f, config.MeleeDistance, 0.01f);
    }

    [Fact]
    public void EnemyHp_IsRebalanced()
    {
        // Enemies should have 65 HP (Endurance=1) not 95 (Endurance=4)
        var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        foreach (var enemy in combat.Enemies)
        {
            Assert.Equal(65, enemy.MaxHp);
        }
    }

    [Fact]
    public void FullCombat_PlayerSurvives()
    {
        // Trigger combat
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            if (GameQueryHelpers.GetGameState(_fixture.Context) == "InCombat") break;
        }

        // Attack continuously in chunks (pipe timeout ~10s, combat ~28s expected)
        for (int i = 0; i < 6; i++)
        {
            var state = GameQueryHelpers.GetGameState(_fixture.Context);
            if (state != "InCombat") break;
            _fixture.Context.HoldKey(VirtualKey.Space, 8000);
        }

        var finalState = GameQueryHelpers.GetGameState(_fixture.Context);
        // Player should win (Exploring) rather than die (GameOver)
        Assert.Equal("Exploring", finalState);
    }
}
