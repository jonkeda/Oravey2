using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Oravey2.Core.Loot;

/// <summary>
/// Auto-picks up loot when the player walks within PickupRadius.
/// Runs on the player entity.
/// </summary>
public class LootPickupScript : SyncScript
{
    public float PickupRadius { get; set; } = 1.5f;
    public InventoryProcessor? Processor { get; set; }
    public IEventBus? EventBus { get; set; }

    public override void Update()
    {
        if (Processor == null || Entity.Scene == null) return;

        var playerPos = Entity.Transform.Position;
        var toRemove = new List<Entity>();

        foreach (var entity in Entity.Scene.Entities)
        {
            if (!LootDropScript.TryGetLootItems(entity, out var items) || items == null)
                continue;

            var dist = Vector3.Distance(playerPos, entity.Transform.Position);
            if (dist > PickupRadius) continue;

            foreach (var item in items)
            {
                if (Processor.TryPickup(item))
                {
                    EventBus?.Publish(new NotificationEvent(
                        $"Picked up {item.Definition.Name}" +
                        (item.StackCount > 1 ? $" x{item.StackCount}" : ""),
                        3f));
                }
            }

            LootDropScript.RemoveLoot(entity);
            toRemove.Add(entity);
        }

        foreach (var entity in toRemove)
            Entity.Scene.Entities.Remove(entity);
    }
}
