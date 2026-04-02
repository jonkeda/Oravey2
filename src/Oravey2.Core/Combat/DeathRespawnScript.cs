using Oravey2.Core.Character.Health;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Save;
using Oravey2.Core.UI;
using Oravey2.Core.UI.Stride;
using Stride.Engine;

namespace Oravey2.Core.Combat;

/// <summary>
/// Manages the death → respawn sequence with timed overlay states.
/// Replaces the permanent game-over freeze with a timed respawn flow.
/// State machine: Idle → ShowDeath (0-2s) → Respawning (2-3s) → Complete (3s+).
/// </summary>
public class DeathRespawnScript : SyncScript
{
    public float ShowDeathDuration { get; set; } = 2.0f;
    public float RespawnDelay { get; set; } = 3.0f;

    // Dependencies
    public HealthComponent? PlayerHealth { get; set; }
    public InventoryComponent? PlayerInventory { get; set; }
    public GameStateManager? GameStateManager { get; set; }
    public GameOverOverlayScript? DeathOverlay { get; set; }
    public NotificationService? Notifications { get; set; }
    public AutoSaveTracker? AutoSaveTracker { get; set; }

    /// <summary>
    /// Callback invoked when the respawn is ready to execute.
    /// Receives the number of caps lost. The callback should handle zone transition,
    /// player healing, state change, and notification display.
    /// </summary>
    public Action<int>? OnRespawn { get; set; }

    private RespawnState _state = RespawnState.Idle;
    private float _timer;
    private int _capsLost;

    /// <summary>Whether the player is currently in the death/respawn sequence.</summary>
    public bool IsDead => _state != RespawnState.Idle;

    /// <summary>Elapsed time since death sequence started.</summary>
    public float RespawnTimer => _timer;

    /// <summary>Caps lost in the current death sequence (set at the 2s mark).</summary>
    public int CapsLost => _capsLost;

    /// <summary>Current state of the respawn sequence.</summary>
    public RespawnState CurrentRespawnState => _state;

    public override void Update()
    {
        if (GameStateManager == null) return;

        var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;

        switch (_state)
        {
            case RespawnState.Idle:
                if (GameStateManager.CurrentState == GameState.GameOver)
                {
                    _state = RespawnState.ShowDeath;
                    _timer = 0f;
                    _capsLost = 0;
                    if (AutoSaveTracker != null)
                        AutoSaveTracker.Paused = true;
                    // Override GameOverOverlayScript's default "GAME OVER" text
                    DeathOverlay?.Show("YOU DIED", "");
                }
                break;

            case RespawnState.ShowDeath:
                _timer += dt;
                if (_timer >= ShowDeathDuration)
                {
                    _state = RespawnState.Respawning;
                    // Apply caps penalty
                    if (PlayerInventory != null)
                        _capsLost = PlayerInventory.ApplyDeathPenalty();
                    DeathOverlay?.SetSubtitle("Respawning...");
                }
                break;

            case RespawnState.Respawning:
                _timer += dt;
                if (_timer >= RespawnDelay)
                {
                    _state = RespawnState.Complete;
                    ExecuteRespawn();
                }
                break;

            case RespawnState.Complete:
                // Respawn triggered — entity will be destroyed by zone transition
                break;
        }
    }

    private void ExecuteRespawn()
    {
        DeathOverlay?.Hide();
        if (AutoSaveTracker != null)
            AutoSaveTracker.Paused = false;
        OnRespawn?.Invoke(_capsLost);
        // After OnRespawn triggers zone transition, this entity is destroyed.
        // The callback handles healing, state transition, and notification.
    }

    public enum RespawnState
    {
        Idle,
        ShowDeath,
        Respawning,
        Complete,
    }
}
