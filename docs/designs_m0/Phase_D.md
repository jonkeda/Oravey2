# Design: Phase D — Polish, Balance & Cleanup

Fixes combat balance issues exposed by Phases A-C: weapon stats are hardcoded instead of reading from equipment, melee distance penalty makes combat unwinnable, and enemy tuning doesn't produce the target 3-5 round fight. After this phase, combat reads real weapon/stat data, enemies are appropriately challenging, and the codebase is clean for M0 delivery.

**Depends on:** Phase A (combat wiring), Phase B (inventory/equipment), Phase C (HUD/game-over)

---

## Problem

### 1. Hardcoded weapon constants ignore equipped items

`CombatSyncScript` uses hardcoded constants for **all** attacks — both player and enemy:

```csharp
private const float WeaponAccuracy = 0.75f;
private const int WeaponDamage = 12;
private const float WeaponRange = 2f;
private const float CritMultiplier = 1.5f;
private const int MeleeAPCost = 3;
```

The player's equipped Pipe Wrench (`M0Items.PipeWrench()`) defines different stats: Damage=14, Accuracy=0.80. These are never read. Enemies use the same constants as the player despite being intended as weaker opponents.

### 2. Melee distance penalty makes combat unwinnable

The `RangeMultiplier` formula penalises attacks based on world distance:

```
RangeMultiplier(distance, weaponRange) = max(0.3, 1.0 - distance / (weaponRange × 1.5))
```

When combat triggers at the 5-unit encounter radius, both combatants are 5+ world units apart. With a 2-unit weapon range, `RangeMultiplier(5, 2) = max(0.3, 1.0 - 5/3) = 0.3`. This drops effective hit chance to ~22%, making each attack deal ~2.6 expected damage. At this rate:

- Player DPS to one enemy: ~1.7
- 3 enemies DPS to player: ~5.1
- **Player dies in ~20s without killing a single enemy**

In a real melee fight, combatants close to striking range. The world distance should not apply to melee attacks.

### 3. Enemy stats produce unbalanced encounters

With Endurance=4, enemies have 95 HP — nearly as tough as the player (105 HP). Combined with identical weapon stats and 3v1 odds, the player cannot win even with the distance fix unless enemy damage output is reduced.

### 4. Character stats not wired into combat

`AttackContext` is built with `Luck: 5` and `ArmorDR: 0` hardcoded. The attacker's actual Luck stat from `StatsComponent` is ignored. Armor DR from equipped items is ignored (though no armor is equipped at game start, the wire should exist for loot pickups).

---

## Scope

| Sub-task | Summary |
|----------|---------|
| D1 | Wire equipped weapon stats — CombatSyncScript reads player weapon from EquipmentComponent, enemies from EnemyInfo |
| D2 | Wire character stats into combat — Luck from StatsComponent, armor DR from EquipmentComponent |
| D3 | Fix melee distance — melee attacks use effective distance 0 (combatants at striking range) |
| D4 | Rebalance enemy configuration — weaker stats, new enemy weapon, target ~30s combat with player barely winning |
| D5 | Camera tuning — increase FollowSmoothing for snappier tracking, slight FOV adjustment |
| D6 | Cleanup & verification — remove dead code, verify clean build and all tests pass |

### What's Deferred

| Item | Deferred To |
|------|-------------|
| Medkit usage in combat (UseItem action) | Post-M0 |
| Ranged weapons / weapon switching | Post-M0 |
| Enemy AI movement / pathfinding | Post-M0 |
| Dynamic encounter generation (multiple separate encounters) | Post-M0 |
| Difficulty settings | Post-M0 |

---

## D1 — Wire Equipped Weapon Stats

### Problem

`CombatSyncScript.ProcessNextAction()` builds every `AttackContext` with the same 5 hardcoded weapon constants. The player's Pipe Wrench (14 dmg, 0.80 acc) is equipped but never read. Enemies have no weapon data at all.

### Changes

**EnemyInfo** — Add weapon and stats fields:

```csharp
internal sealed class EnemyInfo
{
    public required Entity Entity { get; init; }
    public required string Id { get; init; }
    public required HealthComponent Health { get; init; }
    public required CombatComponent Combat { get; init; }
    public required StatsComponent Stats { get; init; }         // NEW
    public WeaponData? Weapon { get; init; }                    // NEW
}
```

**CombatSyncScript** — Replace hardcoded constants:

```csharp
// Remove these:
// private const float WeaponAccuracy = 0.75f;
// private const int WeaponDamage = 12;
// private const float WeaponRange = 2f;
// private const float CritMultiplier = 1.5f;
// private const int MeleeAPCost = 3;

// Add:
public EquipmentComponent? PlayerEquipment { get; set; }
public StatsComponent? PlayerStats { get; set; }

// Unarmed fallback when no weapon equipped
private static readonly WeaponData UnarmedWeapon = new(
    Damage: 5, Range: 1.5f, ApCost: 3,
    Accuracy: 0.50f, SkillType: "melee", CritMultiplier: 1.5f);
```

**Reading weapon stats** — In `ProcessNextAction()`, resolve weapon per-attacker:

```csharp
WeaponData weapon;
if (action.ActorId == PlayerId)
{
    var equipped = PlayerEquipment?.GetEquipped(EquipmentSlot.PrimaryWeapon);
    weapon = equipped?.Definition.Weapon ?? UnarmedWeapon;
}
else
{
    var attacker = Enemies.FirstOrDefault(e => e.Id == action.ActorId);
    weapon = attacker?.Weapon ?? UnarmedWeapon;
}
```

Then build `AttackContext` from `weapon`:

```csharp
var context = new AttackContext(
    WeaponAccuracy: weapon.Accuracy,
    WeaponDamage: weapon.Damage,
    WeaponRange: weapon.Range,
    CritMultiplier: weapon.CritMultiplier,
    SkillLevel: 0,           // D2 will read from stats
    Luck: attackerLuck,       // D2
    ArmorDR: targetArmorDR,   // D2
    Cover: CoverLevel.None,
    Distance: effectiveDistance);
```

**HandlePlayerInput** — Read AP cost from weapon:

```csharp
var equipped = PlayerEquipment?.GetEquipped(EquipmentSlot.PrimaryWeapon);
var weapon = equipped?.Definition.Weapon ?? UnarmedWeapon;
if (!PlayerCombat.CanAfford(weapon.ApCost)) return;
```

**RunEnemyAI** — Read AP cost from enemy weapon:

```csharp
var weapon = enemy.Weapon ?? UnarmedWeapon;
if (!enemy.Combat.CanAfford(weapon.ApCost)) continue;
```

**Program.cs** — Wire equipment:

```csharp
combatScript.PlayerEquipment = playerEquipment;
combatScript.PlayerStats = playerStats;
```

---

## D2 — Wire Character Stats into Combat

### Problem

`AttackContext` hardcodes `Luck: 5` and `ArmorDR: 0`. The attacker's actual Luck affects crit chance (`Luck × 0.01`), and armor DR reduces damage. Both should come from character data.

### Changes

**Read Luck** — In `ProcessNextAction`, resolve from the attacker's StatsComponent:

```csharp
int attackerLuck;
if (action.ActorId == PlayerId)
    attackerLuck = PlayerStats?.GetEffective(Stat.Luck) ?? 5;
else
{
    var attacker = Enemies.FirstOrDefault(e => e.Id == action.ActorId);
    attackerLuck = attacker?.Stats.GetEffective(Stat.Luck) ?? 5;
}
```

**Read Armor DR** — From the target's equipment. For enemies (no equipment system), DR is 0. For player:

```csharp
int targetArmorDR = 0;
if (action.TargetId == PlayerId && PlayerEquipment != null)
{
    var torsoArmor = PlayerEquipment.GetEquipped(EquipmentSlot.Torso);
    targetArmorDR = torsoArmor?.Definition.Armor?.DamageReduction ?? 0;
}
```

### Impact

- Player Luck=5 → 5% crit chance (unchanged from current hardcode, but now dynamic)
- Enemy Luck=3 → 3% crit chance (down from hardcoded 5%)
- If player picks up and equips LeatherJacket (DR 3), incoming damage is reduced by 3 per hit

---

## D3 — Fix Melee Combat Distance

### Problem

`ProcessNextAction` passes world distance between Entity positions to `AttackContext.Distance`. For melee attacks at encounter trigger range (5+ units), `RangeMultiplier` drops to 0.3, reducing hit chance to ~22%. Melee combatants should close to striking range.

### Change

For melee attacks, set effective distance to 0 (optimal range):

```csharp
// In ProcessNextAction, after calculating world distance:
var effectiveDistance = action.Type == CombatActionType.MeleeAttack
    ? 0f
    : distance;
```

### Impact

At effective distance 0, `RangeMultiplier(0, any) = 1.0`. Hit chances become:
- Player with PipeWrench (0.80 acc): **80% hit** (was 22%)
- Enemy with RustyShiv (0.55 acc): **55% hit** (was 16%)

This is the single biggest balance fix. Combat becomes viable.

---

## D4 — Rebalance Enemy Configuration

### Target Balance

**Goal:** 3-enemy combat lasting ~28 seconds. Player wins with ~10% HP remaining. Tense but winnable.

### Current vs Proposed

| Stat | Current | Proposed | Rationale |
|------|---------|----------|-----------|
| Enemy Endurance | 4 | 1 | HP: 95→65. Enemies should fall faster in 3v1 |
| Enemy Strength | 5 | 3 | Lower base stat for weaker melee |
| Enemy Perception | 4 | 3 | Lower awareness |
| Enemy Luck | 4 | 3 | Crit chance: 4%→3% |
| Enemy weapon | PipeWrench (14 dmg) | RustyShiv (5 dmg) | Enemies deal much less damage per hit |
| Enemy accuracy | 0.80 | 0.55 | Lower hit rate |

### New Item: RustyShiv

Add to `M0Items`:

```csharp
public static ItemDefinition RustyShiv() => new(
    Id: "rusty_shiv",
    Name: "Rusty Shiv",
    Description: "A crude blade. Barely functional.",
    Category: ItemCategory.WeaponMelee,
    Weight: 0.5f,
    Stackable: false,
    Value: 3,
    Slot: EquipmentSlot.PrimaryWeapon,
    Weapon: new WeaponData(
        Damage: 5,
        Range: 1.5f,
        ApCost: 3,
        Accuracy: 0.55f,
        SkillType: "melee",
        CritMultiplier: 1.5f));
```

### Program.cs — Adjusted enemy creation

```csharp
var enemyStats = new StatsComponent(new Dictionary<Stat, int>
{
    { Stat.Strength, 3 }, { Stat.Perception, 3 }, { Stat.Endurance, 1 },
    { Stat.Charisma, 2 }, { Stat.Intelligence, 2 }, { Stat.Agility, 4 },
    { Stat.Luck, 3 },
});
// ...
enemies.Add(new EnemyInfo
{
    Entity = enemyEntity,
    Id = id,
    Health = enemyHealth,
    Combat = enemyCombat,
    Stats = enemyStats,
    Weapon = M0Items.RustyShiv().Weapon,
});
```

### Expected Balance (post D1-D4)

**Player:** 105 HP, PipeWrench (14 dmg, 0.80 acc), Luck 5
**Enemy (×3):** 65 HP each, RustyShiv (5 dmg, 0.55 acc), Luck 3
**Melee distance:** 0 (no range penalty)

| Metric | Player → Enemy | Enemy → Player |
|--------|---------------|----------------|
| Hit chance | 80% | 55% |
| Avg damage/hit | 13.2 | 4.7 |
| Attacks/sec | 0.67 | 0.67 each |
| DPS | 7.04 | 1.72 each (5.17 total) |

**Kill timeline:**

| Event | Time | Player HP |
|-------|------|-----------|
| Enemy 1 dies (65 HP / 7.04 DPS) | 9.2s | 57 |
| Enemy 2 dies | 18.5s | 24 |
| Enemy 3 dies | 27.7s | 10 |

Combat lasts ~28 seconds. Player wins with ~10% HP. RNG variance means some fights are tighter — appropriate for post-apocalyptic survival.

### Enemy positions (unchanged)

Keeping existing positions to avoid breaking 101 existing UI tests:

- `enemy_1` at `(8, 0.5, 8)` — NE quadrant
- `enemy_2` at `(-6, 0.5, 10)` — NW quadrant
- `enemy_3` at `(10, 0.5, -6)` — SE quadrant

---

## D5 — Camera Tuning

### Current values

| Property | Value | Observation |
|----------|-------|-------------|
| Distance | 50 | Far. Camera feels detached |
| CurrentFov | 25° | Narrow FOV compensates for distance |
| FollowSmoothing | 5 | Noticeable lag when player moves |
| Deadzone | 0.5 | Fine |

### Proposed values

| Property | Old | New | Rationale |
|----------|-----|-----|-----------|
| Distance | 50 | 40 | 20% closer — more intimate feel |
| CurrentFov | 25° | 28° | Slightly wider to compensate for closer distance |
| FollowSmoothing | 5 | 8 | Snappier camera tracking, less float |

### Test impact

- `ZoomState_IsQueryable` asserts `Assert.Equal(25.0, cam.Zoom, 5.0)` — tolerance of 5.0, so 28° still passes
- `InitialZoom_WorldEdgesVisible` checks at least 2 corners visible — should still pass at 28° FOV / distance 40
- `CameraFollowTests` uses relative assertions (player on screen, delta movement) — no breakage expected

---

## D6 — Cleanup & Verification

1. Remove the 5 hardcoded weapon constants from `CombatSyncScript`
2. Add the `StatsComponent` `using` to `EnemyInfo` if not already imported
3. Verify `dotnet build Oravey2.sln` — 0 errors, only NU1903 warnings
4. Verify `dotnet test tests/Oravey2.Tests/` — all 635 unit tests pass
5. Run all 101 UI tests — all pass (update any that break from camera/stat changes)
6. Verify game launches cleanly: `dotnet run --project src/Oravey2.Windows`

---

## File Layout

```
src/Oravey2.Core/
├── Combat/
│   └── CombatSyncScript.cs              # MODIFY — D1/D2/D3: replace hardcoded constants, add weapon/stats/armor reading
├── Inventory/Items/
│   └── M0Items.cs                       # MODIFY — D4: add RustyShiv()
src/Oravey2.Windows/
└── Program.cs                           # MODIFY — D1/D4/D5: wire equipment + stats, adjust enemy config, camera tuning
```

No new files in this phase. All changes are to existing code.

---

## Acceptance Criteria

| # | Criterion |
|---|-----------|
| 1 | CombatSyncScript reads player weapon stats from EquipmentComponent (no hardcoded weapon constants) |
| 2 | Enemies have their own weapon data via EnemyInfo.Weapon |
| 3 | Attacker's Luck is read from StatsComponent |
| 4 | Target's armor DR is read from EquipmentComponent |
| 5 | Melee attacks use effective distance 0 (no range penalty) |
| 6 | Enemy stats: End=1 (65 HP), weapon=RustyShiv (5 dmg, 0.55 acc) |
| 7 | Camera: Distance=40, FOV=28°, FollowSmoothing=8 |
| 8 | Combat is winnable — player survives 3v1 encounter in majority of runs |
| 9 | All 635 unit tests still pass |
| 10 | All 101 UI tests still pass (updated if camera changes break any) |
| 11 | `dotnet build` produces 0 errors |
