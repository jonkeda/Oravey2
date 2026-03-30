using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class DeterministicCombatTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void Player_Beats_SingleWeakEnemy()
    {
        GameQueryHelpers.ResetScenario(_fixture.Context);

        GameQueryHelpers.SpawnEnemy(_fixture.Context,
            id: "weak_1", x: 2, z: 0,
            hp: 15, weaponDamage: 1, weaponAccuracy: 0.20f);

        GameQueryHelpers.SetPlayerWeapon(_fixture.Context, damage: 20, accuracy: 0.95f);

        // Walk into trigger range
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 1, 0.5, 0);

        // Fight until combat ends
        for (int i = 0; i < 20; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 500);
            if (GameQueryHelpers.GetGameState(_fixture.Context) != "InCombat") break;
        }

        Assert.Equal("Exploring", GameQueryHelpers.GetGameState(_fixture.Context));

        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        var minHp = hud.MaxHp * 0.5;
        Assert.True(hud.Hp > minHp,
            $"Player should survive comfortably; HP={hud.Hp}, threshold={minHp}");
    }

    [Fact]
    public void Player_Dies_Against_OverpoweredEnemy()
    {
        GameQueryHelpers.ResetScenario(_fixture.Context);

        GameQueryHelpers.SpawnEnemy(_fixture.Context,
            id: "boss", x: 2, z: 0,
            hp: 999, weaponDamage: 80, weaponAccuracy: 0.99f);

        GameQueryHelpers.TeleportPlayer(_fixture.Context, 1, 0.5, 0);

        for (int i = 0; i < 20; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 500);
            var state = GameQueryHelpers.GetGameState(_fixture.Context);
            if (state != "InCombat") break;
        }

        Assert.Equal("GameOver", GameQueryHelpers.GetGameState(_fixture.Context));
    }

    [Fact]
    public void ThreeEnemyFight_PlayerSurvives()
    {
        GameQueryHelpers.ResetScenario(_fixture.Context);

        // Spawn 3 very weak enemies close to the player
        for (int i = 1; i <= 3; i++)
        {
            GameQueryHelpers.SpawnEnemy(_fixture.Context,
                id: $"balanced_{i}", x: 1 + i, z: 0,
                hp: 20, weaponDamage: 1, weaponAccuracy: 0.20f);
        }

        // Give player a very strong weapon (one-shot each enemy)
        GameQueryHelpers.SetPlayerWeapon(_fixture.Context, damage: 50, accuracy: 0.95f);

        // Walk into trigger range
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 1, 0.5, 0);

        // Fight until combat ends
        for (int i = 0; i < 40; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 500);
            if (GameQueryHelpers.GetGameState(_fixture.Context) != "InCombat") break;
        }

        Assert.Equal("Exploring", GameQueryHelpers.GetGameState(_fixture.Context));

        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.True(hud.Hp > 0, $"Player should survive; HP={hud.Hp}");
    }

    [Fact]
    public void ArmorReducesDamage_InScenario()
    {
        GameQueryHelpers.ResetScenario(_fixture.Context);

        // Spawn a strong enemy that always hits
        GameQueryHelpers.SpawnEnemy(_fixture.Context,
            id: "hitter", x: 2, z: 0,
            hp: 999, weaponDamage: 10, weaponAccuracy: 1.0f);

        // Equip leather armor (3 DR)
        GameQueryHelpers.EquipItem(_fixture.Context, "leather_jacket");

        // Record pre-combat HP
        var hudBefore = GameQueryHelpers.GetHudState(_fixture.Context);

        // Walk into range and let one round of combat happen
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 1, 0.5, 0);
        _fixture.Context.HoldKey(VirtualKey.Space, 500);
        _fixture.Context.HoldKey(VirtualKey.Space, 500);

        var hudAfter = GameQueryHelpers.GetHudState(_fixture.Context);
        var damageTaken = hudBefore.Hp - hudAfter.Hp;

        // With 10 base damage and 3 DR, each hit should deal at most 7
        // Without armor it would be 10 per hit
        Assert.True(damageTaken < 10 * 3,
            $"Armor should reduce damage; took {damageTaken} total damage");
    }
}
