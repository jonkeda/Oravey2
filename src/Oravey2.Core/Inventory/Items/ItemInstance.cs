namespace Oravey2.Core.Inventory.Items;

public sealed class ItemInstance
{
    public ItemDefinition Definition { get; }
    public int StackCount { get; set; }
    public int? CurrentDurability { get; set; }

    public float TotalWeight => Definition.Weight * StackCount;

    public ItemInstance(ItemDefinition definition, int stackCount = 1)
    {
        Definition = definition;
        StackCount = stackCount;
        CurrentDurability = definition.Durability?.MaxDurability;
    }
}
