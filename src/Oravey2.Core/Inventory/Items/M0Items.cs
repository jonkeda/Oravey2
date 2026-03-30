namespace Oravey2.Core.Inventory.Items;

public static class M0Items
{
    public static ItemDefinition PipeWrench() => new(
        Id: "pipe_wrench",
        Name: "Pipe Wrench",
        Description: "A heavy pipe wrench. Better than bare fists.",
        Category: ItemCategory.WeaponMelee,
        Weight: 2.5f,
        Stackable: false,
        Value: 15,
        Slot: EquipmentSlot.PrimaryWeapon,
        Weapon: new WeaponData(
            Damage: 14,
            Range: 2f,
            ApCost: 3,
            Accuracy: 0.80f,
            SkillType: "melee",
            CritMultiplier: 1.5f));

    public static ItemDefinition Medkit() => new(
        Id: "medkit",
        Name: "Medkit",
        Description: "Restores 30 HP. A salvaged first-aid kit.",
        Category: ItemCategory.Consumable,
        Weight: 0.5f,
        Stackable: true,
        Value: 25,
        MaxStack: 5,
        Effects: new Dictionary<string, string> { { "heal", "30" } });

    public static ItemDefinition ScrapMetal() => new(
        Id: "scrap_metal",
        Name: "Scrap Metal",
        Description: "Twisted metal fragments. Useful for crafting.",
        Category: ItemCategory.CraftingMaterial,
        Weight: 1.0f,
        Stackable: true,
        Value: 3,
        MaxStack: 20);

    public static ItemDefinition LeatherJacket() => new(
        Id: "leather_jacket",
        Name: "Leather Jacket",
        Description: "Worn but sturdy. Offers some protection.",
        Category: ItemCategory.Armor,
        Weight: 3.0f,
        Stackable: false,
        Value: 20,
        Slot: EquipmentSlot.Torso,
        Armor: new ArmorData(
            DamageReduction: 3,
            CoverageZones: new Dictionary<string, float>
            {
                { "torso", 0.8f }, { "arms", 0.5f }
            }));
}
