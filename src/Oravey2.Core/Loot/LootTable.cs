using Oravey2.Core.Inventory.Items;

namespace Oravey2.Core.Loot;

public sealed class LootTable
{
    private readonly List<(ItemDefinition Item, float Weight)> _entries = [];
    private static readonly Random _rng = new();

    public void Add(ItemDefinition item, float weight) =>
        _entries.Add((item, weight));

    /// <summary>
    /// Selects 1-maxCount items using weighted random.
    /// Each entry is rolled independently against its weight as a drop chance (0-1).
    /// </summary>
    public List<ItemInstance> Roll(int maxCount = 2)
    {
        var drops = new List<ItemInstance>();
        foreach (var (item, weight) in _entries)
        {
            if (drops.Count >= maxCount) break;
            if (_rng.NextDouble() < weight)
                drops.Add(new ItemInstance(item));
        }
        return drops;
    }
}
