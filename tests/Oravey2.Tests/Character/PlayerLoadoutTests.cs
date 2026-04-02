using Oravey2.Core.Character.Stats;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;
using Xunit;

namespace Oravey2.Tests.Character;

public class PlayerLoadoutTests
{
    private static (InventoryComponent inventory, EquipmentComponent equipment) CreateDefaultLoadout()
    {
        var stats = new StatsComponent();
        var inventory = new InventoryComponent(stats);
        var equipment = new EquipmentComponent();

        // Equip pipe wrench (same as ScenarioLoader.CreatePlayer)
        var wrench = new ItemInstance(M0Items.PipeWrench());
        equipment.Equip(wrench, EquipmentSlot.PrimaryWeapon);

        // Add 2 medkits
        inventory.Add(new ItemInstance(M0Items.Medkit(), 2));

        return (inventory, equipment);
    }

    [Fact]
    public void DefaultLoadout_HasPipeWrench()
    {
        var (_, equipment) = CreateDefaultLoadout();
        var weapon = equipment.GetEquipped(EquipmentSlot.PrimaryWeapon);
        Assert.NotNull(weapon);
        Assert.Equal("pipe_wrench", weapon.Definition.Id);
    }

    [Fact]
    public void DefaultLoadout_HasTwoMedkits()
    {
        var (inventory, _) = CreateDefaultLoadout();
        var medkit = inventory.Items.FirstOrDefault(i => i.Definition.Id == "medkit");
        Assert.NotNull(medkit);
        Assert.Equal(2, medkit.StackCount);
    }

    [Fact]
    public void DefaultLoadout_WeightIs1_0()
    {
        var (inventory, _) = CreateDefaultLoadout();
        Assert.Equal(1.0, inventory.CurrentWeight, 0.01);
    }

    [Fact]
    public void DefaultLoadout_NotOverweight()
    {
        var (inventory, _) = CreateDefaultLoadout();
        Assert.False(inventory.IsOverweight);
    }

    [Fact]
    public void DefaultLoadout_OnlyPrimaryWeaponEquipped()
    {
        var (_, equipment) = CreateDefaultLoadout();
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            if (slot == EquipmentSlot.PrimaryWeapon)
                Assert.NotNull(equipment.GetEquipped(slot));
            else
                Assert.Null(equipment.GetEquipped(slot));
        }
    }

    [Fact]
    public void EquipItem_SetsSlot()
    {
        var equipment = new EquipmentComponent();
        var jacket = new ItemInstance(M0Items.LeatherJacket());
        equipment.Equip(jacket, EquipmentSlot.Torso);

        var equipped = equipment.GetEquipped(EquipmentSlot.Torso);
        Assert.NotNull(equipped);
        Assert.Equal("leather_jacket", equipped.Definition.Id);
    }
}
