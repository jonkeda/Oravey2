using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Framework.State;

public sealed class GameStateManager
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<GameStateManager> _logger;

    public GameState CurrentState { get; private set; } = GameState.Loading;

    public GameStateManager(IEventBus eventBus, ILogger<GameStateManager>? logger = null)
    {
        _eventBus = eventBus;
        _logger = logger ?? NullLogger<GameStateManager>.Instance;
    }

    public bool TransitionTo(GameState newState)
    {
        if (CurrentState == newState)
        {
            _logger.LogDebug("Ignored transition to same state: {State}", newState);
            return false;
        }

        if (!IsValidTransition(CurrentState, newState))
        {
            _logger.LogWarning("Invalid state transition: {From} -> {To}", CurrentState, newState);
            return false;
        }

        var oldState = CurrentState;
        CurrentState = newState;
        _logger.LogInformation("State transition: {From} -> {To}", oldState, newState);
        _eventBus.Publish(new GameStateChangedEvent(oldState, newState));
        return true;
    }

    private static bool IsValidTransition(GameState from, GameState to)
    {
        return (from, to) switch
        {
            (GameState.Loading, GameState.Exploring) => true,
            (GameState.Exploring, GameState.InCombat) => true,
            (GameState.Exploring, GameState.InDialogue) => true,
            (GameState.Exploring, GameState.InMenu) => true,
            (GameState.Exploring, GameState.Paused) => true,
            (GameState.InCombat, GameState.Exploring) => true,
            (GameState.InCombat, GameState.Paused) => true,
            (GameState.InDialogue, GameState.Exploring) => true,
            (GameState.InMenu, GameState.Exploring) => true,
            (GameState.Paused, GameState.Exploring) => true,
            (GameState.Paused, GameState.InCombat) => true,
            (GameState.InCombat, GameState.GameOver) => true,
            _ => false
        };
    }
}
