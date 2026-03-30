namespace Oravey2.Core.Inventory.Items;

public sealed record WeaponData(
    int Damage,
    float Range,
    int ApCost,
    float Accuracy,
    string SkillType,
    string? AmmoType = null,
    float? FireRate = null,
    float CritMultiplier = 2.0f);

public sealed record ArmorData(
    int DamageReduction,
    Dictionary<string, float> CoverageZones);

public sealed record DurabilityData(
    int MaxDurability,
    float DegradePerUse);

public sealed record ItemDefinition(
    string Id,
    string Name,
    string Description,
    ItemCategory Category,
    float Weight,
    bool Stackable,
    int Value,
    int MaxStack = 1,
    EquipmentSlot? Slot = null,
    Dictionary<string, string>? Effects = null,
    WeaponData? Weapon = null,
    ArmorData? Armor = null,
    DurabilityData? Durability = null,
    string? Icon = null,
    string[]? Tags = null);
