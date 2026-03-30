using Oravey2.Core.Audio;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.State;

namespace Oravey2.Tests.Audio;

public class MusicLayerResolverTests
{
    private readonly EventBus _bus = new();
    private readonly MusicLayerResolver _resolver;

    public MusicLayerResolverTests()
    {
        _resolver = new MusicLayerResolver(_bus);
    }

    [Fact]
    public void BaseLayerActiveByDefault()
    {
        Assert.Contains(MusicLayer.Base, _resolver.ActiveLayers);
        Assert.Single(_resolver.ActiveLayers);
    }

    [Fact]
    public void ExploringActivatesExploration()
    {
        _resolver.Resolve(GameState.Exploring, enemiesNearby: false, inIrradiatedZone: false);

        Assert.Contains(MusicLayer.Base, _resolver.ActiveLayers);
        Assert.Contains(MusicLayer.Exploration, _resolver.ActiveLayers);
        Assert.Equal(2, _resolver.ActiveLayers.Count);
    }

    [Fact]
    public void InCombatActivatesCombat()
    {
        _resolver.Resolve(GameState.InCombat, enemiesNearby: false, inIrradiatedZone: false);

        Assert.Contains(MusicLayer.Base, _resolver.ActiveLayers);
        Assert.Contains(MusicLayer.Combat, _resolver.ActiveLayers);
        Assert.DoesNotContain(MusicLayer.Exploration, _resolver.ActiveLayers);
    }

    [Fact]
    public void EnemiesNearbyActivatesTension()
    {
        _resolver.Resolve(GameState.Exploring, enemiesNearby: true, inIrradiatedZone: false);

        Assert.Contains(MusicLayer.Tension, _resolver.ActiveLayers);
        Assert.Contains(MusicLayer.Exploration, _resolver.ActiveLayers);
        Assert.Contains(MusicLayer.Base, _resolver.ActiveLayers);
    }

    [Fact]
    public void TensionNotActiveDuringCombat()
    {
        _resolver.Resolve(GameState.InCombat, enemiesNearby: true, inIrradiatedZone: false);

        Assert.DoesNotContain(MusicLayer.Tension, _resolver.ActiveLayers);
        Assert.Contains(MusicLayer.Combat, _resolver.ActiveLayers);
    }

    [Fact]
    public void IrradiatedZoneActivatesEerie()
    {
        _resolver.Resolve(GameState.Exploring, enemiesNearby: false, inIrradiatedZone: true);

        Assert.Contains(MusicLayer.Eerie, _resolver.ActiveLayers);
    }

    [Fact]
    public void CombatPlusIrradiated()
    {
        _resolver.Resolve(GameState.InCombat, enemiesNearby: false, inIrradiatedZone: true);

        Assert.Contains(MusicLayer.Base, _resolver.ActiveLayers);
        Assert.Contains(MusicLayer.Combat, _resolver.ActiveLayers);
        Assert.Contains(MusicLayer.Eerie, _resolver.ActiveLayers);
        Assert.Equal(3, _resolver.ActiveLayers.Count);
    }

    [Fact]
    public void AllFlagsActiveInExploring()
    {
        _resolver.Resolve(GameState.Exploring, enemiesNearby: true, inIrradiatedZone: true);

        Assert.Contains(MusicLayer.Base, _resolver.ActiveLayers);
        Assert.Contains(MusicLayer.Exploration, _resolver.ActiveLayers);
        Assert.Contains(MusicLayer.Tension, _resolver.ActiveLayers);
        Assert.Contains(MusicLayer.Eerie, _resolver.ActiveLayers);
        Assert.Equal(4, _resolver.ActiveLayers.Count);
    }

    [Fact]
    public void TransitionFromCombatToExploring()
    {
        _resolver.Resolve(GameState.InCombat, enemiesNearby: false, inIrradiatedZone: false);

        var events = new List<IGameEvent>();
        _bus.Subscribe<MusicLayerDeactivatedEvent>(e => events.Add(e));
        _bus.Subscribe<MusicLayerActivatedEvent>(e => events.Add(e));

        _resolver.Resolve(GameState.Exploring, enemiesNearby: false, inIrradiatedZone: false);

        Assert.Contains(events, e => e is MusicLayerDeactivatedEvent d && d.Layer == MusicLayer.Combat);
        Assert.Contains(events, e => e is MusicLayerActivatedEvent a && a.Layer == MusicLayer.Exploration);
    }

    [Fact]
    public void DeactivationEventHasCorrectFade()
    {
        _resolver.Resolve(GameState.InCombat, enemiesNearby: false, inIrradiatedZone: false);

        MusicLayerDeactivatedEvent? received = null;
        _bus.Subscribe<MusicLayerDeactivatedEvent>(e => received = e);

        _resolver.Resolve(GameState.Exploring, enemiesNearby: false, inIrradiatedZone: false);

        Assert.NotNull(received);
        Assert.Equal(MusicLayer.Combat, received.Value.Layer);
        Assert.Equal(0.5f, received.Value.FadeDuration);
    }

    [Fact]
    public void ActivationEventHasCorrectFade()
    {
        MusicLayerActivatedEvent? received = null;
        _bus.Subscribe<MusicLayerActivatedEvent>(e => received = e);

        _resolver.Resolve(GameState.Exploring, enemiesNearby: false, inIrradiatedZone: false);

        Assert.NotNull(received);
        Assert.Equal(MusicLayer.Exploration, received.Value.Layer);
        Assert.Equal(2f, received.Value.FadeDuration);
    }

    [Fact]
    public void NoEventsWhenStateUnchanged()
    {
        _resolver.Resolve(GameState.Exploring, enemiesNearby: false, inIrradiatedZone: false);

        var events = new List<IGameEvent>();
        _bus.Subscribe<MusicLayerActivatedEvent>(e => events.Add(e));
        _bus.Subscribe<MusicLayerDeactivatedEvent>(e => events.Add(e));

        _resolver.Resolve(GameState.Exploring, enemiesNearby: false, inIrradiatedZone: false);

        Assert.Empty(events);
    }
}
