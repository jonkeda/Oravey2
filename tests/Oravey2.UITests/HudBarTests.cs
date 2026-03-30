using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class HudBarTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void HudState_FullHealth_AtStart()
    {
        // 105 HP = 50 + 5*10 + 1*5
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(105, hud.Hp);
        Assert.Equal(105, hud.MaxHp);
    }

    [Fact]
    public void HudState_ReducedHp_AfterDamage()
    {
        var dmg = GameQueryHelpers.DamagePlayer(_fixture.Context, 30);
        Assert.Equal(75, dmg.NewHp);

        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(75, hud.Hp);
        Assert.Equal(105, hud.MaxHp);
    }

    [Fact]
    public void HudState_FullAp_AtStart()
    {
        // 10 AP = 8 + 5/2
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(10, hud.Ap);
        Assert.Equal(10, hud.MaxAp);
    }

    [Fact]
    public void HudState_LevelOne_AtStart()
    {
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(1, hud.Level);
    }

    [Fact]
    public void HudState_ShowsExploring_AtStart()
    {
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal("Exploring", hud.GameState);
    }
}
