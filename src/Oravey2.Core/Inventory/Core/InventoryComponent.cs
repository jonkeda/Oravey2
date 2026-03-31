using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Inventory.Items;

namespace Oravey2.Core.Inventory.Core;

public class InventoryComponent
{
    private readonly List<ItemInstance> _items = [];
    private readonly StatsComponent _stats;

    public IReadOnlyList<ItemInstance> Items => _items;
    public float MaxCarryWeight => LevelFormulas.CarryWeight(_stats.GetEffective(Stat.Strength));
    public float CurrentWeight => _items.Sum(i => i.TotalWeight);
    public bool IsOverweight => CurrentWeight > MaxCarryWeight;
    public int Caps { get; set; } = 50;

    /// <summary>
    /// Applies the death penalty: lose 10% of Caps (rounded down, minimum 0).
    /// Returns the amount lost.
    /// </summary>
    public int ApplyDeathPenalty()
    {
        var loss = Caps / 10; // integer division = floor
        Caps = Math.Max(0, Caps - loss);
        return loss;
    }

    public InventoryComponent(StatsComponent stats)
    {
        _stats = stats;
    }

    public bool CanAdd(ItemInstance item)
        => CurrentWeight + item.TotalWeight <= MaxCarryWeight;

    public bool Add(ItemInstance item)
    {
        if (item.Definition.Stackable)
        {
            var existing = _items.FirstOrDefault(i =>
                i.Definition.Id == item.Definition.Id &&
                i.StackCount < i.Definition.MaxStack);

            if (existing != null)
            {
                var space = existing.Definition.MaxStack - existing.StackCount;
                var toAdd = Math.Min(space, item.StackCount);
                existing.StackCount += toAdd;
                item.StackCount -= toAdd;
                if (item.StackCount <= 0) return true;
            }
        }

        _items.Add(item);
        return true;
    }

    public bool Remove(string itemId, int count = 1)
    {
        var item = _items.FirstOrDefault(i => i.Definition.Id == itemId);
        if (item == null) return false;

        if (item.StackCount > count)
        {
            item.StackCount -= count;
            return true;
        }

        if (item.StackCount == count)
        {
            _items.Remove(item);
            return true;
        }

        return false;
    }

    public bool Contains(string itemId, int count = 1)
        => _items.Where(i => i.Definition.Id == itemId).Sum(i => i.StackCount) >= count;
}
