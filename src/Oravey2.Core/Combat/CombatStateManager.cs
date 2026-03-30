using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.State;

namespace Oravey2.Core.Combat;

public sealed class CombatStateManager
{
    private readonly IEventBus _eventBus;
    private readonly GameStateManager _gameState;
    private readonly List<string> _combatants = [];

    public bool InCombat { get; private set; }
    public IReadOnlyList<string> Combatants => _combatants;

    public CombatStateManager(IEventBus eventBus, GameStateManager gameState)
    {
        _eventBus = eventBus;
        _gameState = gameState;
    }

    public bool EnterCombat(string[] enemyIds)
    {
        if (InCombat || enemyIds.Length == 0) return false;

        InCombat = true;
        _combatants.Clear();
        _combatants.AddRange(enemyIds);

        _gameState.TransitionTo(GameState.InCombat);
        _eventBus.Publish(new CombatStartedEvent([.. enemyIds]));
        return true;
    }

    public bool ExitCombat()
    {
        if (!InCombat) return false;

        InCombat = false;
        _combatants.Clear();

        _gameState.TransitionTo(GameState.Exploring);
        _eventBus.Publish(new CombatEndedEvent());
        return true;
    }

    public void RemoveCombatant(string entityId)
    {
        _combatants.Remove(entityId);
        if (_combatants.Count == 0 && InCombat)
            ExitCombat();
    }
}
