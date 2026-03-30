# Design: Step 02 — Character & Inventory

Implements the character system (stats, skills, health, levelling, perks) and weight-based inventory per [docs/steps/02-character-inventory.md](../../docs/steps/02-character-inventory.md).

---

## File Layout

All new files go in `src/Oravey2.Core/`. Tests in `tests/Oravey2.Tests/`.

```
src/Oravey2.Core/
├── Character/
│   ├── Stats/
│   │   ├── Stat.cs                  # enum
│   │   ├── StatModifier.cs          # record
│   │   └── StatsComponent.cs        # component
│   ├── Skills/
│   │   ├── SkillType.cs             # enum
│   │   └── SkillsComponent.cs       # component
│   ├── Health/
│   │   ├── StatusEffectType.cs      # enum
│   │   ├── StatusEffect.cs          # record
│   │   └── HealthComponent.cs       # component
│   ├── Level/
│   │   ├── LevelComponent.cs        # component
│   │   └── LevelFormulas.cs         # static helpers
│   └── Perks/
│       ├── PerkCondition.cs          # record
│       ├── PerkDefinition.cs         # record
│       └── PerkTreeComponent.cs      # component
├── Inventory/
│   ├── Core/
│   │   ├── InventoryComponent.cs     # component
│   │   └── InventoryProcessor.cs     # enforces rules, publishes events
│   ├── Items/
│   │   ├── ItemDefinition.cs         # data model (from JSON)
│   │   ├── ItemInstance.cs           # runtime instance (def + stack + durability)
│   │   ├── ItemCategory.cs           # enum
│   │   └── EquipmentSlot.cs          # enum
│   └── Equipment/
│       └── EquipmentComponent.cs     # equipped slots
├── Framework/
│   └── Events/
│       └── GameEvents.cs             # add new events (existing file)
tests/Oravey2.Tests/
├── Character/
│   ├── StatsComponentTests.cs
│   ├── SkillsComponentTests.cs
│   ├── HealthComponentTests.cs
│   ├── LevelComponentTests.cs
│   ├── LevelFormulasTests.cs
│   └── PerkTreeComponentTests.cs
└── Inventory/
    ├── InventoryComponentTests.cs
    └── InventoryProcessorTests.cs
```

---

## Enums

### Stat.cs

```csharp
namespace Oravey2.Core.Character.Stats;

public enum Stat
{
    Strength,
    Perception,
    Endurance,
    Charisma,
    Intelligence,
    Agility,
    Luck
}
```

### SkillType.cs

```csharp
namespace Oravey2.Core.Character.Skills;

public enum SkillType
{
    Firearms,
    Melee,
    Survival,
    Science,
    Speech,
    Stealth,
    Mechanics
}
```

### StatusEffectType.cs

```csharp
namespace Oravey2.Core.Character.Health;

public enum StatusEffectType
{
    Poisoned,
    Bleeding,
    Irradiated,
    Stunned,
    Crippled
}
```

### ItemCategory.cs

```csharp
namespace Oravey2.Core.Inventory.Items;

public enum ItemCategory
{
    WeaponMelee,
    WeaponRanged,
    Armor,
    Consumable,
    Ammo,
    CraftingMaterial,
    QuestItem,
    Junk,
    Schematic
}
```

### EquipmentSlot.cs

```csharp
namespace Oravey2.Core.Inventory.Items;

public enum EquipmentSlot
{
    Head,
    Torso,
    Legs,
    Feet,
    PrimaryWeapon,
    SecondaryWeapon,
    Accessory1,
    Accessory2
}
```

---

## Records & Data Models

### StatModifier.cs

```csharp
namespace Oravey2.Core.Character.Stats;

public sealed record StatModifier(Stat Stat, int Amount, string Source);
```

### StatusEffect.cs

```csharp
namespace Oravey2.Core.Character.Health;

public sealed record StatusEffect(StatusEffectType Type, float Duration, float Intensity);
```

### PerkCondition.cs

```csharp
namespace Oravey2.Core.Character.Perks;

using Oravey2.Core.Character.Stats;

public sealed record PerkCondition(
    int RequiredLevel,
    Stat? RequiredStat = null,
    int? StatThreshold = null,
    string? RequiredPerk = null);
```

### PerkDefinition.cs

```csharp
namespace Oravey2.Core.Character.Perks;

public sealed record PerkDefinition(
    string Id,
    string Name,
    string Description,
    PerkCondition Condition,
    string[] Effects,
    string[]? MutuallyExclusive = null);
```

### ItemDefinition.cs

Matches the [items schema](../../docs/schemas/items.md). Weapon/armor data embedded as nullable sub-records.

```csharp
namespace Oravey2.Core.Inventory.Items;

public sealed record WeaponData(
    int Damage,
    float Range,
    int ApCost,
    float Accuracy,
    string SkillType,       // "firearms" or "melee"
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
```

### ItemInstance.cs

Runtime wrapper around a definition. Tracks stack count and current durability.

```csharp
namespace Oravey2.Core.Inventory.Items;

public sealed class ItemInstance
{
    public ItemDefinition Definition { get; }
    public int StackCount { get; set; }
    public int? CurrentDurability { get; set; }

    public float TotalWeight => Definition.Weight * StackCount;

    public ItemInstance(ItemDefinition definition, int stackCount = 1)
    {
        Definition = definition;
        StackCount = stackCount;
        CurrentDurability = definition.Durability?.MaxDurability;
    }
}
```

---

## Components

### StatsComponent.cs

```csharp
namespace Oravey2.Core.Character.Stats;

public class StatsComponent
{
    private readonly Dictionary<Stat, int> _baseStats = new();
    private readonly List<StatModifier> _modifiers = [];

    public IReadOnlyDictionary<Stat, int> BaseStats => _baseStats;
    public IReadOnlyList<StatModifier> Modifiers => _modifiers;

    public StatsComponent(Dictionary<Stat, int>? initial = null)
    {
        // Default all stats to 5
        foreach (var stat in Enum.GetValues<Stat>())
            _baseStats[stat] = 5;

        if (initial != null)
        {
            foreach (var (stat, value) in initial)
                _baseStats[stat] = Math.Clamp(value, 1, 10);
        }
    }

    public int GetBase(Stat stat) => _baseStats[stat];

    public void SetBase(Stat stat, int value)
        => _baseStats[stat] = Math.Clamp(value, 1, 10);

    public int GetEffective(Stat stat)
    {
        var total = _baseStats[stat];
        foreach (var mod in _modifiers)
        {
            if (mod.Stat == stat) total += mod.Amount;
        }
        return Math.Clamp(total, 1, 99);  // modifiers can push past 10
    }

    public void AddModifier(StatModifier mod) => _modifiers.Add(mod);

    public void RemoveModifier(StatModifier mod) => _modifiers.Remove(mod);

    /// <summary>
    /// Validate a point-buy allocation: 28 points total, each stat 1-10.
    /// </summary>
    public static bool IsValidAllocation(Dictionary<Stat, int> stats)
    {
        if (stats.Count != 7) return false;
        var total = 0;
        foreach (var (_, value) in stats)
        {
            if (value < 1 || value > 10) return false;
            total += value;
        }
        return total == 28;
    }
}
```

### SkillsComponent.cs

```csharp
namespace Oravey2.Core.Character.Skills;

using Oravey2.Core.Character.Stats;

public class SkillsComponent
{
    private readonly Dictionary<SkillType, int> _baseSkills = new();
    private readonly Dictionary<SkillType, int> _skillXP = new();
    private readonly StatsComponent _stats;

    public IReadOnlyDictionary<SkillType, int> BaseSkills => _baseSkills;

    /// <summary>Skill → linked SPECIAL stat, per GAME_CONSTANTS.md §3.</summary>
    private static readonly Dictionary<SkillType, Stat> SkillStatLinks = new()
    {
        { SkillType.Firearms, Stat.Perception },
        { SkillType.Melee, Stat.Strength },
        { SkillType.Survival, Stat.Endurance },
        { SkillType.Science, Stat.Intelligence },
        { SkillType.Speech, Stat.Charisma },
        { SkillType.Stealth, Stat.Agility },
        { SkillType.Mechanics, Stat.Intelligence },
    };

    public SkillsComponent(StatsComponent stats)
    {
        _stats = stats;
        // Starting value = 10 + (linked_stat × 2)
        foreach (var skill in Enum.GetValues<SkillType>())
        {
            var linked = SkillStatLinks[skill];
            _baseSkills[skill] = 10 + stats.GetBase(linked) * 2;
            _skillXP[skill] = 0;
        }
    }

    public int GetBase(SkillType skill) => _baseSkills[skill];

    public int GetEffective(SkillType skill)
    {
        // Base + stat bonus (effective stat minus base stat used at creation)
        var linked = SkillStatLinks[skill];
        var statBonus = (_stats.GetEffective(linked) - _stats.GetBase(linked)) * 2;
        return Math.Clamp(_baseSkills[skill] + statBonus, 0, 100);
    }

    public void AllocatePoints(SkillType skill, int points)
    {
        _baseSkills[skill] = Math.Clamp(_baseSkills[skill] + points, 0, 100);
    }

    /// <summary>
    /// Use-based skill XP. Threshold to level up = current_level × 5.
    /// Returns true if the skill levelled up.
    /// </summary>
    public bool AddXP(SkillType skill, int amount)
    {
        _skillXP[skill] += amount;
        var threshold = _baseSkills[skill] * 5;
        if (_skillXP[skill] >= threshold && _baseSkills[skill] < 100)
        {
            _baseSkills[skill]++;
            _skillXP[skill] -= threshold;
            return true;
        }
        return false;
    }

    public static Stat GetLinkedStat(SkillType skill) => SkillStatLinks[skill];
}
```

### HealthComponent.cs

```csharp
namespace Oravey2.Core.Character.Health;

using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;

public class HealthComponent
{
    private readonly StatsComponent _stats;
    private readonly LevelComponent _level;
    private readonly IEventBus? _eventBus;
    private readonly List<StatusEffect> _activeEffects = [];

    public int CurrentHP { get; private set; }
    public int MaxHP => LevelFormulas.MaxHP(_stats.GetEffective(Stat.Endurance), _level.Level);
    public int RadiationLevel { get; set; }
    public IReadOnlyList<StatusEffect> ActiveEffects => _activeEffects;
    public bool IsAlive => CurrentHP > 0;

    public HealthComponent(StatsComponent stats, LevelComponent level, IEventBus? eventBus = null)
    {
        _stats = stats;
        _level = level;
        _eventBus = eventBus;
        CurrentHP = MaxHP;
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        var oldHP = CurrentHP;
        CurrentHP = Math.Max(0, CurrentHP - amount);
        _eventBus?.Publish(new HealthChangedEvent(oldHP, CurrentHP));
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        var oldHP = CurrentHP;
        CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
        _eventBus?.Publish(new HealthChangedEvent(oldHP, CurrentHP));
    }

    public void HealToMax()
    {
        var oldHP = CurrentHP;
        CurrentHP = MaxHP;
        if (oldHP != CurrentHP)
            _eventBus?.Publish(new HealthChangedEvent(oldHP, CurrentHP));
    }

    public void ApplyEffect(StatusEffect effect) => _activeEffects.Add(effect);

    public void RemoveEffect(StatusEffectType type)
        => _activeEffects.RemoveAll(e => e.Type == type);
}
```

### LevelComponent.cs

```csharp
namespace Oravey2.Core.Character.Level;

using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;

public class LevelComponent
{
    private readonly StatsComponent _stats;
    private readonly IEventBus? _eventBus;

    public int Level { get; private set; } = 1;
    public int CurrentXP { get; private set; }
    public int XPToNextLevel => LevelFormulas.XPRequired(Level + 1);
    public int StatPointsAvailable { get; private set; }
    public int SkillPointsAvailable { get; private set; }
    public int PerkPointsAvailable { get; private set; }

    public const int MaxLevel = 30;

    public LevelComponent(StatsComponent stats, IEventBus? eventBus = null)
    {
        _stats = stats;
        _eventBus = eventBus;
    }

    public void GainXP(int amount)
    {
        if (amount <= 0 || Level >= MaxLevel) return;

        CurrentXP += amount;
        _eventBus?.Publish(new XPGainedEvent(amount));

        while (CurrentXP >= XPToNextLevel && Level < MaxLevel)
        {
            CurrentXP -= XPToNextLevel;
            var oldLevel = Level;
            Level++;

            StatPointsAvailable += 1;
            SkillPointsAvailable += LevelFormulas.SkillPointsPerLevel(
                _stats.GetEffective(Stat.Intelligence));

            // Perk every 2 levels
            if (Level % 2 == 0)
                PerkPointsAvailable++;

            _eventBus?.Publish(new LevelUpEvent(oldLevel, Level));
        }
    }

    public bool SpendStatPoint(Stat stat)
    {
        if (StatPointsAvailable <= 0) return false;
        if (_stats.GetBase(stat) >= 10) return false;
        _stats.SetBase(stat, _stats.GetBase(stat) + 1);
        StatPointsAvailable--;
        return true;
    }

    public bool SpendSkillPoints(Character.Skills.SkillType skill, int points,
        Character.Skills.SkillsComponent skills)
    {
        if (points <= 0 || points > SkillPointsAvailable) return false;
        skills.AllocatePoints(skill, points);
        SkillPointsAvailable -= points;
        return true;
    }
}
```

### LevelFormulas.cs

Pure static — no dependencies.

```csharp
namespace Oravey2.Core.Character.Level;

public static class LevelFormulas
{
    public static int XPRequired(int level) => 100 * level * level;

    public static int SkillPointsPerLevel(int intelligence) => 5 + intelligence / 2;

    public static int MaxHP(int endurance, int level) => 50 + endurance * 10 + level * 5;

    public static float CarryWeight(int strength) => 50f + strength * 10f;
}
```

### PerkTreeComponent.cs

```csharp
namespace Oravey2.Core.Character.Perks;

using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;

public class PerkTreeComponent
{
    private readonly List<PerkDefinition> _allPerks;
    private readonly HashSet<string> _unlocked = [];
    private readonly StatsComponent _stats;
    private readonly LevelComponent _level;

    public IReadOnlyList<PerkDefinition> AllPerks => _allPerks;
    public IReadOnlySet<string> UnlockedPerks => _unlocked;

    public PerkTreeComponent(
        IReadOnlyList<PerkDefinition> perks,
        StatsComponent stats,
        LevelComponent level)
    {
        _allPerks = [.. perks];
        _stats = stats;
        _level = level;
    }

    public bool CanUnlock(string perkId)
    {
        var perk = _allPerks.FirstOrDefault(p => p.Id == perkId);
        if (perk == null) return false;
        if (_unlocked.Contains(perkId)) return false;
        if (_level.PerkPointsAvailable <= 0) return false;
        if (_level.Level < perk.Condition.RequiredLevel) return false;

        if (perk.Condition.RequiredStat is { } stat &&
            perk.Condition.StatThreshold is { } threshold)
        {
            if (_stats.GetEffective(stat) < threshold) return false;
        }

        if (perk.Condition.RequiredPerk is { } reqPerk)
        {
            if (!_unlocked.Contains(reqPerk)) return false;
        }

        if (perk.MutuallyExclusive != null)
        {
            foreach (var excl in perk.MutuallyExclusive)
            {
                if (_unlocked.Contains(excl)) return false;
            }
        }

        return true;
    }

    public bool Unlock(string perkId)
    {
        if (!CanUnlock(perkId)) return false;
        _unlocked.Add(perkId);
        _level.PerkPointsAvailable--;  // needs internal setter — see note below
        return true;
    }
}
```

> **Note:** `LevelComponent.PerkPointsAvailable` needs an `internal set` so `PerkTreeComponent.Unlock` can decrement it. Both are in the same assembly.

### InventoryComponent.cs

```csharp
namespace Oravey2.Core.Inventory.Core;

using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Inventory.Items;

public class InventoryComponent
{
    private readonly List<ItemInstance> _items = [];
    private readonly StatsComponent _stats;

    public IReadOnlyList<ItemInstance> Items => _items;
    public float MaxCarryWeight => LevelFormulas.CarryWeight(_stats.GetEffective(Stat.Strength));
    public float CurrentWeight => _items.Sum(i => i.TotalWeight);
    public bool IsOverweight => CurrentWeight > MaxCarryWeight;

    public InventoryComponent(StatsComponent stats)
    {
        _stats = stats;
    }

    public bool CanAdd(ItemInstance item)
        => CurrentWeight + item.TotalWeight <= MaxCarryWeight;

    public bool Add(ItemInstance item)
    {
        // Try stacking first
        if (item.Definition.Stackable)
        {
            var existing = _items.FirstOrDefault(i =>
                i.Definition.Id == item.Definition.Id &&
                i.StackCount < i.Definition.MaxStack);

            if (existing != null)
            {
                var space = existing.Definition.MaxStack - existing.StackCount;
                var toAdd = Math.Min(space, item.StackCount);
                existing.StackCount += toAdd;
                item.StackCount -= toAdd;
                if (item.StackCount <= 0) return true;
            }
        }

        _items.Add(item);
        return true;
    }

    public bool Remove(string itemId, int count = 1)
    {
        var item = _items.FirstOrDefault(i => i.Definition.Id == itemId);
        if (item == null) return false;

        if (item.StackCount > count)
        {
            item.StackCount -= count;
            return true;
        }

        if (item.StackCount == count)
        {
            _items.Remove(item);
            return true;
        }

        return false;  // not enough
    }

    public bool Contains(string itemId, int count = 1)
        => _items.Where(i => i.Definition.Id == itemId).Sum(i => i.StackCount) >= count;
}
```

### EquipmentComponent.cs

```csharp
namespace Oravey2.Core.Inventory.Equipment;

using Oravey2.Core.Inventory.Items;

public class EquipmentComponent
{
    private readonly Dictionary<EquipmentSlot, ItemInstance?> _slots = new();

    public EquipmentComponent()
    {
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
            _slots[slot] = null;
    }

    public ItemInstance? GetEquipped(EquipmentSlot slot) => _slots[slot];

    public ItemInstance? Equip(ItemInstance item, EquipmentSlot slot)
    {
        if (item.Definition.Slot != slot) return null;  // wrong slot

        var previous = _slots[slot];
        _slots[slot] = item;
        return previous;  // caller handles returning to inventory
    }

    public ItemInstance? Unequip(EquipmentSlot slot)
    {
        var item = _slots[slot];
        _slots[slot] = null;
        return item;
    }

    public bool IsSlotOccupied(EquipmentSlot slot) => _slots[slot] != null;
}
```

### InventoryProcessor.cs

Orchestrates inventory operations, enforces rules, publishes events.

```csharp
namespace Oravey2.Core.Inventory.Core;

using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;

public class InventoryProcessor
{
    private readonly InventoryComponent _inventory;
    private readonly EquipmentComponent _equipment;
    private readonly IEventBus _eventBus;

    public InventoryProcessor(
        InventoryComponent inventory,
        EquipmentComponent equipment,
        IEventBus eventBus)
    {
        _inventory = inventory;
        _equipment = equipment;
        _eventBus = eventBus;
    }

    public bool TryPickup(ItemInstance item)
    {
        if (!_inventory.CanAdd(item)) return false;

        _inventory.Add(item);
        _eventBus.Publish(new ItemPickedUpEvent(item.Definition.Id));
        return true;
    }

    public bool TryDrop(string itemId, int count = 1)
    {
        if (!_inventory.Remove(itemId, count)) return false;

        _eventBus.Publish(new ItemDroppedEvent(itemId, count));
        return true;
    }

    public bool TryEquip(ItemInstance item, EquipmentSlot slot)
    {
        if (item.Definition.Slot != slot) return false;

        var previous = _equipment.Equip(item, slot);
        _inventory.Remove(item.Definition.Id, 1);

        if (previous != null)
            _inventory.Add(previous);

        _eventBus.Publish(new ItemEquippedEvent(item.Definition.Id, slot));
        return true;
    }

    public bool TryUnequip(EquipmentSlot slot)
    {
        var item = _equipment.Unequip(slot);
        if (item == null) return false;

        _inventory.Add(item);
        _eventBus.Publish(new ItemUnequippedEvent(slot));
        return true;
    }
}
```

---

## Events to Add

Add to `src/Oravey2.Core/Framework/Events/GameEvents.cs`:

```csharp
public readonly record struct HealthChangedEvent(int OldHP, int NewHP) : IGameEvent;
public readonly record struct XPGainedEvent(int Amount) : IGameEvent;
public readonly record struct LevelUpEvent(int OldLevel, int NewLevel) : IGameEvent;
public readonly record struct ItemPickedUpEvent(string ItemId) : IGameEvent;
public readonly record struct ItemDroppedEvent(string ItemId, int Count) : IGameEvent;
public readonly record struct ItemEquippedEvent(string ItemId, EquipmentSlot Slot) : IGameEvent;
public readonly record struct ItemUnequippedEvent(EquipmentSlot Slot) : IGameEvent;
```

> **Note:** The CLASS_ARCHITECTURE.md events include `Entity` references. We defer that to when Stride ECS integration is needed (Step 3 combat). For now, events carry value types only — simpler to test, no Entity mocking.

---

## Tests

### LevelFormulasTests.cs — pure math, no dependencies

| Test | Assertion |
|------|-----------|
| `XPRequired_Level1` | `XPRequired(1) == 100` |
| `XPRequired_Level10` | `XPRequired(10) == 10000` |
| `XPRequired_Level30` | `XPRequired(30) == 90000` |
| `SkillPointsPerLevel_Int5` | `SkillPointsPerLevel(5) == 7` |
| `SkillPointsPerLevel_Int10` | `SkillPointsPerLevel(10) == 10` |
| `MaxHP_End5_Level1` | `MaxHP(5, 1) == 105` |
| `MaxHP_End10_Level30` | `MaxHP(10, 30) == 300` |
| `CarryWeight_Str5` | `CarryWeight(5) == 100f` |
| `CarryWeight_Str1` | `CarryWeight(1) == 60f` |

### StatsComponentTests.cs

| Test | Assertion |
|------|-----------|
| `DefaultStats_AllFive` | All 7 stats = 5 |
| `CustomAllocation_Applies` | Pass custom dict, values stick |
| `SetBase_ClampsTo1_10` | SetBase(stat, 0) → 1, SetBase(stat, 15) → 10 |
| `Modifier_IncreasesEffective` | Add +2 Str mod → GetEffective(Str) = 7 |
| `RemoveModifier_RestoresEffective` | Remove mod → back to 5 |
| `MultipleModifiers_Stack` | Two +1 mods → effective = 7 |
| `IsValidAllocation_28Points` | 7×4 = 28 ✓ |
| `IsValidAllocation_WrongTotal_Fails` | 7×5 = 35 ✗ |
| `IsValidAllocation_StatBelowMin_Fails` | stat=0 ✗ |

### SkillsComponentTests.cs

| Test | Assertion |
|------|-----------|
| `StartingValues_DerivedFromStats` | Stat=5 → skill=20 |
| `AllocatePoints_Increases` | Allocate 10 → skill=30 |
| `AllocatePoints_ClampsAt100` | Allocate 200 → 100 |
| `GetEffective_IncludesStatBonus` | Add Str modifier → Melee effective rises |
| `AddXP_BelowThreshold_NoLevelUp` | 1 XP when skill=20 → no change |
| `AddXP_MeetsThreshold_LevelsUp` | Add 100 XP when skill=20 (threshold=100) → skill=21 |

### HealthComponentTests.cs

| Test | Assertion |
|------|-----------|
| `InitialHP_EqualsMaxHP` | CurrentHP = MaxHP(End=5, Lvl=1) = 105 |
| `TakeDamage_ReducesHP` | TakeDamage(20) → HP=85 |
| `TakeDamage_ClampsAtZero` | TakeDamage(200) → HP=0 |
| `TakeDamage_NegativeIgnored` | TakeDamage(-5) → no change |
| `Heal_IncreasesHP` | Heal(10) after damage → HP goes up |
| `Heal_ClampsAtMax` | Heal(999) → HP = MaxHP |
| `IsAlive_TrueAboveZero` | HP=1 → true |
| `IsAlive_FalseAtZero` | HP=0 → false |
| `ApplyEffect_AddsToList` | Apply Poisoned → in ActiveEffects |
| `RemoveEffect_RemovesFromList` | Remove Poisoned → gone |

### LevelComponentTests.cs

| Test | Assertion |
|------|-----------|
| `StartsAtLevel1` | Level=1, XP=0 |
| `GainXP_NoLevelUp` | Gain 100 XP (need 400) → Level=1, XP=100 |
| `GainXP_LevelUp` | Gain 400 XP → Level=2, statPoints=1, skillPoints=7 |
| `GainXP_MultipleLevels` | Gain 1300 XP → Level=3 |
| `GainXP_PerkPointEvery2Levels` | Level to 4 → PerkPoints=2 |
| `GainXP_CapsAtMaxLevel` | Can't exceed level 30 |
| `SpendStatPoint_IncreasesBase` | Spend → stat goes up, points decrease |
| `SpendStatPoint_FailsWhenNone` | 0 points → returns false |
| `SpendStatPoint_FailsAtMax10` | Stat=10 → returns false |

### PerkTreeComponentTests.cs

| Test | Assertion |
|------|-----------|
| `CanUnlock_MeetsConditions` | Level 2, Str ≥ 3, perk point → true |
| `CanUnlock_LevelTooLow` | Level 1, needs 2 → false |
| `CanUnlock_StatTooLow` | Str=2, needs 3 → false |
| `CanUnlock_NoPerkPoints` | 0 perk points → false |
| `CanUnlock_AlreadyUnlocked` | Unlock twice → second false |
| `CanUnlock_RequiredPerkMissing` | Chain perk without prerequisite → false |
| `CanUnlock_MutuallyExclusive` | Has excluded perk → false |
| `Unlock_DecrementsPerkPoints` | Points go from 1 to 0 |

### InventoryComponentTests.cs

| Test | Assertion |
|------|-----------|
| `Empty_WeightIsZero` | CurrentWeight = 0 |
| `Add_IncreasesWeight` | Add 5lb item → weight=5 |
| `CanAdd_OverWeight_ReturnsFalse` | Str=1 (60lb cap), 70lb item → false |
| `Add_StacksMatchingItems` | Add 2 of same stackable → count=2 |
| `Add_RespectsMaxStack` | MaxStack=5, add 6 → splits |
| `Remove_DecreasesStack` | Remove 1 from stack of 3 → 2 |
| `Remove_RemovesItemAtZero` | Remove last → item gone |
| `Remove_NotEnough_ReturnsFalse` | Have 1, remove 2 → false |
| `Contains_ChecksAcrossStacks` | 2 stacks of 3 → Contains(id, 5) true |

### InventoryProcessorTests.cs

| Test | Assertion |
|------|-----------|
| `TryPickup_Success_PublishesEvent` | Event published with item ID |
| `TryPickup_Overweight_Fails` | Returns false, no event |
| `TryEquip_MovesFromInventory` | Item in equipment slot, removed from inventory |
| `TryEquip_SwapsOldItem` | Previous item returns to inventory |
| `TryEquip_WrongSlot_Fails` | Weapon to Head → false |
| `TryUnequip_ReturnsToInventory` | Item back in inventory |
| `TryDrop_PublishesEvent` | Event published with item ID and count |

---

## Execution Order

1. `LevelFormulas.cs` + `LevelFormulasTests.cs` — pure math, no deps
2. `Stat.cs`, `StatModifier.cs`, `StatsComponent.cs` + `StatsComponentTests.cs`
3. `SkillType.cs`, `SkillsComponent.cs` + `SkillsComponentTests.cs` — depends on StatsComponent
4. Events in `GameEvents.cs` — add new event records
5. `LevelComponent.cs` + `LevelComponentTests.cs` — depends on Stats + Events
6. `HealthComponent.cs` + `HealthComponentTests.cs` — depends on Stats + Level + Events
7. `PerkCondition.cs`, `PerkDefinition.cs`, `PerkTreeComponent.cs` + `PerkTreeComponentTests.cs` — depends on Stats + Level
8. `ItemCategory.cs`, `EquipmentSlot.cs`, `ItemDefinition.cs`, `ItemInstance.cs` — data models, no deps
9. `InventoryComponent.cs` + `InventoryComponentTests.cs` — depends on Stats (for carry weight)
10. `EquipmentComponent.cs` — thin slot map
11. `InventoryProcessor.cs` + `InventoryProcessorTests.cs` — depends on Inventory + Equipment + Events
12. Run full test suite — all unit + UI tests pass
