using Oravey2.Core.Combat;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.State;

namespace Oravey2.Tests.Combat;

public class CombatStateManagerTests
{
    private (CombatStateManager csm, EventBus bus, GameStateManager gsm) Create()
    {
        var bus = new EventBus();
        var gsm = new GameStateManager(bus);
        gsm.TransitionTo(GameState.Exploring); // Loading → Exploring
        return (new CombatStateManager(bus, gsm), bus, gsm);
    }

    [Fact]
    public void InitialState_NotInCombat()
    {
        var (csm, _, _) = Create();
        Assert.False(csm.InCombat);
    }

    [Fact]
    public void EnterCombat_SetsInCombat()
    {
        var (csm, _, _) = Create();
        Assert.True(csm.EnterCombat(["enemy1"]));
        Assert.True(csm.InCombat);
    }

    [Fact]
    public void EnterCombat_PublishesEvent()
    {
        var (csm, bus, _) = Create();
        CombatStartedEvent? received = null;
        bus.Subscribe<CombatStartedEvent>(e => received = e);

        csm.EnterCombat(["enemy1", "enemy2"]);

        Assert.NotNull(received);
        Assert.Equal(2, received.Value.EnemyIds.Length);
    }

    [Fact]
    public void EnterCombat_TransitionsGameState()
    {
        var (csm, _, gsm) = Create();
        csm.EnterCombat(["enemy1"]);
        Assert.Equal(GameState.InCombat, gsm.CurrentState);
    }

    [Fact]
    public void EnterCombat_TracksCombatants()
    {
        var (csm, _, _) = Create();
        csm.EnterCombat(["e1", "e2"]);
        Assert.Equal(2, csm.Combatants.Count);
        Assert.Contains("e1", csm.Combatants);
        Assert.Contains("e2", csm.Combatants);
    }

    [Fact]
    public void EnterCombat_Empty_ReturnsFalse()
    {
        var (csm, _, _) = Create();
        Assert.False(csm.EnterCombat([]));
        Assert.False(csm.InCombat);
    }

    [Fact]
    public void EnterCombat_AlreadyInCombat_ReturnsFalse()
    {
        var (csm, _, _) = Create();
        csm.EnterCombat(["e1"]);
        Assert.False(csm.EnterCombat(["e2"]));
    }

    [Fact]
    public void ExitCombat_ClearsState()
    {
        var (csm, _, _) = Create();
        csm.EnterCombat(["e1"]);
        Assert.True(csm.ExitCombat());
        Assert.False(csm.InCombat);
        Assert.Empty(csm.Combatants);
    }

    [Fact]
    public void ExitCombat_PublishesEvent()
    {
        var (csm, bus, _) = Create();
        csm.EnterCombat(["e1"]);

        bool received = false;
        bus.Subscribe<CombatEndedEvent>(_ => received = true);
        csm.ExitCombat();

        Assert.True(received);
    }

    [Fact]
    public void ExitCombat_TransitionsToExploring()
    {
        var (csm, _, gsm) = Create();
        csm.EnterCombat(["e1"]);
        csm.ExitCombat();
        Assert.Equal(GameState.Exploring, gsm.CurrentState);
    }

    [Fact]
    public void ExitCombat_NotInCombat_ReturnsFalse()
    {
        var (csm, _, _) = Create();
        Assert.False(csm.ExitCombat());
    }

    [Fact]
    public void RemoveCombatant_ReducesList()
    {
        var (csm, _, _) = Create();
        csm.EnterCombat(["e1", "e2"]);
        csm.RemoveCombatant("e1");
        Assert.Single(csm.Combatants);
        Assert.DoesNotContain("e1", csm.Combatants);
    }

    [Fact]
    public void RemoveCombatant_LastEnemy_AutoExits()
    {
        var (csm, _, gsm) = Create();
        csm.EnterCombat(["e1"]);
        csm.RemoveCombatant("e1");
        Assert.False(csm.InCombat);
        Assert.Equal(GameState.Exploring, gsm.CurrentState);
    }

    [Fact]
    public void RemoveCombatant_Unknown_NoEffect()
    {
        var (csm, _, _) = Create();
        csm.EnterCombat(["e1"]);
        csm.RemoveCombatant("nonexistent");
        Assert.True(csm.InCombat);
        Assert.Single(csm.Combatants);
    }
}
