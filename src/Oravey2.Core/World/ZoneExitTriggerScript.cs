using Oravey2.Core.Framework.State;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Oravey2.Core.World;

/// <summary>
/// Detects player proximity to a zone exit point and fires a callback.
/// Once triggered, stays inert until reset (prevents re-triggering during transition).
/// </summary>
public class ZoneExitTriggerScript : SyncScript
{
    public Entity? Player { get; set; }
    public string TargetZoneId { get; set; } = "";
    public Vector3 TargetSpawnPosition { get; set; }
    public float TriggerRadius { get; set; } = 1.5f;
    public GameStateManager? StateManager { get; set; }
    public Action<string, Vector3>? OnZoneExit { get; set; }

    private bool _triggered;

    public override void Update()
    {
        if (_triggered || StateManager?.CurrentState != GameState.Exploring || Player == null) return;

        var dist = (Player.Transform.Position - Entity.Transform.Position).Length();
        if (dist <= TriggerRadius)
        {
            _triggered = true;
            OnZoneExit?.Invoke(TargetZoneId, TargetSpawnPosition);
        }
    }
}
