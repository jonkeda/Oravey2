using Oravey2.Core.Character.Stats;
using Oravey2.Core.Crafting;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;

namespace Oravey2.Tests.Crafting;

public class RepairProcessorTests
{
    private static ItemDefinition MakeDurableItem(int maxDurability = 100)
        => new("weapon", "Weapon", "", ItemCategory.WeaponRanged, 3f, false, 50,
            Durability: new DurabilityData(maxDurability, 2.0f));

    private static ItemDefinition MakeStackableItem(string id, int maxStack = 99)
        => new(id, id, "", ItemCategory.CraftingMaterial, 0.1f, true, 5, MaxStack: maxStack);

    private static ItemDefinition MakeNonDurableItem()
        => new("junk", "Junk", "", ItemCategory.Junk, 1f, false, 5);

    private static (RepairProcessor proc, InventoryComponent inv) Setup(int scrapCount = 0, int capsCount = 0)
    {
        var stats = new StatsComponent();
        var inv = new InventoryComponent(stats);
        if (scrapCount > 0)
            inv.Add(new ItemInstance(MakeStackableItem("scrap_metal"), scrapCount));
        if (capsCount > 0)
            inv.Add(new ItemInstance(MakeStackableItem("caps"), capsCount));
        return (new RepairProcessor(), inv);
    }

    // --- CalculateSelfRepair ---

    [Fact]
    public void CalculateSelfRepair_HasScrap_ReturnsAmount()
    {
        var (proc, inv) = Setup(scrapCount: 6);
        var item = new ItemInstance(MakeDurableItem(100));
        item.CurrentDurability = 0; // 100 missing
        Assert.Equal(100, proc.CalculateSelfRepairAmount(inv, item));
    }

    [Fact]
    public void CalculateSelfRepair_NoScrap_ReturnsZero()
    {
        var (proc, inv) = Setup(scrapCount: 0);
        var item = new ItemInstance(MakeDurableItem(100));
        item.CurrentDurability = 50;
        Assert.Equal(0, proc.CalculateSelfRepairAmount(inv, item));
    }

    [Fact]
    public void CalculateSelfRepair_FullDurability_ReturnsZero()
    {
        var (proc, inv) = Setup(scrapCount: 10);
        var item = new ItemInstance(MakeDurableItem(100)); // starts at max
        Assert.Equal(0, proc.CalculateSelfRepairAmount(inv, item));
    }

    [Fact]
    public void CalculateSelfRepair_NoDurabilityData_ReturnsZero()
    {
        var (proc, inv) = Setup(scrapCount: 10);
        var item = new ItemInstance(MakeNonDurableItem());
        Assert.Equal(0, proc.CalculateSelfRepairAmount(inv, item));
    }

    // --- SelfRepair ---

    [Fact]
    public void SelfRepair_ConsumesScrap()
    {
        var (proc, inv) = Setup(scrapCount: 6);
        var item = new ItemInstance(MakeDurableItem(100));
        item.CurrentDurability = 50; // 50 missing → 1 unit → 3 scrap

        proc.SelfRepair(inv, item);

        // 6 - 3 = 3 scrap remaining
        Assert.True(inv.Contains("scrap_metal", 3));
        Assert.False(inv.Contains("scrap_metal", 4));
    }

    [Fact]
    public void SelfRepair_RestoresDurability()
    {
        var (proc, inv) = Setup(scrapCount: 6);
        var item = new ItemInstance(MakeDurableItem(100));
        item.CurrentDurability = 50;

        var restored = proc.SelfRepair(inv, item);

        Assert.Equal(50, restored);
        Assert.Equal(100, item.CurrentDurability);
    }

    [Fact]
    public void SelfRepair_CappedAtMax()
    {
        var (proc, inv) = Setup(scrapCount: 30); // lots of scrap
        var item = new ItemInstance(MakeDurableItem(100));
        item.CurrentDurability = 80; // only 20 missing

        proc.SelfRepair(inv, item);

        Assert.Equal(100, item.CurrentDurability);
    }

    // --- NPC repair ---

    [Fact]
    public void CalculateNpcCost_MissingDurability_ReturnsCorrectCaps()
    {
        var proc = new RepairProcessor();
        var item = new ItemInstance(MakeDurableItem(100));
        item.CurrentDurability = 50; // 50 missing → ceiling(50/25)=2 units → 20 caps
        Assert.Equal(20, proc.CalculateNpcRepairCost(item));
    }

    [Fact]
    public void CalculateNpcCost_FullDurability_ReturnsZero()
    {
        var proc = new RepairProcessor();
        var item = new ItemInstance(MakeDurableItem(100));
        Assert.Equal(0, proc.CalculateNpcRepairCost(item));
    }

    [Fact]
    public void NpcRepair_ConsumeCaps_RestoreFull()
    {
        var (proc, inv) = Setup(capsCount: 100);
        var item = new ItemInstance(MakeDurableItem(100));
        item.CurrentDurability = 50;

        Assert.True(proc.NpcRepair(inv, item));
        Assert.Equal(100, item.CurrentDurability);
    }

    [Fact]
    public void NpcRepair_InsufficientCaps_ReturnsFalse()
    {
        var (proc, inv) = Setup(capsCount: 5); // not enough
        var item = new ItemInstance(MakeDurableItem(100));
        item.CurrentDurability = 50; // needs 20 caps

        Assert.False(proc.NpcRepair(inv, item));
        Assert.Equal(50, item.CurrentDurability); // unchanged
    }
}
