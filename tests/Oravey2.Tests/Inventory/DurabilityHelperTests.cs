using Oravey2.Core.Inventory.Items;

namespace Oravey2.Tests.Inventory;

public class DurabilityHelperTests
{
    private static ItemDefinition MakeDurableItem(int maxDurability = 100, float degradePerUse = 2.0f)
        => new("weapon", "Weapon", "", ItemCategory.WeaponRanged, 3f, false, 50,
            Durability: new DurabilityData(maxDurability, degradePerUse));

    private static ItemDefinition MakeNonDurableItem()
        => new("junk", "Junk", "", ItemCategory.Junk, 1f, false, 5);

    [Fact]
    public void Degrade_ReducesByDegradePerUse()
    {
        var item = new ItemInstance(MakeDurableItem(100, 2.0f));
        var result = DurabilityHelper.Degrade(item);
        Assert.Equal(98, result);
        Assert.Equal(98, item.CurrentDurability);
    }

    [Fact]
    public void Degrade_FloorsAtZero()
    {
        var item = new ItemInstance(MakeDurableItem(100, 2.0f));
        item.CurrentDurability = 1;
        DurabilityHelper.Degrade(item);
        Assert.Equal(0, item.CurrentDurability);
    }

    [Fact]
    public void Degrade_NoDurabilityData_ReturnsNull()
    {
        var item = new ItemInstance(MakeNonDurableItem());
        var result = DurabilityHelper.Degrade(item);
        Assert.Null(result);
    }

    [Fact]
    public void DegradeBy_CustomAmount()
    {
        var item = new ItemInstance(MakeDurableItem(100, 2.0f));
        var result = DurabilityHelper.DegradeBy(item, 5f);
        Assert.Equal(95, result);
    }

    [Fact]
    public void Repair_IncreaseDurability()
    {
        var item = new ItemInstance(MakeDurableItem(100, 2.0f));
        item.CurrentDurability = 50;
        var result = DurabilityHelper.Repair(item, 30);
        Assert.Equal(80, result);
    }

    [Fact]
    public void Repair_CappedAtMax()
    {
        var item = new ItemInstance(MakeDurableItem(100, 2.0f));
        item.CurrentDurability = 90;
        var result = DurabilityHelper.Repair(item, 50);
        Assert.Equal(100, result);
    }

    [Fact]
    public void Repair_NoDurabilityData_ReturnsNull()
    {
        var item = new ItemInstance(MakeNonDurableItem());
        var result = DurabilityHelper.Repair(item, 50);
        Assert.Null(result);
    }

    [Fact]
    public void IsBroken_AtZero_True()
    {
        var item = new ItemInstance(MakeDurableItem());
        item.CurrentDurability = 0;
        Assert.True(DurabilityHelper.IsBroken(item));
    }

    [Fact]
    public void IsBroken_AboveZero_False()
    {
        var item = new ItemInstance(MakeDurableItem());
        Assert.False(DurabilityHelper.IsBroken(item));
    }

    [Fact]
    public void IsBroken_NoDurability_False()
    {
        var item = new ItemInstance(MakeNonDurableItem());
        Assert.False(DurabilityHelper.IsBroken(item));
    }

    [Fact]
    public void GetDurabilityPercent_Half()
    {
        var item = new ItemInstance(MakeDurableItem(100, 2.0f));
        item.CurrentDurability = 50;
        Assert.Equal(0.5f, DurabilityHelper.GetDurabilityPercent(item));
    }

    [Fact]
    public void GetDurabilityPercent_NoDurability_Null()
    {
        var item = new ItemInstance(MakeNonDurableItem());
        Assert.Null(DurabilityHelper.GetDurabilityPercent(item));
    }
}
