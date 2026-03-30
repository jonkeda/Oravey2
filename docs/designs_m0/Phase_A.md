# Design: Phase A — Enemy & Combat Integration

Wires the existing pure-C# combat logic (Step 03) into the live Stride game loop. After this phase, the player can walk near enemies, enter combat, attack them, and see them die.

**Depends on:** All Steps 1-10 (pure C# logic), Program.cs bootstrap, PlayerMovementScript, IsometricCameraScript

---

## Scope

| Sub-task | Summary |
|----------|---------|
| A1 | Spawn 2-3 enemy entities with red capsules + combat components on walkable tiles |
| A2 | Proximity trigger — distance-check SyncScript, GameState transitions |
| A3 | CombatSyncScript — wire CombatEngine into per-frame loop, process ActionQueue |
| A4 | Player attack input — Space to queue melee attack on nearest enemy |
| A5 | Hit feedback — flash red on hit, remove entity on death, return to Exploring |

### What's Deferred

| Item | Deferred To |
|------|-------------|
| Enemy AI pathfinding / movement | Phase D tuning (M0 enemies stand still) |
| Ranged attacks / weapon switching | Post-M0 |
| Cover system integration | Post-M0 |
| Combat HUD (HP bars, state text) | Phase C |
| Unit tests for SyncScripts | Phase D (Brinell UI tests) |
| Sound effects on hit / death | Post-M0 |

---

## File Layout

```
src/Oravey2.Core/
├── Combat/
│   ├── CombatSyncScript.cs          # NEW — per-frame combat loop SyncScript
│   └── EncounterTriggerScript.cs    # NEW — proximity detection + state transitions
src/Oravey2.Windows/
└── Program.cs                       # MODIFY — spawn enemies, register combat scripts
```

No new test files in this phase. The SyncScripts depend on the Stride engine runtime and will be covered by Brinell UI tests in Phase D.

---

## Existing APIs We'll Use

### CombatEngine.ProcessAttack

```csharp
public DamageResult? ProcessAttack(
    CombatComponent attackerCombat,    // spends AP
    AttackContext context,             // weapon stats, distance, etc.
    HealthComponent targetHealth,      // receives damage
    int apCost,                        // AP to spend
    SkillsComponent? attackerSkills,   // optional, gains XP on hit
    SkillType? weaponSkillType)        // optional, skill to level
```

Returns `null` if not enough AP. Otherwise resolves hit/miss/crit via `DamageResolver`, applies damage to `targetHealth`, publishes `AttackResolvedEvent` and `EntityDiedEvent`.

### CombatStateManager

```csharp
EnterCombat(string[] enemyIds)   // sets InCombat, transitions GameState, publishes CombatStartedEvent
ExitCombat()                     // sets Exploring, publishes CombatEndedEvent
RemoveCombatant(string entityId) // removes from list, auto-exits if last enemy dies
```

### ActionQueue

```csharp
Enqueue(CombatAction action)   // add action to FIFO queue
Dequeue()                      // pop next action (null if empty)
Clear()                        // flush all pending
```

### CombatAction

```csharp
record CombatAction(string ActorId, CombatActionType Type, string? TargetId = null)
```

### CombatComponent

```csharp
bool CanAfford(int apCost)
bool Spend(int apCost)
void Regen(float deltaTime)     // only regens when InCombat == true
void ResetAP()
```

### HealthComponent Constructor

```csharp
HealthComponent(StatsComponent stats, LevelComponent level, IEventBus? eventBus = null)
// MaxHP = 50 + Endurance*10 + Level*5
// Default stats (all 5): MaxHP = 50 + 50 + 5 = 105
```

### GameAction Enum (Input)

`GameAction.Attack` is already mapped to `Keys.Space` in `KeyboardMouseInputProvider`.

### IInputProvider

`IsActionPressed(GameAction.Attack)` — returns `true` on the frame Space is first pressed.

---

## A1 — Spawn Enemy Entities

### Design

Each enemy is a Stride `Entity` with:
- A red capsule mesh (same geometry as the player, different colour)
- An `EncounterTriggerScript` (shared across all enemies — lives on a manager entity, not per-enemy)
- Pure C# component instances stored in a lookup dictionary

Enemy data is held in a simple class (not a SyncScript) since the combat components are plain C# objects:

```csharp
internal sealed class EnemyInfo
{
    public required Entity Entity { get; init; }
    public required string Id { get; init; }
    public required HealthComponent Health { get; init; }
    public required CombatComponent Combat { get; init; }
}
```

This is defined inside `CombatSyncScript.cs` as a nested type (no separate file for M0).

### Enemy Stats (M0 Defaults)

| Property | Value | Rationale |
|----------|-------|-----------|
| Endurance | 4 | Slightly weaker than player default (5) |
| Level | 1 | Same as player |
| MaxHP | 95 | `50 + 4*10 + 1*5 = 95` |
| MaxAP | 10 | Default `CombatFormulas.DefaultMaxAP` |
| AP Regen | 2/s | Default `CombatFormulas.DefaultAPRegen` |

### Enemy Placement

Place on hardcoded walkable tile positions (inside the 16×16 map, avoiding border walls):

| Enemy | Id | Position (tile) | World Position |
|-------|-----|-----------------|----------------|
| Enemy 1 | `"enemy_1"` | (10, 10) | `(10, 0.5, 10)` |
| Enemy 2 | `"enemy_2"` | (5, 12) | `(5, 0.5, 12)` |
| Enemy 3 | `"enemy_3"` | (12, 5) | `(12, 0.5, 5)` |

Y=0.5 matches the player entity height.

### Program.cs Changes (A1)

After the player entity section, add enemy spawning:

```csharp
// --- Enemy entities ---
var enemyPositions = new (string id, Vector3 pos)[]
{
    ("enemy_1", new Vector3(10, 0.5f, 10)),
    ("enemy_2", new Vector3(5, 0.5f, 12)),
    ("enemy_3", new Vector3(12, 0.5f, 5)),
};

var enemyMaterial = game.CreateMaterial(new Color(0.8f, 0.15f, 0.15f));
var enemies = new List<EnemyInfo>();

foreach (var (id, pos) in enemyPositions)
{
    var enemyEntity = new Entity(id);
    enemyEntity.Transform.Position = pos;

    // Visual: red capsule
    var enemyVisual = new Entity($"{id}_Visual");
    var enemyMesh = GeometricPrimitive.Capsule.New(game.GraphicsDevice, 0.3f, 0.8f).ToMeshDraw();
    var enemyModel = new Model();
    enemyModel.Meshes.Add(new Mesh { Draw = enemyMesh });
    enemyModel.Materials.Add(enemyMaterial);
    enemyVisual.Add(new ModelComponent(enemyModel));
    enemyEntity.AddChild(enemyVisual);

    rootScene.Entities.Add(enemyEntity);

    // Pure C# combat components
    var enemyStats = new StatsComponent(new Dictionary<Stat, int>
    {
        { Stat.Strength, 5 }, { Stat.Perception, 4 }, { Stat.Endurance, 4 },
        { Stat.Charisma, 3 }, { Stat.Intelligence, 3 }, { Stat.Agility, 5 },
        { Stat.Luck, 4 }
    });
    var enemyLevel = new LevelComponent(enemyStats);
    var enemyHealth = new HealthComponent(enemyStats, enemyLevel, eventBus);
    var enemyCombat = new CombatComponent { InCombat = false };

    enemies.Add(new EnemyInfo
    {
        Entity = enemyEntity,
        Id = id,
        Health = enemyHealth,
        Combat = enemyCombat,
    });
}
```

---

## A2 — Encounter Trigger Script

### Design

`EncounterTriggerScript` is a SyncScript attached to a "CombatManager" entity. Each frame it checks the distance from the player to each living enemy. When any enemy is within `TriggerRadius`, it enters combat. When all enemies are dead, the `CombatStateManager` auto-exits via `RemoveCombatant`.

```
┌──────────────────┐    distance < TriggerRadius    ┌────────────────────────┐
│   Exploring      │ ─────────────────────────────→  │   InCombat             │
│                  │                                  │                        │
│  EncounterTrigger│                                  │  CombatSyncScript runs │
│  checks distance │                                  │  per frame             │
│  each frame      │ ←─────────────────────────────   │                        │
│                  │    all enemies dead               │                        │
└──────────────────┘    (CombatStateManager auto-exit) └────────────────────────┘
```

### Source: EncounterTriggerScript.cs

```csharp
using Oravey2.Core.Framework.State;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Oravey2.Core.Combat;

public class EncounterTriggerScript : SyncScript
{
    /// <summary>
    /// Distance in world units at which enemies trigger combat.
    /// </summary>
    public float TriggerRadius { get; set; } = 8f;

    /// <summary>
    /// The player entity to measure distance from.
    /// </summary>
    public Entity? Player { get; set; }

    /// <summary>
    /// Reference to the game state manager for state checks.
    /// </summary>
    public GameStateManager? StateManager { get; set; }

    /// <summary>
    /// The combat state manager to call EnterCombat on.
    /// </summary>
    public CombatStateManager? CombatState { get; set; }

    /// <summary>
    /// Live list of enemies — entries are removed by CombatSyncScript on death.
    /// </summary>
    public List<EnemyInfo> Enemies { get; set; } = [];

    public override void Start() { }

    public override void Update()
    {
        if (Player == null || StateManager == null || CombatState == null)
            return;

        // Only trigger when exploring — don't re-trigger while already fighting
        if (StateManager.CurrentState != GameState.Exploring)
            return;

        var playerPos = Player.Transform.Position;

        foreach (var enemy in Enemies)
        {
            var dist = Vector3.Distance(playerPos, enemy.Entity.Transform.Position);
            if (dist <= TriggerRadius)
            {
                var ids = Enemies
                    .Where(e => e.Health.IsAlive)
                    .Select(e => e.Id)
                    .ToArray();

                if (ids.Length > 0)
                {
                    // Mark all combatants as in-combat for AP regen
                    foreach (var e in Enemies)
                        e.Combat.InCombat = true;

                    CombatState.EnterCombat(ids);
                }
                return; // Only need one trigger
            }
        }
    }
}
```

---

## A3 — Combat Sync Script

### Design

`CombatSyncScript` is the per-frame combat loop. It runs only when `GameState == InCombat`. Each frame:

1. **Regen AP** for all combatants
2. **Enemy AI** — if an enemy has enough AP, auto-queue a melee attack on the player
3. **Process ActionQueue** — dequeue one action per frame, resolve it via `CombatEngine`
4. **Handle death** — if a target's `HealthComponent.IsAlive` is false after an attack, remove the entity from the scene and call `CombatStateManager.RemoveCombatant`
5. **Hit flash** — swap material color to white for 0.2s on hit targets

The script holds references to:
- Player entity + player combat data (HealthComponent, CombatComponent)
- Enemy list (same `List<EnemyInfo>` shared with EncounterTriggerScript)
- CombatEngine, ActionQueue, CombatStateManager, DamageResolver
- IInputProvider (for reading attack input in A4)

### M0 Weapon Constants

Since there are no real weapon items in the scene, use hardcoded attack context values:

| Constant | Value | Notes |
|----------|-------|-------|
| `WeaponAccuracy` | 0.75 | 75% base accuracy for fist/melee |
| `WeaponDamage` | 12 | Base damage per hit |
| `WeaponRange` | 2.0 | Melee range |
| `CritMultiplier` | 1.5 | 50% bonus on crit |
| `MeleeAPCost` | 3 | From `CombatFormulas.DefaultAPCost(MeleeAttack)` |

### Enemy AI (M0 — simple)

Each frame, for each living enemy with `CanAfford(3)`, auto-queue a melee attack on the player. This gives enemies a steady attack rate limited by AP regen (2/s → one attack every 1.5s).

### Source: CombatSyncScript.cs

```csharp
using Oravey2.Core.Character.Health;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;

namespace Oravey2.Core.Combat;

/// <summary>
/// Holds pure-C# combat data associated with a Stride entity.
/// </summary>
internal sealed class EnemyInfo
{
    public required Entity Entity { get; init; }
    public required string Id { get; init; }
    public required HealthComponent Health { get; init; }
    public required CombatComponent Combat { get; init; }
}

public class CombatSyncScript : SyncScript
{
    // --- Player refs (set from Program.cs) ---
    public Entity? Player { get; set; }
    public HealthComponent? PlayerHealth { get; set; }
    public CombatComponent? PlayerCombat { get; set; }

    // --- Shared enemy list (also used by EncounterTriggerScript) ---
    internal List<EnemyInfo> Enemies { get; set; } = [];

    // --- Combat services (set from Program.cs) ---
    public CombatEngine? Engine { get; set; }
    public ActionQueue? Queue { get; set; }
    public CombatStateManager? CombatState { get; set; }
    public GameStateManager? StateManager { get; set; }

    // --- M0 weapon constants ---
    private const float WeaponAccuracy = 0.75f;
    private const int WeaponDamage = 12;
    private const float WeaponRange = 2f;
    private const float CritMultiplier = 1.5f;
    private const int MeleeAPCost = 3;
    private const string PlayerId = "player";

    // --- Hit flash state ---
    private readonly Dictionary<Entity, (Material Original, float Timer)> _flashingEntities = [];
    private Material? _flashMaterial;
    private const float FlashDuration = 0.2f;

    private IInputProvider? _inputProvider;

    public override void Start()
    {
        if (ServiceLocator.Instance.TryGet<IInputProvider>(out var inputProvider))
            _inputProvider = inputProvider;
    }

    public override void Update()
    {
        var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;

        // Always tick flash timers (even outside combat for lingering flashes)
        UpdateHitFlash(dt);

        if (StateManager == null || StateManager.CurrentState != GameState.InCombat)
            return;

        if (Engine == null || Queue == null || CombatState == null)
            return;

        // 1. Regen AP for all combatants
        PlayerCombat?.Regen(dt);
        foreach (var enemy in Enemies)
            enemy.Combat.Regen(dt);

        // 2. Player attack input (A4)
        HandlePlayerInput();

        // 3. Enemy AI — auto-queue melee attacks when AP allows
        RunEnemyAI();

        // 4. Process one queued action per frame
        ProcessNextAction();

        // 5. Check for dead enemies and remove them
        CleanupDead();
    }

    // ---- A4: Player Attack Input ----

    private void HandlePlayerInput()
    {
        if (_inputProvider == null || Player == null || PlayerCombat == null)
            return;

        if (!_inputProvider.IsActionPressed(GameAction.Attack))
            return;

        if (!PlayerCombat.CanAfford(MeleeAPCost))
            return;

        // Target the nearest living enemy
        var nearest = FindNearestEnemy();
        if (nearest == null) return;

        Queue!.Enqueue(new CombatAction(PlayerId, CombatActionType.MeleeAttack, nearest.Id));
    }

    private EnemyInfo? FindNearestEnemy()
    {
        if (Player == null) return null;

        var playerPos = Player.Transform.Position;
        EnemyInfo? best = null;
        var bestDist = float.MaxValue;

        foreach (var enemy in Enemies)
        {
            if (!enemy.Health.IsAlive) continue;
            var dist = Vector3.Distance(playerPos, enemy.Entity.Transform.Position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = enemy;
            }
        }

        return best;
    }

    // ---- A3: Enemy AI (M0 — simple auto-attack) ----

    private void RunEnemyAI()
    {
        foreach (var enemy in Enemies)
        {
            if (!enemy.Health.IsAlive) continue;
            if (!enemy.Combat.CanAfford(MeleeAPCost)) continue;

            // Check if there's already a queued action for this enemy
            if (Queue!.PendingActions.Any(a => a.ActorId == enemy.Id))
                continue;

            Queue.Enqueue(new CombatAction(enemy.Id, CombatActionType.MeleeAttack, PlayerId));
        }
    }

    // ---- A3: Process ActionQueue ----

    private void ProcessNextAction()
    {
        var action = Queue!.Dequeue();
        if (action == null) return;

        if (action.Type != CombatActionType.MeleeAttack || action.TargetId == null)
            return;

        // Resolve attacker and target
        CombatComponent? attackerCombat;
        HealthComponent? targetHealth;
        Entity? targetEntity;
        float distance;

        if (action.ActorId == PlayerId)
        {
            attackerCombat = PlayerCombat;
            var target = Enemies.FirstOrDefault(e => e.Id == action.TargetId);
            if (target == null || !target.Health.IsAlive) return;
            targetHealth = target.Health;
            targetEntity = target.Entity;
            distance = Vector3.Distance(
                Player!.Transform.Position, target.Entity.Transform.Position);
        }
        else
        {
            var attacker = Enemies.FirstOrDefault(e => e.Id == action.ActorId);
            if (attacker == null || !attacker.Health.IsAlive) return;
            attackerCombat = attacker.Combat;
            targetHealth = PlayerHealth;
            targetEntity = Player;
            distance = Vector3.Distance(
                attacker.Entity.Transform.Position, Player!.Transform.Position);
        }

        if (attackerCombat == null || targetHealth == null || targetEntity == null)
            return;

        var context = new AttackContext(
            WeaponAccuracy: WeaponAccuracy,
            WeaponDamage: WeaponDamage,
            WeaponRange: WeaponRange,
            CritMultiplier: CritMultiplier,
            SkillLevel: 0,      // M0: no skills
            Luck: 5,            // M0: default luck
            ArmorDR: 0,         // M0: no armor
            Cover: CoverLevel.None,
            Distance: distance);

        var result = Engine!.ProcessAttack(
            attackerCombat, context, targetHealth, MeleeAPCost);

        // A5: flash target on hit
        if (result is { Hit: true })
            StartHitFlash(targetEntity);
    }

    // ---- A5: Hit Feedback ----

    private void StartHitFlash(Entity entity)
    {
        // Find the visual child entity with a ModelComponent
        var visual = entity.GetChildren()
            .FirstOrDefault(c => c.Get<ModelComponent>() != null);
        if (visual == null) return;

        var model = visual.Get<ModelComponent>();
        if (model?.Model == null || model.Model.Materials.Count == 0) return;

        // Store original material if not already flashing
        if (_flashingEntities.ContainsKey(entity)) return;

        var originalMat = model.Model.Materials[0];

        // Create flash material once (lazy init)
        _flashMaterial ??= CreateFlashMaterial();

        model.Model.Materials[0] = _flashMaterial;
        _flashingEntities[entity] = (originalMat, FlashDuration);
    }

    private void UpdateHitFlash(float dt)
    {
        var finished = new List<Entity>();

        foreach (var (entity, (original, timer)) in _flashingEntities)
        {
            var newTimer = timer - dt;
            if (newTimer <= 0f)
            {
                // Restore original material
                var visual = entity.GetChildren()
                    .FirstOrDefault(c => c.Get<ModelComponent>() != null);
                var model = visual?.Get<ModelComponent>();
                if (model?.Model != null && model.Model.Materials.Count > 0)
                    model.Model.Materials[0] = original;

                finished.Add(entity);
            }
            else
            {
                _flashingEntities[entity] = (original, newTimer);
            }
        }

        foreach (var entity in finished)
            _flashingEntities.Remove(entity);
    }

    private Material CreateFlashMaterial()
    {
        // White flash material — uses the game's graphics device
        return Material.New(Game.GraphicsDevice, new MaterialDescriptor
        {
            Attributes =
            {
                Diffuse = new MaterialDiffuseMapFeature(
                    new ComputeColor(new Color4(1f, 1f, 1f, 1f))),
                Emissive = new MaterialEmissiveMapFeature(
                    new ComputeColor(new Color4(1f, 0.3f, 0.3f, 1f)))
            }
        });
    }

    // ---- A5: Death Cleanup ----

    private void CleanupDead()
    {
        for (var i = Enemies.Count - 1; i >= 0; i--)
        {
            var enemy = Enemies[i];
            if (enemy.Health.IsAlive) continue;

            // Remove the entity from the scene
            enemy.Entity.Scene?.Entities.Remove(enemy.Entity);
            enemy.Combat.InCombat = false;

            // Notify CombatStateManager (auto-exits combat if last enemy)
            CombatState!.RemoveCombatant(enemy.Id);

            // Remove from our tracking list
            Enemies.RemoveAt(i);
        }

        // If combat ended (all enemies dead), reset player combat state
        if (CombatState!.InCombat == false && PlayerCombat != null)
        {
            PlayerCombat.InCombat = false;
            PlayerCombat.ResetAP();
            Queue!.Clear();
        }
    }
}
```

---

## A4 — Player Attack Input

Handled inside `CombatSyncScript.HandlePlayerInput()` (see A3 source above).

- Reads `IInputProvider.IsActionPressed(GameAction.Attack)` — triggers on the frame Space is pressed
- Checks player has enough AP (`CanAfford(3)`)
- Finds the nearest living enemy
- Enqueues a `CombatAction(PlayerId, MeleeAttack, targetId)` into the shared `ActionQueue`

No new files. No changes to `KeyboardMouseInputProvider` — `GameAction.Attack` is already bound to `Keys.Space`.

---

## A5 — Hit Feedback

Handled inside `CombatSyncScript` (see A3 source above).

| Feedback | Implementation |
|----------|---------------|
| Hit flash | Swap target entity's material to white+emissive for 0.2s, then restore original |
| Death removal | Remove dead enemy entity from `Scene.Entities`, call `CombatStateManager.RemoveCombatant` |
| Combat end | When last enemy dies, `CombatStateManager.RemoveCombatant` auto-calls `ExitCombat()` → `GameState.Exploring` |
| Player death | Checked in Phase C (game over screen) — for now, player can reach 0 HP but game continues to explore state |

---

## Program.cs — Full Modifications

### New usings

```csharp
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Combat;
```

### Player combat data (after player entity creation)

```csharp
// --- Player combat data ---
var playerStats = new StatsComponent();  // default stats (all 5)
var playerLevel = new LevelComponent(playerStats);
var playerHealth = new HealthComponent(playerStats, playerLevel, eventBus);
var playerCombat = new CombatComponent { InCombat = false };
```

### Enemy spawning (after tile map section)

See [A1 section above](#a1--spawn-enemy-entities) for the enemy spawning loop.

### Combat manager entity (after enemies, before camera)

```csharp
// --- Combat Manager ---
var damageResolver = new DamageResolver();
var combatEngine = new CombatEngine(damageResolver, eventBus);
var actionQueue = new ActionQueue();
var combatStateManager = new CombatStateManager(eventBus, gameStateManager);

var combatManagerEntity = new Entity("CombatManager");

var combatScript = new CombatSyncScript
{
    Player = playerEntity,
    PlayerHealth = playerHealth,
    PlayerCombat = playerCombat,
    Engine = combatEngine,
    Queue = actionQueue,
    CombatState = combatStateManager,
    StateManager = gameStateManager,
};
combatScript.Enemies = enemies;  // shares the list

var encounterTrigger = new EncounterTriggerScript
{
    Player = playerEntity,
    StateManager = gameStateManager,
    CombatState = combatStateManager,
    TriggerRadius = 8f,
};
encounterTrigger.Enemies = enemies;  // same shared list

combatManagerEntity.Add(combatScript);
combatManagerEntity.Add(encounterTrigger);
rootScene.Entities.Add(combatManagerEntity);
```

---

## Entity Hierarchy (after Phase A)

```
Scene
├── InputManager
│   └── InputUpdateScript
├── Player
│   ├── PlayerVisual (green capsule + ModelComponent)
│   └── PlayerMovementScript
├── TileMap
│   └── TileMapRendererScript
├── enemy_1 (red capsule + ModelComponent)
├── enemy_2 (red capsule + ModelComponent)
├── enemy_3 (red capsule + ModelComponent)
├── CombatManager
│   ├── CombatSyncScript
│   └── EncounterTriggerScript
└── IsometricCamera
    ├── CameraComponent
    └── IsometricCameraScript
```

---

## Gameplay Flow

```
1. Game starts → GameState.Exploring
2. Player walks with WASD toward enemies
3. EncounterTriggerScript detects distance < 8 → CombatStateManager.EnterCombat()
   → GameState.InCombat → publishes CombatStartedEvent
4. CombatSyncScript starts running combat loop each frame:
   a. All combatants regen AP (2/s)
   b. Player presses Space → queues MeleeAttack on nearest enemy
   c. Enemies auto-queue MeleeAttack on player when AP >= 3
   d. One action dequeued per frame, resolved via CombatEngine
   e. Hits flash target white for 0.2s
   f. Dead enemies removed from scene
5. Last enemy dies → CombatStateManager.RemoveCombatant → auto-ExitCombat()
   → GameState.Exploring → publishes CombatEndedEvent
6. Player can roam freely again (no enemies remain in M0)
```

---

## Acceptance Criteria (Phase A)

| # | Criterion |
|---|-----------|
| 1 | Three red capsule enemies are visible on the tile map at startup |
| 2 | Walking within 8 units of any enemy transitions GameState to InCombat |
| 3 | Pressing Space during combat queues an attack that deals damage to the nearest enemy |
| 4 | Enemies auto-attack the player at a rate limited by AP regen |
| 5 | Hit targets flash white for ~0.2s |
| 6 | Dead enemies are removed from the scene |
| 7 | When all enemies are dead, GameState returns to Exploring |
| 8 | Existing 621 unit tests still pass (no combat logic changed) |
