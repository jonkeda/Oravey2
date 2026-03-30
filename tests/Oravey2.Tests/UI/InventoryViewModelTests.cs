using Oravey2.Core.Character.Stats;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.UI.ViewModels;

namespace Oravey2.Tests.UI;

public class InventoryViewModelTests
{
    private static ItemDefinition MakeItem(string id, float weight = 1f, bool stackable = false)
        => new(id, id, "desc", ItemCategory.Junk, weight, stackable, 5);

    private static ItemDefinition MakeDurableItem(string id, int maxDur = 100)
        => new(id, id, "desc", ItemCategory.WeaponRanged, 3f, false, 50,
            Durability: new DurabilityData(maxDur, 2.0f));

    [Fact]
    public void Create_MapsItems()
    {
        var inv = new InventoryComponent(new StatsComponent());
        inv.Add(new ItemInstance(MakeItem("a")));
        inv.Add(new ItemInstance(MakeItem("b")));
        var vm = InventoryViewModel.Create(inv);
        Assert.Equal(2, vm.Items.Count);
    }

    [Fact]
    public void Create_MapsWeight()
    {
        var inv = new InventoryComponent(new StatsComponent());
        inv.Add(new ItemInstance(MakeItem("a", weight: 5f)));
        var vm = InventoryViewModel.Create(inv);
        Assert.True(vm.CurrentWeight > 0);
    }

    [Fact]
    public void Create_MapsOverweight()
    {
        var inv = new InventoryComponent(new StatsComponent());
        // MaxCarryWeight = 50 + 5*10 = 100. Add items exceeding that.
        for (int i = 0; i < 21; i++)
            inv.Add(new ItemInstance(MakeItem($"heavy_{i}", weight: 5f)));
        var vm = InventoryViewModel.Create(inv);
        Assert.True(vm.IsOverweight);
    }

    [Fact]
    public void Create_ItemView_HasDurability()
    {
        var inv = new InventoryComponent(new StatsComponent());
        var item = new ItemInstance(MakeDurableItem("weapon", 100));
        item.CurrentDurability = 50;
        inv.Add(item);
        var vm = InventoryViewModel.Create(inv);
        Assert.Equal(50, vm.Items[0].CurrentDurability);
        Assert.Equal(100, vm.Items[0].MaxDurability);
    }

    [Fact]
    public void Create_ItemView_NoDurability_Null()
    {
        var inv = new InventoryComponent(new StatsComponent());
        inv.Add(new ItemInstance(MakeItem("junk")));
        var vm = InventoryViewModel.Create(inv);
        Assert.Null(vm.Items[0].CurrentDurability);
        Assert.Null(vm.Items[0].MaxDurability);
    }
}
