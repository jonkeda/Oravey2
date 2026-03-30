using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class InputFreezeTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private void ForceGameOver()
    {
        // Enter combat
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            if (GameQueryHelpers.GetGameState(_fixture.Context) == "InCombat") break;
        }

        // Kill the player
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        GameQueryHelpers.DamagePlayer(_fixture.Context, hud.Hp);
        _fixture.Context.HoldKey(VirtualKey.Space, 100);
    }

    [Fact]
    public void Movement_Blocked_DuringGameOver()
    {
        ForceGameOver();
        Assert.Equal("GameOver", GameQueryHelpers.GetGameState(_fixture.Context));

        var before = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _fixture.Context.HoldKey(VirtualKey.W, 500);
        var after = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        // Position should not change
        Assert.Equal(before.X, after.X, 0.1);
        Assert.Equal(before.Z, after.Z, 0.1);
    }

    [Fact]
    public void InventoryToggle_Blocked_DuringGameOver()
    {
        ForceGameOver();

        _fixture.Context.PressKey(VirtualKey.Tab);
        var visible = GameQueryHelpers.GetInventoryOverlayVisible(_fixture.Context);
        Assert.False(visible, "Inventory should not open during GameOver");
    }

    [Fact]
    public void GameState_StaysGameOver_AfterInput()
    {
        ForceGameOver();

        _fixture.Context.HoldKey(VirtualKey.W, 200);
        _fixture.Context.PressKey(VirtualKey.Tab);
        _fixture.Context.PressKey(VirtualKey.Space);

        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("GameOver", state);
    }
}
