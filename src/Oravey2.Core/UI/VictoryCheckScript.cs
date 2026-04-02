using Oravey2.Core.Framework.State;
using Oravey2.Core.UI.Stride;
using Oravey2.Core.World;
using Stride.Engine;

namespace Oravey2.Core.UI;

/// <summary>
/// Detects quest chain completion (m1_complete flag) and shows a victory overlay.
/// The overlay displays for 5 seconds then auto-hides. Player remains in Exploring state.
/// </summary>
public class VictoryCheckScript : SyncScript
{
    public GameOverOverlayScript? Overlay { get; set; }
    public WorldStateService? WorldState { get; set; }
    public GameStateManager? StateManager { get; set; }

    private bool _triggered;
    private float _dismissTimer;

    /// <summary>Whether the victory has been achieved (m1_complete flag is set).</summary>
    public bool VictoryAchieved => WorldState?.GetFlag("m1_complete") == true;

    /// <summary>Whether the victory overlay is currently showing.</summary>
    public bool IsShowingOverlay => _dismissTimer > 0f;

    public override void Update()
    {
        if (_triggered)
        {
            // Auto-dismiss after 5 seconds
            if (_dismissTimer > 0f)
            {
                _dismissTimer -= (float)Game.UpdateTime.Elapsed.TotalSeconds;
                if (_dismissTimer <= 0f)
                {
                    _dismissTimer = 0f;
                    Overlay?.Hide();
                }
            }
            return;
        }

        if (StateManager?.CurrentState != GameState.Exploring) return;

        if (WorldState?.GetFlag("m1_complete") == true)
        {
            _triggered = true;
            _dismissTimer = 5f;
            Overlay?.Show("HAVEN IS SAFE", "You completed all quests.");
        }
    }
}
