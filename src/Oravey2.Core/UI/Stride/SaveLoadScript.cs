using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Oravey2.Core.Save;
using Oravey2.Core.UI;
using Stride.Engine;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Handles QuickSave (F5), QuickLoad (F9), and auto-save ticking during gameplay.
/// Only active when GameState is Exploring.
/// </summary>
public class SaveLoadScript : SyncScript
{
    public IInputProvider? InputProvider { get; set; }
    public GameStateManager? StateManager { get; set; }
    public AutoSaveTracker? AutoSaveTracker { get; set; }
    public Func<NotificationService?>? GetNotifications { get; set; }

    /// <summary>Called to perform a save (set by Program.cs to build SaveData and call SaveService).</summary>
    public Action? OnSave { get; set; }

    /// <summary>Called to perform a load (set by Program.cs to call SaveService + restore).</summary>
    public Action? OnLoad { get; set; }

    /// <summary>Called to check if a save file exists.</summary>
    public Func<bool>? HasSave { get; set; }

    public override void Update()
    {
        if (StateManager == null || InputProvider == null) return;

        var state = StateManager.CurrentState;
        var notifications = GetNotifications?.Invoke();

        // QuickSave: only during Exploring
        if (state == GameState.Exploring && InputProvider.IsActionPressed(GameAction.QuickSave))
        {
            OnSave?.Invoke();
            notifications?.Add("Quick Saved!", 2f);
        }

        // QuickLoad: only during Exploring, and only if save exists
        if (state == GameState.Exploring && InputProvider.IsActionPressed(GameAction.QuickLoad))
        {
            if (HasSave?.Invoke() == true)
            {
                OnLoad?.Invoke();
                GetNotifications?.Invoke()?.Add("Game Loaded!", 2f);
            }
            else
            {
                notifications?.Add("No save file found", 2f);
            }
        }

        // Auto-save tick: only during Exploring
        if (state == GameState.Exploring && AutoSaveTracker != null)
        {
            var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;
            AutoSaveTracker.Tick(dt);

            if (AutoSaveTracker.ShouldSave)
            {
                OnSave?.Invoke();
                AutoSaveTracker.Acknowledge();
                notifications?.Add("Auto-saved!", 2f);
            }
        }
    }
}
