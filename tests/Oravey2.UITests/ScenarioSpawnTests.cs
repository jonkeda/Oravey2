using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class ScenarioSpawnTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void SpawnEnemy_CreatesEnemyAtPosition()
    {
        GameQueryHelpers.ResetScenario(_fixture.Context);

        var diagBefore = GameQueryHelpers.GetSceneDiagnostics(_fixture.Context);

        var result = GameQueryHelpers.SpawnEnemy(_fixture.Context,
            id: "test_1", x: 3, z: 0, hp: 50);

        Assert.True(result.Success);
        Assert.Equal("test_1", result.Id);
        Assert.Equal(50, result.Hp);

        var diagAfter = GameQueryHelpers.GetSceneDiagnostics(_fixture.Context);
        Assert.True(diagAfter.TotalEntities > diagBefore.TotalEntities,
            $"Entity count should increase: before={diagBefore.TotalEntities}, after={diagAfter.TotalEntities}");
    }

    [Fact]
    public void SpawnEnemy_CustomWeaponStats()
    {
        GameQueryHelpers.ResetScenario(_fixture.Context);

        GameQueryHelpers.SpawnEnemy(_fixture.Context,
            id: "custom_wpn", x: 3, z: 0,
            weaponDamage: 10, weaponAccuracy: 0.80f);

        var config = GameQueryHelpers.GetCombatConfig(_fixture.Context);
        Assert.Equal(10, config.Enemy.Damage);
        Assert.Equal(0.80f, config.Enemy.Accuracy, 0.01f);
    }

    [Fact]
    public void SpawnEnemy_MultipleEnemies()
    {
        GameQueryHelpers.ResetScenario(_fixture.Context);

        var r1 = GameQueryHelpers.SpawnEnemy(_fixture.Context, id: "multi_1", x: 3, z: 0);
        var r2 = GameQueryHelpers.SpawnEnemy(_fixture.Context, id: "multi_2", x: 0, z: 3);
        var r3 = GameQueryHelpers.SpawnEnemy(_fixture.Context, id: "multi_3", x: -3, z: 0);

        Assert.True(r1.Success);
        Assert.True(r2.Success);
        Assert.True(r3.Success);

        var combatState = GameQueryHelpers.GetCombatState(_fixture.Context);
        Assert.Equal(3, combatState.EnemyCount);
    }

    [Fact]
    public void SetPlayerStats_UpdatesMaxHp()
    {
        var hudBefore = GameQueryHelpers.GetHudState(_fixture.Context);
        var baselineMaxHp = hudBefore.MaxHp;

        var result = GameQueryHelpers.SetPlayerStats(_fixture.Context, endurance: 8);

        Assert.True(result.Success);
        Assert.True(result.MaxHp > baselineMaxHp,
            $"MaxHP should increase with Endurance 8: was {baselineMaxHp}, now {result.MaxHp}");

        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(result.MaxHp, hud.MaxHp);
        Assert.Equal(result.Hp, hud.Hp);
    }

    [Fact]
    public void SetPlayerWeapon_EquipsCustomWeapon()
    {
        var result = GameQueryHelpers.SetPlayerWeapon(_fixture.Context,
            damage: 25, accuracy: 0.90f);

        Assert.True(result.Success);
        Assert.Equal(25, result.Damage);
        Assert.Equal(0.90f, result.Accuracy, 0.01f);

        var config = GameQueryHelpers.GetCombatConfig(_fixture.Context);
        Assert.Equal(25, config.Player.Damage);
        Assert.Equal(0.90f, config.Player.Accuracy, 0.01f);
    }
}
