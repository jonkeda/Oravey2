namespace Oravey2.Core.Crafting;

using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;

public sealed class RepairProcessor
{
    public const int ScrapPerRepairUnit = 3;
    public const int DurabilityPerRepairUnit = 50;

    public const int CapsPerNpcUnit = 10;
    public const int DurabilityPerNpcUnit = 25;

    public int CalculateSelfRepairAmount(InventoryComponent inventory, ItemInstance item)
    {
        if (item.Definition.Durability == null || item.CurrentDurability == null)
            return 0;

        var missing = item.Definition.Durability.MaxDurability - item.CurrentDurability.Value;
        if (missing <= 0) return 0;

        var scrapCount = CountItem(inventory, "scrap_metal");
        var repairUnits = scrapCount / ScrapPerRepairUnit;
        var maxRestore = repairUnits * DurabilityPerRepairUnit;

        return Math.Min(maxRestore, missing);
    }

    public int SelfRepair(InventoryComponent inventory, ItemInstance item)
    {
        var amount = CalculateSelfRepairAmount(inventory, item);
        if (amount <= 0) return 0;

        var unitsNeeded = (int)MathF.Ceiling((float)amount / DurabilityPerRepairUnit);
        var scrapNeeded = unitsNeeded * ScrapPerRepairUnit;

        inventory.Remove("scrap_metal", scrapNeeded);
        item.CurrentDurability = Math.Min(
            item.CurrentDurability!.Value + amount,
            item.Definition.Durability!.MaxDurability);

        return amount;
    }

    public int CalculateNpcRepairCost(ItemInstance item)
    {
        if (item.Definition.Durability == null || item.CurrentDurability == null)
            return 0;

        var missing = item.Definition.Durability.MaxDurability - item.CurrentDurability.Value;
        if (missing <= 0) return 0;

        var units = (int)MathF.Ceiling((float)missing / DurabilityPerNpcUnit);
        return units * CapsPerNpcUnit;
    }

    public bool NpcRepair(InventoryComponent inventory, ItemInstance item)
    {
        var cost = CalculateNpcRepairCost(item);
        if (cost <= 0) return false;

        if (!inventory.Contains("caps", cost))
            return false;

        inventory.Remove("caps", cost);
        item.CurrentDurability = item.Definition.Durability!.MaxDurability;
        return true;
    }

    private static int CountItem(InventoryComponent inventory, string itemId)
    {
        var total = 0;
        foreach (var item in inventory.Items)
        {
            if (item.Definition.Id == itemId)
                total += item.StackCount;
        }
        return total;
    }
}
