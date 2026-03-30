# Design: Step 03 — Combat

Implements the combat system (AP management, damage resolution, action queue, RTwP loop, combat state) per [docs/steps/03-combat.md](../steps/03-combat.md).

**Depends on:** Step 1 (Framework, Events, GameState), Step 2 (Stats, Skills, Health, Level, Inventory, Equipment)

---

## File Layout

All new files go in `src/Oravey2.Core/`. Tests in `tests/Oravey2.Tests/`.

```
src/Oravey2.Core/
├── Combat/
│   ├── CombatActionType.cs          # enum
│   ├── CoverLevel.cs                # enum
│   ├── HitLocation.cs               # enum
│   ├── CombatAction.cs              # record — queued action
│   ├── AttackContext.cs             # record — flat data for damage resolution
│   ├── DamageResult.cs              # record — outcome of an attack
│   ├── CombatComponent.cs           # AP management (current/max/regen/spend)
│   ├── CombatFormulas.cs            # pure static math — hit chance, damage, crit
│   ├── DamageResolver.cs            # single-attack resolution using CombatFormulas
│   ├── ActionQueue.cs               # ordered FIFO queue of CombatActions
│   ├── CombatStateManager.cs        # enter/exit combat, event publishing
│   └── CombatEngine.cs              # processes actions: AP → resolve → damage → death
├── Framework/
│   └── Events/
│       └── GameEvents.cs            # add new events (existing file)
tests/Oravey2.Tests/
└── Combat/
    ├── CombatFormulasTests.cs
    ├── CombatComponentTests.cs
    ├── DamageResolverTests.cs
    ├── ActionQueueTests.cs
    ├── CombatStateManagerTests.cs
    └── CombatEngineTests.cs
```

### Deferred to Stride Integration

These deliverables require the Stride engine and are deferred until ECS integration:

| Deliverable | Reason |
|-------------|--------|
| `CombatProcessor` (SyncScript) | RTwP tick loop — wraps `CombatEngine`, calls `Regen`/`ProcessAction` per frame |
| `ProjectileScript` (SyncScript) | Entity movement, collision detection |
| Combat HUD | Stride UI — HP bar, AP bar, pause indicator, action queue display |
| `CoverComponent` on world tiles | Requires Stride entity attachment |
| Weapon/Armor durability degradation | Requires `DurabilityData` integration in combat loop (Step 6) |

The pure C# classes designed here are fully testable and will be wrapped by thin Stride scripts later.

---

## Enums

### CombatActionType.cs

```csharp
namespace Oravey2.Core.Combat;

public enum CombatActionType
{
    MeleeAttack,
    RangedAttack,
    Reload,
    UseItem,
    Move,
    TakeCover
}
```

### CoverLevel.cs

```csharp
namespace Oravey2.Core.Combat;

public enum CoverLevel
{
    None,
    Half,
    Full
}
```

### HitLocation.cs

```csharp
namespace Oravey2.Core.Combat;

public enum HitLocation
{
    Torso,
    Head,
    Arms,
    Legs
}
```

---

## Records & Data Models

### CombatAction.cs

Represents a queued action in the RTwP loop. Uses string IDs to identify actors/targets — decoupled from Stride Entity references.

```csharp
namespace Oravey2.Core.Combat;

public sealed record CombatAction(
    string ActorId,
    CombatActionType Type,
    string? TargetId = null);
```

### AttackContext.cs

Flat data bag for `DamageResolver`. Decouples damage resolution from component references — the caller extracts values from `StatsComponent`, `SkillsComponent`, `WeaponData`, `ArmorData`, etc. and populates this record.

```csharp
namespace Oravey2.Core.Combat;

public sealed record AttackContext(
    float WeaponAccuracy,
    int WeaponDamage,
    float WeaponRange,
    float CritMultiplier,
    int SkillLevel,
    int Luck,
    int ArmorDR,
    CoverLevel Cover,
    float Distance);
```

**Mapping from Step 2 components:**

| AttackContext field | Source |
|--------------------|--------|
| `WeaponAccuracy` | `ItemDefinition.Weapon.Accuracy` |
| `WeaponDamage` | `ItemDefinition.Weapon.Damage` |
| `WeaponRange` | `ItemDefinition.Weapon.Range` |
| `CritMultiplier` | `ItemDefinition.Weapon.CritMultiplier` |
| `SkillLevel` | `SkillsComponent.GetEffective(Firearms\|Melee)` |
| `Luck` | `StatsComponent.GetEffective(Stat.Luck)` |
| `ArmorDR` | `ItemDefinition.Armor.DamageReduction` on target |
| `Cover` | Target's cover state |
| `Distance` | Euclidean distance between entities |

### DamageResult.cs

```csharp
namespace Oravey2.Core.Combat;

public sealed record DamageResult(
    bool Hit,
    int Damage,
    HitLocation Location,
    bool Critical);
```

---

## Pure Static Math — CombatFormulas.cs

All formulas from [GAME_CONSTANTS.md §4](../constants/GAME_CONSTANTS.md). No state, no dependencies — identical pattern to `LevelFormulas`.

```csharp
namespace Oravey2.Core.Combat;

public static class CombatFormulas
{
    /// <summary>Max AP at combat start.</summary>
    public const int DefaultMaxAP = 10;

    /// <summary>AP regenerated per second in combat.</summary>
    public const float DefaultAPRegen = 2f;

    // ── Hit Chance ──────────────────────────────────────────────

    /// <summary>
    /// hitChance = weapon.accuracy × (1 + skill/200) × coverMult × rangeMult
    /// Clamped to [0, 0.95].
    /// </summary>
    public static float HitChance(
        float weaponAccuracy, int skillLevel,
        CoverLevel cover, float distance, float weaponRange)
    {
        var skillMod = 1.0f + skillLevel / 200f;
        var coverMod = CoverMultiplier(cover);
        var rangeMod = RangeMultiplier(distance, weaponRange);
        return Math.Clamp(weaponAccuracy * skillMod * coverMod * rangeMod, 0f, 0.95f);
    }

    /// <summary>Cover penalty multiplier.</summary>
    public static float CoverMultiplier(CoverLevel cover) => cover switch
    {
        CoverLevel.Half => 0.70f,
        CoverLevel.Full => 0.40f,
        _ => 1.0f
    };

    /// <summary>
    /// Range penalty: max(0.3, 1.0 − distance / (weapon.range × 1.5)).
    /// </summary>
    public static float RangeMultiplier(float distance, float weaponRange)
        => Math.Max(0.3f, 1.0f - distance / (weaponRange * 1.5f));

    // ── Damage ──────────────────────────────────────────────────

    /// <summary>
    /// damage = weapon.damage × (1 + skill/100) × critMult × locationMult − armor.DR
    /// Minimum 1 on hit.
    /// </summary>
    public static int Damage(
        int weaponDamage, int skillLevel,
        float critMultiplier, bool isCritical,
        int armorDR, float locationMultiplier)
    {
        var skillMod = 1.0f + skillLevel / 100f;
        var crit = isCritical ? critMultiplier : 1.0f;
        var raw = (int)(weaponDamage * skillMod * crit * locationMultiplier);
        return Math.Max(1, raw - armorDR);
    }

    // ── Critical Hit ────────────────────────────────────────────

    /// <summary>Crit chance = Luck × 0.01 (e.g. Luck 5 → 5%).</summary>
    public static float CritChance(int luck) => luck * 0.01f;

    // ── Hit Location ────────────────────────────────────────────

    /// <summary>
    /// Deterministic hit location from a [0, 1) roll.
    /// Torso 40%, Head 10%, Arms 25%, Legs 25%.
    /// </summary>
    public static (HitLocation Location, float DamageMultiplier) RollHitLocation(double roll)
    {
        if (roll < 0.40) return (HitLocation.Torso, 1.0f);
        if (roll < 0.50) return (HitLocation.Head, 1.5f);
        if (roll < 0.75) return (HitLocation.Arms, 0.8f);
        return (HitLocation.Legs, 0.8f);
    }

    // ── AP Costs ────────────────────────────────────────────────

    /// <summary>
    /// Default AP costs for non-weapon actions.
    /// Weapon attacks use WeaponData.ApCost instead.
    /// </summary>
    public static int DefaultAPCost(CombatActionType action) => action switch
    {
        CombatActionType.MeleeAttack => 3,
        CombatActionType.RangedAttack => 2,
        CombatActionType.Reload => 2,
        CombatActionType.UseItem => 2,
        CombatActionType.Move => 1,
        CombatActionType.TakeCover => 1,
        _ => 0
    };
}
```

---

## Components

### CombatComponent.cs

Manages a combatant's Action Points. Attached to any entity that can participate in combat (player, NPCs, enemies).

```csharp
namespace Oravey2.Core.Combat;

public class CombatComponent
{
    public int MaxAP { get; set; } = CombatFormulas.DefaultMaxAP;
    public float CurrentAP { get; private set; }
    public float APRegenPerSecond { get; set; } = CombatFormulas.DefaultAPRegen;
    public bool InCombat { get; set; }

    public CombatComponent()
    {
        CurrentAP = MaxAP;
    }

    /// <summary>Can the entity afford this AP cost?</summary>
    public bool CanAfford(int apCost) => CurrentAP >= apCost;

    /// <summary>Spend AP. Returns false if insufficient.</summary>
    public bool Spend(int apCost)
    {
        if (apCost <= 0 || !CanAfford(apCost)) return false;
        CurrentAP -= apCost;
        return true;
    }

    /// <summary>Regenerate AP over deltaTime seconds. Only while in combat.</summary>
    public void Regen(float deltaTime)
    {
        if (!InCombat || deltaTime <= 0) return;
        CurrentAP = Math.Min(MaxAP, CurrentAP + APRegenPerSecond * deltaTime);
    }

    /// <summary>Reset AP to max (e.g. on combat start).</summary>
    public void ResetAP()
    {
        CurrentAP = MaxAP;
    }
}
```

---

## Logic Classes

### DamageResolver.cs

Resolves a single attack using `CombatFormulas` and injected randomness. Returns a `DamageResult` — the caller decides what to do with it (apply to `HealthComponent`, play SFX, etc.).

```csharp
namespace Oravey2.Core.Combat;

public sealed class DamageResolver
{
    private readonly Random _random;

    public DamageResolver(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public DamageResult Resolve(AttackContext context)
    {
        // 1. Hit roll
        var hitChance = CombatFormulas.HitChance(
            context.WeaponAccuracy, context.SkillLevel,
            context.Cover, context.Distance, context.WeaponRange);

        if (_random.NextDouble() >= hitChance)
            return new DamageResult(Hit: false, Damage: 0, HitLocation.Torso, Critical: false);

        // 2. Hit location
        var (location, locationMult) = CombatFormulas.RollHitLocation(_random.NextDouble());

        // 3. Critical hit
        var critChance = CombatFormulas.CritChance(context.Luck);
        var isCritical = _random.NextDouble() < critChance;

        // 4. Damage
        var damage = CombatFormulas.Damage(
            context.WeaponDamage, context.SkillLevel,
            context.CritMultiplier, isCritical,
            context.ArmorDR, locationMult);

        return new DamageResult(Hit: true, damage, location, isCritical);
    }
}
```

### ActionQueue.cs

FIFO queue for combat actions. During pause, the player queues actions; on unpause, the `CombatProcessor` dequeues and processes them.

```csharp
namespace Oravey2.Core.Combat;

public sealed class ActionQueue
{
    private readonly Queue<CombatAction> _queue = new();

    public int Count => _queue.Count;
    public IReadOnlyList<CombatAction> PendingActions => [.. _queue];

    public void Enqueue(CombatAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _queue.Enqueue(action);
    }

    public CombatAction? Dequeue()
        => _queue.Count > 0 ? _queue.Dequeue() : null;

    public CombatAction? Peek()
        => _queue.Count > 0 ? _queue.Peek() : null;

    public void Clear() => _queue.Clear();
}
```

### CombatStateManager.cs

Manages combat entry/exit. Transitions `GameStateManager` and publishes `CombatStartedEvent` / `CombatEndedEvent`. Tracks active combatants and auto-exits when all enemies are eliminated.

```csharp
namespace Oravey2.Core.Combat;

using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.State;

public sealed class CombatStateManager
{
    private readonly IEventBus _eventBus;
    private readonly GameStateManager _gameState;
    private readonly List<string> _combatants = [];

    public bool InCombat { get; private set; }
    public IReadOnlyList<string> Combatants => _combatants;

    public CombatStateManager(IEventBus eventBus, GameStateManager gameState)
    {
        _eventBus = eventBus;
        _gameState = gameState;
    }

    public bool EnterCombat(string[] enemyIds)
    {
        if (InCombat || enemyIds.Length == 0) return false;

        InCombat = true;
        _combatants.Clear();
        _combatants.AddRange(enemyIds);

        _gameState.TransitionTo(GameState.Exploring);   // ensure valid source state
        _gameState.TransitionTo(GameState.InCombat);
        _eventBus.Publish(new CombatStartedEvent([.. enemyIds]));
        return true;
    }

    public bool ExitCombat()
    {
        if (!InCombat) return false;

        InCombat = false;
        _combatants.Clear();

        _gameState.TransitionTo(GameState.Exploring);
        _eventBus.Publish(new CombatEndedEvent());
        return true;
    }

    /// <summary>
    /// Remove a dead/fled combatant. Auto-exits combat if none remain.
    /// </summary>
    public void RemoveCombatant(string entityId)
    {
        _combatants.Remove(entityId);
        if (_combatants.Count == 0 && InCombat)
            ExitCombat();
    }
}
```

> **Note on `EnterCombat`:** The double transition (`Exploring` → `InCombat`) handles the case where `GameStateManager` is already in `Exploring`. If the current state is already `Exploring`, the first call is a no-op (same-state ignored). The valid transition table in `GameStateManager` allows `Exploring → InCombat`.

### CombatEngine.cs

Orchestrates a single combat action: validates AP, resolves attacks, applies damage, detects death, publishes events, grants skill XP. This is the core logic that the Stride `CombatProcessor` SyncScript will call each frame.

```csharp
namespace Oravey2.Core.Combat;

using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Framework.Events;

public sealed class CombatEngine
{
    private readonly DamageResolver _resolver;
    private readonly IEventBus _eventBus;

    public CombatEngine(DamageResolver resolver, IEventBus eventBus)
    {
        _resolver = resolver;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Process an attack action. Deducts AP, resolves hit/damage, applies to target health.
    /// Returns null if AP insufficient.
    /// </summary>
    public DamageResult? ProcessAttack(
        CombatComponent attackerCombat,
        AttackContext context,
        HealthComponent targetHealth,
        int apCost,
        SkillsComponent? attackerSkills = null,
        SkillType? weaponSkillType = null)
    {
        if (!attackerCombat.Spend(apCost))
            return null;

        var result = _resolver.Resolve(context);

        _eventBus.Publish(new AttackResolvedEvent(
            result.Hit, result.Damage, result.Location, result.Critical));

        if (result.Hit)
        {
            targetHealth.TakeDamage(result.Damage);

            // Use-based skill XP: 1 XP per successful hit
            if (attackerSkills != null && weaponSkillType != null)
                attackerSkills.AddXP(weaponSkillType.Value, 1);

            if (!targetHealth.IsAlive)
                _eventBus.Publish(new EntityDiedEvent());
        }

        return result;
    }

    /// <summary>
    /// Process a non-attack action (Reload, UseItem, Move, TakeCover).
    /// Deducts AP only. Returns false if AP insufficient.
    /// </summary>
    public bool ProcessAction(CombatComponent combat, CombatActionType actionType)
    {
        var cost = CombatFormulas.DefaultAPCost(actionType);
        return combat.Spend(cost);
    }
}
```

---

## Events to Add

Add to `src/Oravey2.Core/Framework/Events/GameEvents.cs`:

```csharp
public readonly record struct CombatStartedEvent(string[] EnemyIds) : IGameEvent;
public readonly record struct CombatEndedEvent() : IGameEvent;
public readonly record struct AttackResolvedEvent(
    bool Hit, int Damage, HitLocation Location, bool Critical) : IGameEvent;
public readonly record struct EntityDiedEvent() : IGameEvent;
```

> **Note:** `EntityDiedEvent` deliberately carries no payload for now. When Stride ECS integration happens, it will gain `Entity Target` and `Entity Killer` fields for quest/faction/loot handlers. Same pattern as Step 2 — value-type-only events, no Entity mocking.

**Required import** in `GameEvents.cs`:
```csharp
using Oravey2.Core.Combat;
```

---

## Interaction with Step 2 Components

### How the combat system uses existing components

```
┌─────────────────┐     ┌──────────────────┐
│  CombatEngine   │────▶│  DamageResolver  │
│  (orchestrator) │     └──────┬───────────┘
│                 │            │ uses
│  Spends AP from │     ┌──────▼───────────┐
│  CombatComponent│     │ CombatFormulas   │
│                 │     │ (pure static)    │
│  Applies damage │     └──────────────────┘
│  to target via  │
│  HealthComponent│     AttackContext built from:
│                 │     ├── StatsComponent.GetEffective(Luck)
│  Grants skill   │     ├── SkillsComponent.GetEffective(Firearms|Melee)
│  XP via         │     ├── EquipmentComponent → ItemDefinition.Weapon
│  SkillsComponent│     ├── Target EquipmentComponent → ItemDefinition.Armor
└─────────────────┘     └── Target CoverLevel + distance
```

### AttackContext construction (caller responsibility)

```csharp
// Example: building AttackContext from Step 2 components
var weapon = equippedWeapon.Definition.Weapon!;
var skill = weapon.SkillType == "firearms" ? SkillType.Firearms : SkillType.Melee;

var context = new AttackContext(
    WeaponAccuracy: weapon.Accuracy,
    WeaponDamage: weapon.Damage,
    WeaponRange: weapon.Range,
    CritMultiplier: weapon.CritMultiplier,
    SkillLevel: attackerSkills.GetEffective(skill),
    Luck: attackerStats.GetEffective(Stat.Luck),
    ArmorDR: targetArmor?.DamageReduction ?? 0,
    Cover: targetCover,
    Distance: Vector3.Distance(attackerPos, targetPos));
```

---

## Tests

### CombatFormulasTests.cs — pure math, no dependencies

| Test | Assertion |
|------|-----------|
| `CoverMultiplier_None_Returns1` | `CoverMultiplier(None) == 1.0f` |
| `CoverMultiplier_Half_Returns070` | `CoverMultiplier(Half) == 0.70f` |
| `CoverMultiplier_Full_Returns040` | `CoverMultiplier(Full) == 0.40f` |
| `RangeMultiplier_AtZero_Returns1` | `RangeMultiplier(0, 15) == 1.0f` |
| `RangeMultiplier_AtMaxRange_Positive` | `RangeMultiplier(15, 15) > 0.3f` |
| `RangeMultiplier_BeyondRange_ClampedAt03` | `RangeMultiplier(100, 10) == 0.3f` |
| `HitChance_HighAccuracy_NoCover_CloseRange` | ~0.65 × 1.1 × 1.0 × 1.0 ≈ 0.715 |
| `HitChance_Capped_At_095` | Extreme values → 0.95 |
| `HitChance_FullCover_Reduces` | Same setup but full cover → significantly lower |
| `Damage_BasicHit_NoArmor` | `Damage(12, 20, 2.0, false, 0, 1.0) == 14` |
| `Damage_WithArmor_Reduced` | `Damage(12, 20, 2.0, false, 5, 1.0) == 9` |
| `Damage_CriticalHit_Doubled` | `Damage(12, 20, 2.0, true, 0, 1.0) == 28` |
| `Damage_HeadshotMultiplier` | `Damage(12, 20, 2.0, false, 0, 1.5) == 21` |
| `Damage_MinimumOne` | `Damage(1, 0, 1.0, false, 99, 0.8) == 1` |
| `CritChance_Luck5_Returns005` | `CritChance(5) == 0.05f` |
| `CritChance_Luck0_ReturnsZero` | `CritChance(0) == 0f` |
| `RollHitLocation_Torso` | roll=0.0 → (Torso, 1.0) |
| `RollHitLocation_Head` | roll=0.45 → (Head, 1.5) |
| `RollHitLocation_Arms` | roll=0.60 → (Arms, 0.8) |
| `RollHitLocation_Legs` | roll=0.80 → (Legs, 0.8) |
| `RollHitLocation_Boundary_Torso` | roll=0.39 → Torso |
| `RollHitLocation_Boundary_Head` | roll=0.40 → Head |
| `DefaultAPCost_MeleeAttack_3` | `DefaultAPCost(MeleeAttack) == 3` |
| `DefaultAPCost_RangedAttack_2` | `DefaultAPCost(RangedAttack) == 2` |
| `DefaultAPCost_Move_1` | `DefaultAPCost(Move) == 1` |
| `DefaultAPCost_TakeCover_1` | `DefaultAPCost(TakeCover) == 1` |

### CombatComponentTests.cs

| Test | Assertion |
|------|-----------|
| `Defaults_MaxAP10_RegenRate2` | MaxAP=10, APRegen=2, CurrentAP=10 |
| `CanAfford_Sufficient_ReturnsTrue` | CanAfford(3) with 10 AP → true |
| `CanAfford_Insufficient_ReturnsFalse` | CanAfford(11) with 10 AP → false |
| `Spend_DeductsAP` | Spend(3) → CurrentAP = 7 |
| `Spend_Insufficient_ReturnsFalse` | Spend(11) → false, AP unchanged |
| `Spend_ZeroOrNegative_ReturnsFalse` | Spend(0) → false |
| `Regen_IncreasesAP` | Regen(0.5) with 2/s → +1 AP |
| `Regen_CapsAtMax` | Regen(100) → CurrentAP = MaxAP |
| `Regen_NotInCombat_NoEffect` | InCombat=false → no regen |
| `Regen_NegativeDelta_NoEffect` | Regen(-1) → no change |
| `ResetAP_RestoresToMax` | Spend then Reset → MaxAP |
| `CustomMaxAP_Respected` | MaxAP=15 → CurrentAP starts at 15 |

### DamageResolverTests.cs — seeded Random for determinism

| Test | Assertion |
|------|-----------|
| `Resolve_GuaranteedHit_ReturnsDamage` | Seed that rolls < hitChance → Hit=true, Damage > 0 |
| `Resolve_GuaranteedMiss_ReturnsZero` | Low accuracy (0.01) → very likely miss, Damage=0 |
| `Resolve_Headshot_HigherDamage` | Seed roll into head range (0.40–0.50) → 1.5× multiplier |
| `Resolve_CriticalHit_AppliesMultiplier` | Luck=99 (99% crit) → Critical=true, higher damage |
| `Resolve_NoCrit_LuckZero` | Luck=0 → Critical=false |
| `Resolve_ArmorReducesDamage` | ArmorDR=10 → lower damage than DR=0 |
| `Resolve_MinimumDamageOne` | High armor + low weapon → Damage ≥ 1 on hit |
| `Resolve_FullCover_LowersHitChance` | Same context but Full cover → more misses |

### ActionQueueTests.cs

| Test | Assertion |
|------|-----------|
| `Empty_CountZero` | Count = 0 |
| `Enqueue_IncreasesCount` | Enqueue → Count = 1 |
| `Dequeue_ReturnsFirst` | Enqueue A, B → Dequeue gets A |
| `Dequeue_Empty_ReturnsNull` | Empty queue → null |
| `Peek_DoesNotRemove` | Peek → Count unchanged |
| `Peek_Empty_ReturnsNull` | Empty → null |
| `Clear_RemovesAll` | Enqueue 3, Clear → Count = 0 |
| `PendingActions_ReflectsQueue` | Enqueue A, B → PendingActions has 2 items in order |
| `FIFO_Order` | Enqueue A, B, C → Dequeue returns A, B, C |
| `Enqueue_Null_Throws` | Enqueue(null) → ArgumentNullException |

### CombatStateManagerTests.cs

| Test | Assertion |
|------|-----------|
| `InitialState_NotInCombat` | InCombat = false |
| `EnterCombat_SetsInCombat` | EnterCombat → InCombat = true |
| `EnterCombat_PublishesEvent` | CombatStartedEvent published with enemy IDs |
| `EnterCombat_TransitionsGameState` | GameStateManager.CurrentState = InCombat |
| `EnterCombat_TracksCombatants` | Combatants contains enemy IDs |
| `EnterCombat_Empty_ReturnsFalse` | No enemies → false, no transition |
| `EnterCombat_AlreadyInCombat_ReturnsFalse` | Double call → second returns false |
| `ExitCombat_ClearsState` | ExitCombat → InCombat = false |
| `ExitCombat_PublishesEvent` | CombatEndedEvent published |
| `ExitCombat_TransitionsToExploring` | GameStateManager.CurrentState = Exploring |
| `ExitCombat_NotInCombat_ReturnsFalse` | Not in combat → false |
| `RemoveCombatant_ReducesList` | Remove 1 of 2 → 1 remaining |
| `RemoveCombatant_LastEnemy_AutoExits` | Remove last → ExitCombat triggered |
| `RemoveCombatant_Unknown_NoEffect` | Remove non-existent ID → no crash |

### CombatEngineTests.cs

| Test | Assertion |
|------|-----------|
| `ProcessAttack_Hit_DealsDamage` | TargetHealth reduced by result.Damage |
| `ProcessAttack_Miss_NoDamage` | TargetHealth unchanged |
| `ProcessAttack_DeductsAP` | AttackerCombat.CurrentAP decreases by apCost |
| `ProcessAttack_InsufficientAP_ReturnsNull` | AP=1, cost=3 → null, no events |
| `ProcessAttack_PublishesAttackResolvedEvent` | Event with correct Hit/Damage/Location/Critical |
| `ProcessAttack_Kill_PublishesEntityDiedEvent` | Target HP → 0 → EntityDiedEvent published |
| `ProcessAttack_Hit_GrantsSkillXP` | SkillsComponent.AddXP called with 1 |
| `ProcessAttack_Miss_NoSkillXP` | Miss → no AddXP call |
| `ProcessAttack_NoSkillsComponent_StillWorks` | null skills → no crash |
| `ProcessAction_Move_Costs1AP` | AP decreases by 1 |
| `ProcessAction_Reload_Costs2AP` | AP decreases by 2 |
| `ProcessAction_InsufficientAP_ReturnsFalse` | AP=0 → false |

---

## Execution Order

1. **Enums:** `CombatActionType.cs`, `CoverLevel.cs`, `HitLocation.cs` — no deps
2. **Records:** `CombatAction.cs`, `AttackContext.cs`, `DamageResult.cs` — no deps
3. **`CombatFormulas.cs`** + `CombatFormulasTests.cs` — pure static math, depends only on enums
4. **`CombatComponent.cs`** + `CombatComponentTests.cs` — depends on CombatFormulas (constants only)
5. **Events** in `GameEvents.cs` — add 4 new event records
6. **`DamageResolver.cs`** + `DamageResolverTests.cs` — depends on CombatFormulas + records
7. **`ActionQueue.cs`** + `ActionQueueTests.cs` — depends on CombatAction record only
8. **`CombatStateManager.cs`** + `CombatStateManagerTests.cs` — depends on IEventBus + GameStateManager + events
9. **`CombatEngine.cs`** + `CombatEngineTests.cs` — depends on all above + HealthComponent + SkillsComponent
10. **Run full test suite** — all unit + UI tests pass
