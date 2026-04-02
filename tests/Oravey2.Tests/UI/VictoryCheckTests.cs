using Oravey2.Core.UI;
using Oravey2.Core.World;

namespace Oravey2.Tests.UI;

public class VictoryCheckTests
{
    [Fact]
    public void VictoryAchieved_FlagFalse_ReturnsFalse()
    {
        var worldState = new WorldStateService();
        var script = new VictoryCheckScript { WorldState = worldState };

        Assert.False(script.VictoryAchieved);
    }

    [Fact]
    public void VictoryAchieved_FlagTrue_ReturnsTrue()
    {
        var worldState = new WorldStateService();
        worldState.SetFlag("m1_complete", true);
        var script = new VictoryCheckScript { WorldState = worldState };

        Assert.True(script.VictoryAchieved);
    }

    [Fact]
    public void VictoryCheckScript_InitialState_NotShowingOverlay()
    {
        var script = new VictoryCheckScript();

        Assert.False(script.IsShowingOverlay);
    }
}
