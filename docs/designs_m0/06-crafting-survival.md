# Design: Step 06 — Crafting & Survival

Implements crafting stations, recipe system, durability management, survival needs (hunger/thirst/fatigue), and radiation per [docs/steps/06-crafting-survival.md](../steps/06-crafting-survival.md). Balance constants from [GAME_CONSTANTS.md](../constants/GAME_CONSTANTS.md) §5–§6. Data model follows [docs/schemas/recipes.md](../schemas/recipes.md).

**Depends on:** Step 2 (InventoryComponent, SkillsComponent, StatsComponent, HealthComponent, LevelComponent, ItemInstance/ItemDefinition, StatModifier), Step 5 (WorldStateService)

---

## File Layout

All new files go in `src/Oravey2.Core/`. Tests in `tests/Oravey2.Tests/`.

```
src/Oravey2.Core/
├── Crafting/
│   ├── StationType.cs            # enum
│   ├── RecipeDefinition.cs       # record — inputs, output, station, skill req
│   ├── CraftingProcessor.cs      # validates + executes crafting
│   └── RepairProcessor.cs        # repair items at station or via NPC
├── Survival/
│   ├── SurvivalComponent.cs      # hunger, thirst, fatigue values + enabled toggle
│   ├── SurvivalProcessor.cs      # ticks needs, evaluates thresholds, applies debuffs
│   ├── SurvivalThreshold.cs      # enum — WellFed/Normal/Deprived/Critical
│   ├── RadiationComponent.cs     # radiation level 0–1000
│   └── RadiationProcessor.cs     # evaluate rad level, apply/remove debuffs
├── Inventory/
│   └── Items/
│       └── DurabilityHelper.cs   # static: degrade, repair, isBroken checks on ItemInstance
├── Framework/
│   └── Events/
│       └── GameEvents.cs         # add new events (existing file)
tests/Oravey2.Tests/
├── Crafting/
│   ├── CraftingProcessorTests.cs
│   └── RepairProcessorTests.cs
├── Survival/
│   ├── SurvivalComponentTests.cs
│   ├── SurvivalProcessorTests.cs
│   ├── RadiationComponentTests.cs
│   └── RadiationProcessorTests.cs
└── Inventory/
    └── DurabilityHelperTests.cs
```

### Deferred to Stride Integration

| Deliverable | Reason |
|-------------|--------|
| `CraftingStationComponent` (EntityComponent) | Stride ECS attachment — wraps StationType + recipe list |
| Crafting UI | Stride UI framework — recipe list, ingredient check display |
| `SurvivalProcessor` per-frame tick | Requires Stride SyncScript + GameTime delta |
| `RadiationProcessor` zone detection | Requires player position + radiation zone data (Step 7) |
| Consumable item usage flow | Requires item-use pipeline (InventoryProcessor + effects) |
| Recipe discovery system | Requires schematic use pipeline + dialogue integration |
| Crafting SFX / UI feedback | Requires AudioService (Step 9) |

The pure C# classes designed here use direct component references and value types — fully testable without the engine.

---

## Enums

### StationType.cs

```csharp
namespace Oravey2.Core.Crafting;

public enum StationType
{
    Workbench,
    ChemLab,
    CookingFire
}
```

### SurvivalThreshold.cs

Computed from the 0–100 need value. Maps to the four-tier system from GAME_CONSTANTS §6.

```csharp
namespace Oravey2.Core.Survival;

public enum SurvivalThreshold
{
    Satisfied,   // 0–25: buff active
    Normal,      // 26–50: no effects
    Deprived,    // 51–75: stat debuff
    Critical     // 76–100: HP drain + debuff
}
```

---

## Crafting

### RecipeDefinition.cs

Immutable data record matching [docs/schemas/recipes.md](../schemas/recipes.md).

```csharp
namespace Oravey2.Core.Crafting;

using Oravey2.Core.Character.Skills;

public sealed record RecipeDefinition(
    string Id,
    string Name,
    string OutputItemId,
    int OutputCount,
    IReadOnlyDictionary<string, int> Ingredients,
    StationType RequiredStation,
    SkillType? RequiredSkill = null,
    int SkillThreshold = 0);
```

### CraftingProcessor.cs

Validates ingredients and skill, consumes inputs, produces output item. Takes an `ItemFactory` delegate to create output `ItemInstance` from ID — avoids hard dependency on an item registry.

```csharp
namespace Oravey2.Core.Crafting;

using Oravey2.Core.Character.Skills;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;

public sealed class CraftingProcessor
{
    private readonly IEventBus _eventBus;

    public CraftingProcessor(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Check if a recipe can be crafted: all ingredients present, skill met,
    /// and station type matches.
    /// </summary>
    public bool CanCraft(
        InventoryComponent inventory,
        SkillsComponent skills,
        RecipeDefinition recipe,
        StationType currentStation)
    {
        if (recipe.RequiredStation != currentStation)
            return false;

        if (recipe.RequiredSkill.HasValue &&
            skills.GetEffective(recipe.RequiredSkill.Value) < recipe.SkillThreshold)
            return false;

        foreach (var (itemId, count) in recipe.Ingredients)
        {
            if (!inventory.Contains(itemId, count))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Execute crafting: consume ingredients, create output, publish events.
    /// Returns true if successful, false if validation fails.
    /// </summary>
    public bool Craft(
        InventoryComponent inventory,
        SkillsComponent skills,
        RecipeDefinition recipe,
        StationType currentStation,
        Func<string, int, ItemInstance> createItem)
    {
        if (!CanCraft(inventory, skills, recipe, currentStation))
            return false;

        // Consume ingredients
        foreach (var (itemId, count) in recipe.Ingredients)
            inventory.Remove(itemId, count);

        // Create and add output
        var output = createItem(recipe.OutputItemId, recipe.OutputCount);
        inventory.Add(output);

        _eventBus.Publish(new ItemCraftedEvent(recipe.Id, recipe.OutputItemId, recipe.OutputCount));

        return true;
    }
}
```

### RepairProcessor.cs

Repairs an item's durability. Two modes: self-repair at a workbench (costs materials) and NPC repair (costs currency value). Uses the existing `ItemInstance.CurrentDurability` and `ItemDefinition.Durability`.

```csharp
namespace Oravey2.Core.Crafting;

using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;

public sealed class RepairProcessor
{
    /// <summary>Material cost per 50 durability restored (self-repair).</summary>
    public const int ScrapPerRepairUnit = 3;
    public const int DurabilityPerRepairUnit = 50;

    /// <summary>Currency cost per 25 durability restored (NPC repair).</summary>
    public const int CapsPerNpcUnit = 10;
    public const int DurabilityPerNpcUnit = 25;

    /// <summary>
    /// Calculate how much durability can be restored at a workbench
    /// given available scrap metal in inventory.
    /// </summary>
    public int CalculateSelfRepairAmount(InventoryComponent inventory, ItemInstance item)
    {
        if (item.Definition.Durability == null || item.CurrentDurability == null)
            return 0;

        var missing = item.Definition.Durability.MaxDurability - item.CurrentDurability.Value;
        if (missing <= 0) return 0;

        var scrapCount = CountItem(inventory, "scrap_metal");
        var repairUnits = scrapCount / ScrapPerRepairUnit;
        var maxRestore = repairUnits * DurabilityPerRepairUnit;

        return Math.Min(maxRestore, missing);
    }

    /// <summary>
    /// Repair an item at a workbench. Consumes scrap_metal.
    /// Returns the amount of durability restored.
    /// </summary>
    public int SelfRepair(InventoryComponent inventory, ItemInstance item)
    {
        var amount = CalculateSelfRepairAmount(inventory, item);
        if (amount <= 0) return 0;

        var unitsNeeded = (int)MathF.Ceiling((float)amount / DurabilityPerRepairUnit);
        var scrapNeeded = unitsNeeded * ScrapPerRepairUnit;

        inventory.Remove("scrap_metal", scrapNeeded);
        item.CurrentDurability = Math.Min(
            item.CurrentDurability!.Value + amount,
            item.Definition.Durability!.MaxDurability);

        return amount;
    }

    /// <summary>
    /// Calculate NPC repair cost in caps for full repair.
    /// </summary>
    public int CalculateNpcRepairCost(ItemInstance item)
    {
        if (item.Definition.Durability == null || item.CurrentDurability == null)
            return 0;

        var missing = item.Definition.Durability.MaxDurability - item.CurrentDurability.Value;
        if (missing <= 0) return 0;

        var units = (int)MathF.Ceiling((float)missing / DurabilityPerNpcUnit);
        return units * CapsPerNpcUnit;
    }

    /// <summary>
    /// Repair an item via NPC. Consumes caps from inventory.
    /// Returns true if repair was performed.
    /// </summary>
    public bool NpcRepair(InventoryComponent inventory, ItemInstance item)
    {
        var cost = CalculateNpcRepairCost(item);
        if (cost <= 0) return false;

        if (!inventory.Contains("caps", cost))
            return false;

        inventory.Remove("caps", cost);
        item.CurrentDurability = item.Definition.Durability!.MaxDurability;
        return true;
    }

    private static int CountItem(InventoryComponent inventory, string itemId)
    {
        var total = 0;
        foreach (var item in inventory.Items)
        {
            if (item.Definition.Id == itemId)
                total += item.StackCount;
        }
        return total;
    }
}
```

---

## Durability

### DurabilityHelper.cs

Static helper for durability operations on `ItemInstance`. Works directly with the existing `CurrentDurability` field set by `ItemInstance` constructor from `DurabilityData`.

```csharp
namespace Oravey2.Core.Inventory.Items;

public static class DurabilityHelper
{
    /// <summary>
    /// Degrade an item by its DegradePerUse amount.
    /// Returns the new durability, or null if not a degradable item.
    /// </summary>
    public static int? Degrade(ItemInstance item)
    {
        if (item.Definition.Durability == null || item.CurrentDurability == null)
            return null;

        var amount = item.Definition.Durability.DegradePerUse;
        item.CurrentDurability = Math.Max(0, item.CurrentDurability.Value - (int)MathF.Ceiling(amount));
        return item.CurrentDurability;
    }

    /// <summary>
    /// Degrade by a custom amount (e.g. armor hit with variable cost).
    /// </summary>
    public static int? DegradeBy(ItemInstance item, float amount)
    {
        if (item.Definition.Durability == null || item.CurrentDurability == null)
            return null;

        item.CurrentDurability = Math.Max(0, item.CurrentDurability.Value - (int)MathF.Ceiling(amount));
        return item.CurrentDurability;
    }

    /// <summary>
    /// Repair an item by the specified amount, capped at MaxDurability.
    /// </summary>
    public static int? Repair(ItemInstance item, int amount)
    {
        if (item.Definition.Durability == null || item.CurrentDurability == null)
            return null;

        item.CurrentDurability = Math.Min(
            item.Definition.Durability.MaxDurability,
            item.CurrentDurability.Value + amount);
        return item.CurrentDurability;
    }

    /// <summary>
    /// Returns true if the item has durability tracking and is at 0.
    /// </summary>
    public static bool IsBroken(ItemInstance item)
        => item.CurrentDurability.HasValue && item.CurrentDurability.Value <= 0;

    /// <summary>
    /// Returns a 0.0–1.0 ratio of current/max durability.
    /// Returns null for items without durability.
    /// </summary>
    public static float? GetDurabilityPercent(ItemInstance item)
    {
        if (item.Definition.Durability == null || item.CurrentDurability == null)
            return null;

        return (float)item.CurrentDurability.Value / item.Definition.Durability.MaxDurability;
    }
}
```

---

## Survival

### SurvivalComponent.cs

Tracks the three survival needs. Values range 0–100 where 0 = satisfied, 100 = critical. Optionally disabled.

```csharp
namespace Oravey2.Core.Survival;

public sealed class SurvivalComponent
{
    public bool Enabled { get; set; } = true;

    public float Hunger { get; set; }     // 0–100
    public float Thirst { get; set; }     // 0–100
    public float Fatigue { get; set; }    // 0–100

    /// <summary>Clamp all values to 0–100 range.</summary>
    public void Clamp()
    {
        Hunger = Math.Clamp(Hunger, 0f, 100f);
        Thirst = Math.Clamp(Thirst, 0f, 100f);
        Fatigue = Math.Clamp(Fatigue, 0f, 100f);
    }

    public static SurvivalThreshold GetThreshold(float value) => value switch
    {
        <= 25f => SurvivalThreshold.Satisfied,
        <= 50f => SurvivalThreshold.Normal,
        <= 75f => SurvivalThreshold.Critical,
        _ => SurvivalThreshold.Critical
    };
}
```

Wait — the spec says 0–25=buff, 26–50=normal, 51–75=debuff, 76–100=critical. So 51–75 is "Deprived" and 76–100 is "Critical". Let me fix that:

### SurvivalComponent.cs (corrected)

```csharp
namespace Oravey2.Core.Survival;

public sealed class SurvivalComponent
{
    public bool Enabled { get; set; } = true;

    public float Hunger { get; set; }     // 0–100
    public float Thirst { get; set; }     // 0–100
    public float Fatigue { get; set; }    // 0–100

    /// <summary>Clamp all values to 0–100 range.</summary>
    public void Clamp()
    {
        Hunger = Math.Clamp(Hunger, 0f, 100f);
        Thirst = Math.Clamp(Thirst, 0f, 100f);
        Fatigue = Math.Clamp(Fatigue, 0f, 100f);
    }

    public static SurvivalThreshold GetThreshold(float value) => value switch
    {
        <= 25f => SurvivalThreshold.Satisfied,
        <= 50f => SurvivalThreshold.Normal,
        <= 75f => SurvivalThreshold.Deprived,
        _ => SurvivalThreshold.Critical
    };
}
```

### SurvivalProcessor.cs

Ticks hunger/thirst/fatigue by decay rates, evaluates thresholds, and manages stat modifiers on StatsComponent. Uses constants from GAME_CONSTANTS §6.

Called with `deltaHours` representing elapsed in-game time (caller handles conversion from real seconds to in-game hours via Stride SyncScript integration).

```csharp
namespace Oravey2.Core.Survival;

using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;

public sealed class SurvivalProcessor
{
    // Decay rates per in-game hour (GAME_CONSTANTS §6)
    public const float HungerDecayRate = 2.0f;
    public const float ThirstDecayRate = 3.0f;
    public const float FatigueDecayRate = 1.5f;

    // HP drain per in-game minute at Critical threshold
    public const int StarvingHPDrain = 2;
    public const int DehydratedHPDrain = 3;

    // Source tags for StatModifiers (for add/remove tracking)
    public const string HungerBuffSource = "Survival_HungerBuff";
    public const string HungerDebuffSource = "Survival_HungerDebuff";
    public const string ThirstBuffSource = "Survival_ThirstBuff";
    public const string ThirstDebuffSource = "Survival_ThirstDebuff";
    public const string FatigueBuffSource = "Survival_FatigueBuff";
    public const string FatigueDebuffSource = "Survival_FatigueDebuff";

    private readonly IEventBus _eventBus;

    public SurvivalProcessor(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Tick survival needs. deltaHours is elapsed in-game hours.
    /// Updates hunger/thirst/fatigue, returns threshold changes.
    /// </summary>
    public void Tick(
        SurvivalComponent survival,
        StatsComponent stats,
        HealthComponent health,
        float deltaHours)
    {
        if (!survival.Enabled) return;

        // Increment needs
        survival.Hunger += HungerDecayRate * deltaHours;
        survival.Thirst += ThirstDecayRate * deltaHours;
        survival.Fatigue += FatigueDecayRate * deltaHours;
        survival.Clamp();

        // Apply threshold effects
        ApplyHungerEffects(survival, stats);
        ApplyThirstEffects(survival, stats);
        ApplyFatigueEffects(survival, stats);

        // HP drain at Critical
        var deltaMinutes = deltaHours * 60f;
        if (SurvivalComponent.GetThreshold(survival.Hunger) == SurvivalThreshold.Critical)
            health.TakeDamage((int)(StarvingHPDrain * deltaMinutes));

        if (SurvivalComponent.GetThreshold(survival.Thirst) == SurvivalThreshold.Critical)
            health.TakeDamage((int)(DehydratedHPDrain * deltaMinutes));
    }

    /// <summary>
    /// Apply a food/drink/rest effect to reduce a need value.
    /// </summary>
    public static void RestoreNeed(SurvivalComponent survival, string needType, float amount)
    {
        switch (needType.ToLowerInvariant())
        {
            case "hunger":
                survival.Hunger = Math.Max(0f, survival.Hunger - amount);
                break;
            case "thirst":
                survival.Thirst = Math.Max(0f, survival.Thirst - amount);
                break;
            case "fatigue":
                survival.Fatigue = Math.Max(0f, survival.Fatigue - amount);
                break;
        }
    }

    private static void ApplyHungerEffects(SurvivalComponent survival, StatsComponent stats)
    {
        var threshold = SurvivalComponent.GetThreshold(survival.Hunger);

        // Remove previous modifiers
        stats.RemoveModifier(stats.Modifiers.FirstOrDefault(m => m.Source == HungerBuffSource));
        stats.RemoveModifier(stats.Modifiers.FirstOrDefault(m => m.Source == HungerDebuffSource));

        switch (threshold)
        {
            case SurvivalThreshold.Satisfied:
                stats.AddModifier(new StatModifier(Stat.Strength, 1, HungerBuffSource));
                break;
            case SurvivalThreshold.Deprived:
            case SurvivalThreshold.Critical:
                stats.AddModifier(new StatModifier(Stat.Strength, -1, HungerDebuffSource));
                break;
        }
    }

    private static void ApplyThirstEffects(SurvivalComponent survival, StatsComponent stats)
    {
        var threshold = SurvivalComponent.GetThreshold(survival.Thirst);

        stats.RemoveModifier(stats.Modifiers.FirstOrDefault(m => m.Source == ThirstBuffSource));
        stats.RemoveModifier(stats.Modifiers.FirstOrDefault(m => m.Source == ThirstDebuffSource));

        switch (threshold)
        {
            case SurvivalThreshold.Satisfied:
                stats.AddModifier(new StatModifier(Stat.Perception, 1, ThirstBuffSource));
                break;
            case SurvivalThreshold.Deprived:
            case SurvivalThreshold.Critical:
                stats.AddModifier(new StatModifier(Stat.Perception, -1, ThirstDebuffSource));
                break;
        }
    }

    private static void ApplyFatigueEffects(SurvivalComponent survival, StatsComponent stats)
    {
        var threshold = SurvivalComponent.GetThreshold(survival.Fatigue);

        stats.RemoveModifier(stats.Modifiers.FirstOrDefault(m => m.Source == FatigueBuffSource));
        stats.RemoveModifier(stats.Modifiers.FirstOrDefault(m => m.Source == FatigueDebuffSource));

        switch (threshold)
        {
            case SurvivalThreshold.Satisfied:
                // Rested: +1 AP regen is handled by AP system reading this modifier
                stats.AddModifier(new StatModifier(Stat.Agility, 1, FatigueBuffSource));
                break;
            case SurvivalThreshold.Deprived:
            case SurvivalThreshold.Critical:
                stats.AddModifier(new StatModifier(Stat.Agility, -1, FatigueDebuffSource));
                break;
        }
    }
}
```

---

## Radiation

### RadiationComponent.cs

Tracks radiation exposure 0–1000. Pure data holder.

```csharp
namespace Oravey2.Core.Survival;

public sealed class RadiationComponent
{
    public int Level { get; set; }  // 0–1000

    public void Expose(int amount)
    {
        Level = Math.Clamp(Level + amount, 0, 1000);
    }

    public void Reduce(int amount)
    {
        Level = Math.Clamp(Level - amount, 0, 1000);
    }
}
```

### RadiationProcessor.cs

Evaluates radiation level against GAME_CONSTANTS §6 thresholds. Manages stat modifiers on StatsComponent and HP drain at critical levels.

The zone-based radiation accumulation (position checking) is deferred to Stride integration. This class handles the effects of the current radiation level.

```csharp
namespace Oravey2.Core.Survival;

using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Stats;

public sealed class RadiationProcessor
{
    // Thresholds (GAME_CONSTANTS §6)
    public const int MildThreshold = 200;
    public const int SevereThreshold = 500;
    public const int CriticalThreshold = 800;
    public const int LethalThreshold = 1000;

    // Natural decay
    public const int NaturalDecayPerMinute = 1;

    // Rad-Away
    public const int RadAwayReduction = 100;

    // Source tags
    public const string RadMildSource = "Radiation_Mild";
    public const string RadSevereEndSource = "Radiation_Severe_End";
    public const string RadSevereStrSource = "Radiation_Severe_Str";
    public const string RadCritEndSource = "Radiation_Critical_End";
    public const string RadCritStrSource = "Radiation_Critical_Str";

    // HP drain at critical per in-game minute
    public const int CriticalHPDrain = 2;

    /// <summary>
    /// Evaluate radiation effects. Apply/remove stat modifiers based on current level.
    /// HP drain at Critical. Death at Lethal.
    /// deltaMinutes = elapsed in-game minutes.
    /// </summary>
    public void Evaluate(
        RadiationComponent radiation,
        StatsComponent stats,
        HealthComponent health,
        float deltaMinutes,
        bool inRadZone)
    {
        // Natural decay outside rad zones
        if (!inRadZone && radiation.Level > 0)
        {
            var decay = (int)(NaturalDecayPerMinute * deltaMinutes);
            radiation.Reduce(decay);
        }

        // Remove all rad modifiers, then reapply based on level
        ClearRadModifiers(stats);

        if (radiation.Level >= LethalThreshold)
        {
            // Lethal — instant death
            health.TakeDamage(health.CurrentHP);
            return;
        }

        if (radiation.Level >= CriticalThreshold)
        {
            stats.AddModifier(new StatModifier(Stat.Endurance, -3, RadCritEndSource));
            stats.AddModifier(new StatModifier(Stat.Strength, -2, RadCritStrSource));
            health.TakeDamage((int)(CriticalHPDrain * deltaMinutes));
        }
        else if (radiation.Level >= SevereThreshold)
        {
            stats.AddModifier(new StatModifier(Stat.Endurance, -2, RadSevereEndSource));
            stats.AddModifier(new StatModifier(Stat.Strength, -1, RadSevereStrSource));
        }
        else if (radiation.Level >= MildThreshold)
        {
            stats.AddModifier(new StatModifier(Stat.Endurance, -1, RadMildSource));
        }
    }

    /// <summary>Apply Rad-Away effect: reduce radiation by 100.</summary>
    public static void ApplyRadAway(RadiationComponent radiation)
    {
        radiation.Reduce(RadAwayReduction);
    }

    private static void ClearRadModifiers(StatsComponent stats)
    {
        foreach (var source in new[] { RadMildSource, RadSevereEndSource, RadSevereStrSource,
                                       RadCritEndSource, RadCritStrSource })
        {
            var mod = stats.Modifiers.FirstOrDefault(m => m.Source == source);
            if (mod != null) stats.RemoveModifier(mod);
        }
    }
}
```

---

## Events to Add

Add to `src/Oravey2.Core/Framework/Events/GameEvents.cs`:

```csharp
public readonly record struct ItemCraftedEvent(string RecipeId, string OutputItemId, int Count) : IGameEvent;
public readonly record struct ItemRepairedEvent(string ItemId, int DurabilityRestored) : IGameEvent;
public readonly record struct SurvivalThresholdChangedEvent(
    string NeedType, Survival.SurvivalThreshold OldThreshold,
    Survival.SurvivalThreshold NewThreshold) : IGameEvent;
public readonly record struct RadiationChangedEvent(int OldLevel, int NewLevel) : IGameEvent;
```

**Required import** in `GameEvents.cs`:
```csharp
using Oravey2.Core.Survival;
```

---

## Interaction with Previous Steps

### Step 2: Character & Inventory

```
InventoryComponent.Contains()  ──▶ CraftingProcessor.CanCraft (ingredient check)
InventoryComponent.Remove()    ──▶ CraftingProcessor.Craft (consume ingredients)
InventoryComponent.Add()       ──▶ CraftingProcessor.Craft (add output)
SkillsComponent.GetEffective() ──▶ CraftingProcessor.CanCraft (skill threshold)
ItemInstance.CurrentDurability  ──▶ DurabilityHelper (degrade/repair/broken check)
StatsComponent.AddModifier()   ──▶ SurvivalProcessor (buff/debuff at thresholds)
StatsComponent.RemoveModifier()──▶ SurvivalProcessor + RadiationProcessor
HealthComponent.TakeDamage()   ──▶ SurvivalProcessor (HP drain) + RadiationProcessor
StatModifier record            ──▶ All survival/radiation effects use Source tag for tracking
```

### Step 5: WorldStateService

```
WorldStateService ──▶ Could gate recipe discovery via flags (deferred)
```

### Crafting Flow

```
Player at crafting station (StationType)
  │
  ▼
CraftingProcessor.CanCraft(inventory, skills, recipe, station)
  ├─ station type matches? 
  ├─ skill ≥ threshold?
  └─ all ingredients present?
  │
  ▼ (all pass)
CraftingProcessor.Craft(inventory, skills, recipe, station, createItem)
  ├─ inventory.Remove per ingredient
  ├─ createItem(outputId, outputCount) → ItemInstance
  ├─ inventory.Add(output)
  └─ publish ItemCraftedEvent
```

### Survival Threshold Effects

```
SurvivalProcessor.Tick(survival, stats, health, deltaHours)
  │
  ├─ Hunger thresholds:
  │   ├─ Satisfied (0–25):  +1 Str buff
  │   ├─ Normal (26–50):    no effect
  │   ├─ Deprived (51–75):  −1 Str debuff
  │   └─ Critical (76–100): −1 Str debuff + 2 HP/min drain
  │
  ├─ Thirst thresholds:
  │   ├─ Satisfied (0–25):  +1 Per buff
  │   ├─ Normal (26–50):    no effect
  │   ├─ Deprived (51–75):  −1 Per debuff
  │   └─ Critical (76–100): −1 Per debuff + 3 HP/min drain
  │
  └─ Fatigue thresholds:
      ├─ Satisfied (0–25):  +1 Agi buff (+AP regen)
      ├─ Normal (26–50):    no effect
      ├─ Deprived (51–75):  −1 Agi debuff
      └─ Critical (76–100): −1 Agi debuff (AP halved — in Stride integration)
```

### Radiation Tiers

```
RadiationProcessor.Evaluate(radiation, stats, health, deltaMinutes, inRadZone)
  │
  ├─ 0–199:    No effects. Natural decay if outside rad zone.
  ├─ 200–499:  Mild (−1 End)
  ├─ 500–799:  Severe (−2 End, −1 Str)
  ├─ 800–999:  Critical (−3 End, −2 Str, HP drain)
  └─ 1000:     Lethal (instant death)
```

---

## Tests

### CraftingProcessorTests.cs

| Test | Assertion |
|------|-----------|
| `CanCraft_AllIngredients_SkillMet_True` | Has all ingredients + skill → true |
| `CanCraft_MissingIngredient_False` | Missing one ingredient → false |
| `CanCraft_InsufficientIngredientCount_False` | Has 1, needs 2 → false |
| `CanCraft_SkillBelowThreshold_False` | Skill 10, needs 20 → false |
| `CanCraft_NoSkillRequired_SkipsCheck` | RequiredSkill=null → passes skill check |
| `CanCraft_WrongStation_False` | CookingFire recipe at Workbench → false |
| `Craft_ConsumesIngredients` | After craft, ingredients removed from inventory |
| `Craft_AddsOutput` | After craft, output item in inventory |
| `Craft_PublishesItemCraftedEvent` | EventBus received ItemCraftedEvent |
| `Craft_ReturnsFalse_WhenCannotCraft` | Missing ingredient → Craft returns false, no changes |
| `Craft_MultipleOutput` | OutputCount=10 → output item has count 10 |

### RepairProcessorTests.cs

| Test | Assertion |
|------|-----------|
| `CalculateSelfRepair_HasScrap_ReturnsAmount` | 6 scrap → 100 durability max restore |
| `CalculateSelfRepair_NoScrap_ReturnsZero` | 0 scrap → 0 |
| `CalculateSelfRepair_FullDurability_ReturnsZero` | Item already at max → 0 |
| `CalculateSelfRepair_NoDurabilityData_ReturnsZero` | Non-degradable item → 0 |
| `SelfRepair_ConsumesScrap` | Repairs 50 → consumes 3 scrap_metal |
| `SelfRepair_RestoresDurability` | Durability increases by expected amount |
| `SelfRepair_CappedAtMax` | Can't exceed MaxDurability |
| `CalculateNpcCost_MissingDurability_ReturnsCorrectCaps` | 50 missing → 20 caps |
| `CalculateNpcCost_FullDurability_ReturnsZero` | Full → 0 caps |
| `NpcRepair_ConsumeCaps_RestoreFull` | Removes caps, sets to max durability |
| `NpcRepair_InsufficientCaps_ReturnsFalse` | Not enough caps → false |

### DurabilityHelperTests.cs

| Test | Assertion |
|------|-----------|
| `Degrade_ReducesByDegradePerUse` | DegradePerUse=2.0 → loses 2 durability |
| `Degrade_FloorsAtZero` | Can't go below 0 |
| `Degrade_NoDurabilityData_ReturnsNull` | Non-degradable → null |
| `DegradeBy_CustomAmount` | DegradeBy(5) → loses 5 |
| `Repair_IncreaseDurability` | Repair(50) → gains 50 |
| `Repair_CappedAtMax` | Can't exceed MaxDurability |
| `Repair_NoDurabilityData_ReturnsNull` | Non-degradable → null |
| `IsBroken_AtZero_True` | 0 durability → true |
| `IsBroken_AboveZero_False` | >0 durability → false |
| `IsBroken_NoDurability_False` | No durability tracking → false |
| `GetDurabilityPercent_Half` | 50/100 → 0.5 |
| `GetDurabilityPercent_NoDurability_Null` | Non-degradable → null |

### SurvivalComponentTests.cs

| Test | Assertion |
|------|-----------|
| `Default_EnabledAndZero` | Enabled=true, Hunger/Thirst/Fatigue=0 |
| `Clamp_CapsAt100` | Set to 150 → clamped to 100 |
| `Clamp_FloorsAtZero` | Set to -10 → clamped to 0 |
| `GetThreshold_025_Satisfied` | 25 → Satisfied |
| `GetThreshold_2650_Normal` | 50 → Normal |
| `GetThreshold_5175_Deprived` | 75 → Deprived |
| `GetThreshold_76100_Critical` | 76 → Critical |
| `GetThreshold_Zero_Satisfied` | 0 → Satisfied |
| `GetThreshold_100_Critical` | 100 → Critical |

### SurvivalProcessorTests.cs

| Test | Assertion |
|------|-----------|
| `Tick_IncrementsHunger` | 1 hour → Hunger += 2.0 |
| `Tick_IncrementsThirst` | 1 hour → Thirst += 3.0 |
| `Tick_IncrementsFatigue` | 1 hour → Fatigue += 1.5 |
| `Tick_Disabled_NoChange` | Enabled=false → no changes |
| `Tick_ClampsAt100` | Many hours → values capped at 100 |
| `Tick_SatisfiedHunger_StrBuff` | Hunger=0 → +1 Str modifier added |
| `Tick_DeprivedHunger_StrDebuff` | Hunger=60 → −1 Str modifier added |
| `Tick_CriticalHunger_DrainsHP` | Hunger=80, 1hr → HP reduced |
| `Tick_CriticalThirst_DrainsHP` | Thirst=80, 1hr → HP reduced |
| `Tick_ThresholdChange_RemovesOldModifier` | Go from Satisfied to Normal → buff removed |
| `Tick_DeprivedFatigue_AgiDebuff` | Fatigue=60 → −1 Agi modifier |
| `RestoreNeed_Hunger_ReducesValue` | RestoreNeed("hunger", 25) → Hunger decreases |
| `RestoreNeed_Thirst_ReducesValue` | RestoreNeed("thirst", 30) → Thirst decreases |
| `RestoreNeed_FloorsAtZero` | Restore more than current → 0 |

### RadiationComponentTests.cs

| Test | Assertion |
|------|-----------|
| `Default_Zero` | Level=0 |
| `Expose_IncreasesLevel` | Expose(50) → Level=50 |
| `Expose_CappedAt1000` | Expose(2000) → Level=1000 |
| `Reduce_DecreasesLevel` | Reduce(30) → Level decreases |
| `Reduce_FloorsAtZero` | Reduce(more than current) → Level=0 |

### RadiationProcessorTests.cs

| Test | Assertion |
|------|-----------|
| `Evaluate_BelowMild_NoModifiers` | Level=100 → no stat modifiers |
| `Evaluate_MildThreshold_MinusOneEnd` | Level=200 → −1 End modifier |
| `Evaluate_SevereThreshold_MinusTwoEnd_MinusOneStr` | Level=500 → −2 End, −1 Str |
| `Evaluate_CriticalThreshold_MinusThreeEnd_MinusTwoStr_HPDrain` | Level=800 → −3 End, −2 Str, HP drain |
| `Evaluate_Lethal_Death` | Level=1000 → HP=0 |
| `Evaluate_NaturalDecay_OutsideRadZone` | Level=100, not in zone → level decreases |
| `Evaluate_NoDecay_InRadZone` | In zone → no natural decay |
| `Evaluate_ThresholdDown_RemovesOldModifiers` | Level drops from 500→100 → severe modifiers removed |
| `ApplyRadAway_Reduces100` | Level=300 → ApplyRadAway → Level=200 |
| `ApplyRadAway_FloorsAtZero` | Level=50 → ApplyRadAway → Level=0 |

---

## Execution Order

1. **Enums:** `StationType.cs`, `SurvivalThreshold.cs` — no deps
2. **`RecipeDefinition.cs`** — depends on StationType, SkillType
3. **`DurabilityHelper.cs`** + `DurabilityHelperTests.cs` — depends on ItemInstance (Step 2)
4. **`CraftingProcessor.cs`** + `CraftingProcessorTests.cs` — depends on RecipeDefinition, InventoryComponent, SkillsComponent, IEventBus
5. **`RepairProcessor.cs`** + `RepairProcessorTests.cs` — depends on InventoryComponent, ItemInstance
6. **`SurvivalComponent.cs`** + `SurvivalComponentTests.cs` — depends on SurvivalThreshold only
7. **`SurvivalProcessor.cs`** + `SurvivalProcessorTests.cs` — depends on SurvivalComponent, StatsComponent, HealthComponent, IEventBus
8. **`RadiationComponent.cs`** + `RadiationComponentTests.cs` — no deps
9. **`RadiationProcessor.cs`** + `RadiationProcessorTests.cs` — depends on RadiationComponent, StatsComponent, HealthComponent
10. **Events** in `GameEvents.cs` — add 4 new event records
11. **Run full test suite** — all unit + UI tests pass
