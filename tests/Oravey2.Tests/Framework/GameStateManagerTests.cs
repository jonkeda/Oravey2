using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.State;
using Xunit;

namespace Oravey2.Tests.Framework;

public class GameStateManagerTests
{
    [Fact]
    public void Initial_state_is_Loading()
    {
        var bus = new EventBus();
        var mgr = new GameStateManager(bus);
        Assert.Equal(GameState.Loading, mgr.CurrentState);
    }

    [Fact]
    public void Valid_transition_Loading_to_Exploring()
    {
        var bus = new EventBus();
        var mgr = new GameStateManager(bus);
        GameStateChangedEvent? received = null;
        bus.Subscribe<GameStateChangedEvent>(e => received = e);

        var result = mgr.TransitionTo(GameState.Exploring);

        Assert.True(result);
        Assert.Equal(GameState.Exploring, mgr.CurrentState);
        Assert.NotNull(received);
        Assert.Equal(GameState.Loading, received.Value.OldState);
        Assert.Equal(GameState.Exploring, received.Value.NewState);
    }

    [Fact]
    public void Invalid_transition_returns_false()
    {
        var bus = new EventBus();
        var mgr = new GameStateManager(bus);

        // Loading -> InCombat is not valid
        var result = mgr.TransitionTo(GameState.InCombat);

        Assert.False(result);
        Assert.Equal(GameState.Loading, mgr.CurrentState);
    }

    [Fact]
    public void Transition_to_same_state_returns_false()
    {
        var bus = new EventBus();
        var mgr = new GameStateManager(bus);

        var result = mgr.TransitionTo(GameState.Loading);

        Assert.False(result);
    }

    [Fact]
    public void Exploring_to_Combat_and_back()
    {
        var bus = new EventBus();
        var mgr = new GameStateManager(bus);
        mgr.TransitionTo(GameState.Exploring);

        Assert.True(mgr.TransitionTo(GameState.InCombat));
        Assert.Equal(GameState.InCombat, mgr.CurrentState);

        Assert.True(mgr.TransitionTo(GameState.Exploring));
        Assert.Equal(GameState.Exploring, mgr.CurrentState);
    }

    [Fact]
    public void Exploring_to_Dialogue_and_back()
    {
        var bus = new EventBus();
        var mgr = new GameStateManager(bus);
        mgr.TransitionTo(GameState.Exploring);

        Assert.True(mgr.TransitionTo(GameState.InDialogue));
        Assert.True(mgr.TransitionTo(GameState.Exploring));
    }

    [Fact]
    public void Exploring_to_Paused_and_back()
    {
        var bus = new EventBus();
        var mgr = new GameStateManager(bus);
        mgr.TransitionTo(GameState.Exploring);

        Assert.True(mgr.TransitionTo(GameState.Paused));
        Assert.True(mgr.TransitionTo(GameState.Exploring));
    }
}
