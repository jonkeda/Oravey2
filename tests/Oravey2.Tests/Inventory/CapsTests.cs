using Oravey2.Core.Character.Stats;
using Oravey2.Core.Inventory.Core;

namespace Oravey2.Tests.Inventory;

public class CapsTests
{
    [Fact]
    public void Caps_DefaultsTo50()
    {
        var stats = new StatsComponent();
        var inventory = new InventoryComponent(stats);
        Assert.Equal(50, inventory.Caps);
    }

    [Fact]
    public void Caps_CanBeSet()
    {
        var stats = new StatsComponent();
        var inventory = new InventoryComponent(stats) { Caps = 100 };
        Assert.Equal(100, inventory.Caps);
    }

    [Fact]
    public void DeathPenalty_10Percent()
    {
        var stats = new StatsComponent();
        var inventory = new InventoryComponent(stats) { Caps = 50 };
        var lost = inventory.ApplyDeathPenalty();
        Assert.Equal(5, lost);
        Assert.Equal(45, inventory.Caps);
    }

    [Fact]
    public void DeathPenalty_MinZero()
    {
        var stats = new StatsComponent();
        var inventory = new InventoryComponent(stats) { Caps = 3 };
        var lost = inventory.ApplyDeathPenalty();
        Assert.Equal(0, lost); // 10% of 3 rounds down to 0
        Assert.Equal(3, inventory.Caps);
    }

    [Fact]
    public void DeathPenalty_NeverNegative()
    {
        var stats = new StatsComponent();
        var inventory = new InventoryComponent(stats) { Caps = 0 };
        var lost = inventory.ApplyDeathPenalty();
        Assert.Equal(0, lost);
        Assert.Equal(0, inventory.Caps);
    }

    [Fact]
    public void DeathPenalty_LargeAmount()
    {
        var stats = new StatsComponent();
        var inventory = new InventoryComponent(stats) { Caps = 1000 };
        var lost = inventory.ApplyDeathPenalty();
        Assert.Equal(100, lost);
        Assert.Equal(900, inventory.Caps);
    }

    [Fact]
    public void DeathPenalty_RepeatedDeaths()
    {
        var stats = new StatsComponent();
        var inventory = new InventoryComponent(stats) { Caps = 100 };
        inventory.ApplyDeathPenalty(); // 100 -> 90
        inventory.ApplyDeathPenalty(); // 90 -> 81
        Assert.Equal(81, inventory.Caps);
    }
}
