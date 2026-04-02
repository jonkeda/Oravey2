using Oravey2.Core.Framework.Events;
using Oravey2.Core.World;

namespace Oravey2.Core.Combat;

public sealed class KillTracker
{
    private readonly WorldStateService _worldState;
    private readonly Dictionary<string, string> _counterTags = new();
    private readonly Dictionary<string, string> _flagTags = new();

    public KillTracker(WorldStateService worldState, IEventBus eventBus)
    {
        _worldState = worldState;
        eventBus.Subscribe<EntityDiedEvent>(OnEntityDied);
    }

    /// <summary>
    /// When an enemy with the given tag dies, increment the named counter.
    /// </summary>
    public void RegisterCounter(string enemyTag, string counterName)
        => _counterTags[enemyTag] = counterName;

    /// <summary>
    /// When an enemy with the given tag dies, set the named flag to true.
    /// </summary>
    public void RegisterFlag(string enemyTag, string flagName)
        => _flagTags[enemyTag] = flagName;

    private void OnEntityDied(EntityDiedEvent e)
    {
        if (e.Tag is null) return;

        if (_counterTags.TryGetValue(e.Tag, out var counterName))
            _worldState.IncrementCounter(counterName);

        if (_flagTags.TryGetValue(e.Tag, out var flagName))
            _worldState.SetFlag(flagName, true);
    }
}
