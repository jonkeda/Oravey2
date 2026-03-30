using Oravey2.Core.Character.Stats;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;

namespace Oravey2.Tests.Inventory;

public class InventoryComponentTests
{
    private static ItemDefinition MakeItem(string id = "item", float weight = 5f,
        bool stackable = false, int maxStack = 1)
        => new(id, id, "", ItemCategory.Junk, weight, stackable, 10, maxStack);

    private static StatsComponent DefaultStats() => new(); // Str=5 → carry=100

    [Fact]
    public void Empty_WeightIsZero()
    {
        var inv = new InventoryComponent(DefaultStats());
        Assert.Equal(0f, inv.CurrentWeight);
    }

    [Fact]
    public void Add_IncreasesWeight()
    {
        var inv = new InventoryComponent(DefaultStats());
        inv.Add(new ItemInstance(MakeItem(weight: 5f)));

        Assert.Equal(5f, inv.CurrentWeight);
    }

    [Fact]
    public void CanAdd_OverWeight_ReturnsFalse()
    {
        var stats = new StatsComponent(new Dictionary<Stat, int>
        {
            { Stat.Strength, 1 }, { Stat.Perception, 5 }, { Stat.Endurance, 5 },
            { Stat.Charisma, 5 }, { Stat.Intelligence, 5 }, { Stat.Agility, 5 },
            { Stat.Luck, 2 },
        });
        var inv = new InventoryComponent(stats); // carry = 60

        Assert.False(inv.CanAdd(new ItemInstance(MakeItem(weight: 70f))));
    }

    [Fact]
    public void Add_StacksMatchingItems()
    {
        var inv = new InventoryComponent(DefaultStats());
        var def = MakeItem("ammo", 0.1f, stackable: true, maxStack: 100);
        inv.Add(new ItemInstance(def, 5));
        inv.Add(new ItemInstance(def, 3));

        // Should stack into one entry
        Assert.Single(inv.Items);
        Assert.Equal(8, inv.Items[0].StackCount);
    }

    [Fact]
    public void Add_RespectsMaxStack()
    {
        var inv = new InventoryComponent(DefaultStats());
        var def = MakeItem("ammo", 0.1f, stackable: true, maxStack: 5);
        inv.Add(new ItemInstance(def, 4));
        inv.Add(new ItemInstance(def, 3));

        // First stack: 4+1=5 (full), overflow: 2 in new stack
        Assert.Equal(2, inv.Items.Count);
        Assert.Equal(5, inv.Items[0].StackCount);
        Assert.Equal(2, inv.Items[1].StackCount);
    }

    [Fact]
    public void Remove_DecreasesStack()
    {
        var inv = new InventoryComponent(DefaultStats());
        var def = MakeItem("ammo", 0.1f, stackable: true, maxStack: 100);
        inv.Add(new ItemInstance(def, 5));
        inv.Remove("ammo", 2);

        Assert.Single(inv.Items);
        Assert.Equal(3, inv.Items[0].StackCount);
    }

    [Fact]
    public void Remove_RemovesItemAtZero()
    {
        var inv = new InventoryComponent(DefaultStats());
        inv.Add(new ItemInstance(MakeItem("sword")));
        inv.Remove("sword", 1);

        Assert.Empty(inv.Items);
    }

    [Fact]
    public void Remove_NotEnough_ReturnsFalse()
    {
        var inv = new InventoryComponent(DefaultStats());
        inv.Add(new ItemInstance(MakeItem("sword")));
        Assert.False(inv.Remove("sword", 2));
    }

    [Fact]
    public void Contains_ChecksAcrossStacks()
    {
        var inv = new InventoryComponent(DefaultStats());
        var def = MakeItem("ammo", 0.1f, stackable: true, maxStack: 5);
        inv.Add(new ItemInstance(def, 5)); // stack 1
        inv.Add(new ItemInstance(def, 3)); // stack 2

        Assert.True(inv.Contains("ammo", 7));
        Assert.False(inv.Contains("ammo", 9));
    }
}
