using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Verifies the HUD data matches live game state.
/// </summary>
public class HudStateTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    [Trait("Category", "Smoke")]
    public void HudState_HasFullHealth_AtStart()
    {
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(hud.MaxHp, hud.Hp);
        Assert.True(hud.MaxHp > 0, "MaxHP should be positive");
    }

    [Fact]
    public void HudState_ShowsExploring_AtStart()
    {
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal("Exploring", hud.GameState);
    }

    [Fact]
    public void HudState_ShowsLevel1_AtStart()
    {
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(1, hud.Level);
    }

    [Fact]
    public void HudState_ApMatches_MaxAp()
    {
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(hud.MaxAp, hud.Ap);
        Assert.True(hud.MaxAp > 0, "MaxAP should be positive");
    }

    [Fact]
    public void HudState_ShowsInCombat_WhenFighting()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);

        string gameState = "";
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            var hud = GameQueryHelpers.GetHudState(_fixture.Context);
            gameState = hud.GameState;
            if (gameState == "InCombat") break;
        }

        Assert.Equal("InCombat", gameState);
    }

    [Fact]
    public void HudState_HealthDecreases_InCombat()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);

        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
            if (combat.InCombat) break;
        }

        var hudBefore = GameQueryHelpers.GetHudState(_fixture.Context);

        // Idle for 4 seconds — enemies auto-attack
        _fixture.Context.HoldKey(VirtualKey.W, 4000);

        var hudAfter = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.True(hudAfter.Hp < hudBefore.Hp,
            $"Player should take damage: before={hudBefore.Hp}, after={hudAfter.Hp}");
    }
}
