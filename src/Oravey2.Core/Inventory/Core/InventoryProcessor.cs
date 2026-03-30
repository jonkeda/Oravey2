using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;

namespace Oravey2.Core.Inventory.Core;

public class InventoryProcessor
{
    private readonly InventoryComponent _inventory;
    private readonly EquipmentComponent _equipment;
    private readonly IEventBus _eventBus;

    public InventoryProcessor(
        InventoryComponent inventory,
        EquipmentComponent equipment,
        IEventBus eventBus)
    {
        _inventory = inventory;
        _equipment = equipment;
        _eventBus = eventBus;
    }

    public bool TryPickup(ItemInstance item)
    {
        if (!_inventory.CanAdd(item)) return false;

        _inventory.Add(item);
        _eventBus.Publish(new ItemPickedUpEvent(item.Definition.Id));
        return true;
    }

    public bool TryDrop(string itemId, int count = 1)
    {
        if (!_inventory.Remove(itemId, count)) return false;

        _eventBus.Publish(new ItemDroppedEvent(itemId, count));
        return true;
    }

    public bool TryEquip(ItemInstance item, EquipmentSlot slot)
    {
        if (item.Definition.Slot != slot) return false;

        var previous = _equipment.Equip(item, slot);
        _inventory.Remove(item.Definition.Id, 1);

        if (previous != null)
            _inventory.Add(previous);

        _eventBus.Publish(new ItemEquippedEvent(item.Definition.Id, slot));
        return true;
    }

    public bool TryUnequip(EquipmentSlot slot)
    {
        var item = _equipment.Unequip(slot);
        if (item == null) return false;

        _inventory.Add(item);
        _eventBus.Publish(new ItemUnequippedEvent(slot));
        return true;
    }
}
