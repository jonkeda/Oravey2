using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;

namespace Oravey2.Tests.Inventory;

public class InventoryProcessorTests
{
    private static ItemDefinition MakeItem(string id = "item", float weight = 5f,
        EquipmentSlot? slot = null)
        => new(id, id, "", ItemCategory.Junk, weight, false, 10, Slot: slot);

    private static (InventoryProcessor proc, InventoryComponent inv,
        EquipmentComponent equip, EventBus bus) Create()
    {
        var stats = new StatsComponent(); // Str=5 → carry=100
        var inv = new InventoryComponent(stats);
        var equip = new EquipmentComponent();
        var bus = new EventBus();
        var proc = new InventoryProcessor(inv, equip, bus);
        return (proc, inv, equip, bus);
    }

    [Fact]
    public void TryPickup_Success_PublishesEvent()
    {
        var (proc, inv, _, bus) = Create();
        var events = new List<ItemPickedUpEvent>();
        bus.Subscribe<ItemPickedUpEvent>(e => events.Add(e));

        var result = proc.TryPickup(new ItemInstance(MakeItem("sword")));

        Assert.True(result);
        Assert.Single(inv.Items);
        Assert.Single(events);
        Assert.Equal("sword", events[0].ItemId);
    }

    [Fact]
    public void TryPickup_Overweight_Fails()
    {
        var stats = new StatsComponent(new Dictionary<Stat, int>
        {
            { Stat.Strength, 1 }, { Stat.Perception, 5 }, { Stat.Endurance, 5 },
            { Stat.Charisma, 5 }, { Stat.Intelligence, 5 }, { Stat.Agility, 5 },
            { Stat.Luck, 2 },
        });
        var inv = new InventoryComponent(stats);
        var equip = new EquipmentComponent();
        var bus = new EventBus();
        var proc = new InventoryProcessor(inv, equip, bus);

        var result = proc.TryPickup(new ItemInstance(MakeItem("heavy", weight: 70f)));
        Assert.False(result);
        Assert.Empty(inv.Items);
    }

    [Fact]
    public void TryEquip_MovesFromInventory()
    {
        var (proc, inv, equip, _) = Create();
        var weapon = MakeItem("sword", slot: EquipmentSlot.PrimaryWeapon);
        var instance = new ItemInstance(weapon);
        inv.Add(instance);

        var result = proc.TryEquip(instance, EquipmentSlot.PrimaryWeapon);

        Assert.True(result);
        Assert.Equal(instance, equip.GetEquipped(EquipmentSlot.PrimaryWeapon));
    }

    [Fact]
    public void TryEquip_SwapsOldItem()
    {
        var (proc, inv, equip, _) = Create();
        var weapon1 = new ItemInstance(MakeItem("sword1", slot: EquipmentSlot.PrimaryWeapon));
        var weapon2 = new ItemInstance(MakeItem("sword2", slot: EquipmentSlot.PrimaryWeapon));
        inv.Add(weapon1);
        inv.Add(weapon2);

        proc.TryEquip(weapon1, EquipmentSlot.PrimaryWeapon);
        proc.TryEquip(weapon2, EquipmentSlot.PrimaryWeapon);

        Assert.Equal(weapon2, equip.GetEquipped(EquipmentSlot.PrimaryWeapon));
        Assert.True(inv.Contains("sword1"));
    }

    [Fact]
    public void TryEquip_WrongSlot_Fails()
    {
        var (proc, inv, _, _) = Create();
        var weapon = new ItemInstance(MakeItem("sword", slot: EquipmentSlot.PrimaryWeapon));
        inv.Add(weapon);

        Assert.False(proc.TryEquip(weapon, EquipmentSlot.Head));
    }

    [Fact]
    public void TryUnequip_ReturnsToInventory()
    {
        var (proc, inv, equip, _) = Create();
        var weapon = new ItemInstance(MakeItem("sword", slot: EquipmentSlot.PrimaryWeapon));
        inv.Add(weapon);
        proc.TryEquip(weapon, EquipmentSlot.PrimaryWeapon);

        var result = proc.TryUnequip(EquipmentSlot.PrimaryWeapon);

        Assert.True(result);
        Assert.Null(equip.GetEquipped(EquipmentSlot.PrimaryWeapon));
        Assert.True(inv.Contains("sword"));
    }

    [Fact]
    public void TryDrop_PublishesEvent()
    {
        var (proc, inv, _, bus) = Create();
        var events = new List<ItemDroppedEvent>();
        bus.Subscribe<ItemDroppedEvent>(e => events.Add(e));
        inv.Add(new ItemInstance(MakeItem("junk")));

        var result = proc.TryDrop("junk");

        Assert.True(result);
        Assert.Empty(inv.Items);
        Assert.Single(events);
        Assert.Equal("junk", events[0].ItemId);
    }
}
