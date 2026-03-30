using Oravey2.Core.Inventory.Items;

namespace Oravey2.Core.Inventory.Equipment;

public class EquipmentComponent
{
    private readonly Dictionary<EquipmentSlot, ItemInstance?> _slots = new();

    public EquipmentComponent()
    {
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
            _slots[slot] = null;
    }

    public ItemInstance? GetEquipped(EquipmentSlot slot) => _slots[slot];

    public ItemInstance? Equip(ItemInstance item, EquipmentSlot slot)
    {
        if (item.Definition.Slot != slot) return null;

        var previous = _slots[slot];
        _slots[slot] = item;
        return previous;
    }

    public ItemInstance? Unequip(EquipmentSlot slot)
    {
        var item = _slots[slot];
        _slots[slot] = null;
        return item;
    }

    public bool IsSlotOccupied(EquipmentSlot slot) => _slots[slot] != null;
}
