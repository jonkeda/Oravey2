using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Stride.Engine;

namespace Oravey2.Core.NPC;

/// <summary>
/// Detects player proximity and fires NpcInteractionEvent when F is pressed within range.
/// </summary>
public class InteractionTriggerScript : SyncScript
{
    public Entity? Player { get; set; }
    public float InteractionRadius { get; set; } = 2.0f;
    public NpcDefinition? NpcDef { get; set; }
    public IInputProvider? InputProvider { get; set; }
    public IEventBus? EventBus { get; set; }
    public GameStateManager? StateManager { get; set; }

    public bool PlayerInRange { get; private set; }

    public override void Update()
    {
        if (StateManager?.CurrentState != GameState.Exploring)
        {
            PlayerInRange = false;
            return;
        }

        if (Player == null)
        {
            PlayerInRange = false;
            return;
        }

        var dist = (Player.Transform.Position - Entity.Transform.Position).Length();
        PlayerInRange = dist <= InteractionRadius;

        if (PlayerInRange && InputProvider?.IsActionPressed(GameAction.Interact) == true && NpcDef != null)
            EventBus?.Publish(new NpcInteractionEvent(NpcDef.Id, NpcDef.DialogueTreeId));
    }
}
