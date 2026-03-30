using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.State;

namespace Oravey2.Core.Audio;

/// <summary>
/// Determines which music layers should be active based on GameState and context flags.
/// Pure logic — the Stride MusicStateProcessor calls Resolve() and handles actual crossfading.
/// </summary>
public sealed class MusicLayerResolver
{
    private readonly IEventBus _eventBus;
    private readonly HashSet<MusicLayer> _activeLayers = new() { MusicLayer.Base };

    /// <summary>Currently active music layers.</summary>
    public IReadOnlySet<MusicLayer> ActiveLayers => _activeLayers;

    public MusicLayerResolver(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Evaluates which layers should be active and publishes activation/deactivation events
    /// for any changes.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="enemiesNearby">True if non-aggro enemies are within detection range.</param>
    /// <param name="inIrradiatedZone">True if the player is in a zone with radiation > 0.</param>
    public void Resolve(GameState state, bool enemiesNearby, bool inIrradiatedZone)
    {
        var desired = new HashSet<MusicLayer> { MusicLayer.Base };

        if (state == GameState.Exploring)
            desired.Add(MusicLayer.Exploration);

        if (state == GameState.InCombat)
            desired.Add(MusicLayer.Combat);

        if (enemiesNearby && state != GameState.InCombat)
            desired.Add(MusicLayer.Tension);

        if (inIrradiatedZone)
            desired.Add(MusicLayer.Eerie);

        // Deactivate layers no longer desired
        foreach (var layer in _activeLayers)
        {
            if (!desired.Contains(layer))
            {
                float fade = MusicLayerDefinition.GetFadeDuration(layer);
                _eventBus.Publish(new MusicLayerDeactivatedEvent(layer, fade));
            }
        }

        // Activate newly desired layers
        foreach (var layer in desired)
        {
            if (!_activeLayers.Contains(layer))
            {
                float fade = MusicLayerDefinition.GetFadeDuration(layer);
                _eventBus.Publish(new MusicLayerActivatedEvent(layer, fade));
            }
        }

        _activeLayers.Clear();
        foreach (var layer in desired)
            _activeLayers.Add(layer);
    }
}
