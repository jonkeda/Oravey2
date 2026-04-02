using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class CombatBalanceTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

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
