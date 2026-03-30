namespace Oravey2.Core.Inventory.Items;

public static class DurabilityHelper
{
    public static int? Degrade(ItemInstance item)
    {
        if (item.Definition.Durability == null || item.CurrentDurability == null)
            return null;

        var amount = item.Definition.Durability.DegradePerUse;
        item.CurrentDurability = Math.Max(0, item.CurrentDurability.Value - (int)MathF.Ceiling(amount));
        return item.CurrentDurability;
    }

    public static int? DegradeBy(ItemInstance item, float amount)
    {
        if (item.Definition.Durability == null || item.CurrentDurability == null)
            return null;

        item.CurrentDurability = Math.Max(0, item.CurrentDurability.Value - (int)MathF.Ceiling(amount));
        return item.CurrentDurability;
    }

    public static int? Repair(ItemInstance item, int amount)
    {
        if (item.Definition.Durability == null || item.CurrentDurability == null)
            return null;

        item.CurrentDurability = Math.Min(
            item.Definition.Durability.MaxDurability,
            item.CurrentDurability.Value + amount);
        return item.CurrentDurability;
    }

    public static bool IsBroken(ItemInstance item)
        => item.CurrentDurability.HasValue && item.CurrentDurability.Value <= 0;

    public static float? GetDurabilityPercent(ItemInstance item)
    {
        if (item.Definition.Durability == null || item.CurrentDurability == null)
            return null;

        return (float)item.CurrentDurability.Value / item.Definition.Durability.MaxDurability;
    }
}
