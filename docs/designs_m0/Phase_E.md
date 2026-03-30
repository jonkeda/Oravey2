# Design: Phase E — Test Scenario System

Adds automation commands that let UI tests configure game scenarios at runtime — spawning/removing enemies with custom stats, adjusting player health, and resetting combat state. After this phase, each test class can set up the exact scenario it needs without relying on the default Program.cs enemy layout.

**Depends on:** Phase A (enemy/combat wiring), Phase B (inventory), Phase D (weapon/stats integration)

---

## Problem

All 21 test classes share the same hardcoded game layout: 3 enemies at fixed positions with fixed stats. Tests that need specific combat scenarios must work around this with `TeleportPlayer`, `KillEnemy`, and `DamagePlayer` — all of which leave residual state (dead enemies stay dead, player HP stays reduced, loot cubes remain). This causes:

1. **Flaky combat tests** — `FullCombat_PlayerSurvives` depends on RNG across a 28-second fight. No way to create a deterministic 1v1 scenario.
2. **Test coupling** — Tests within a class share game state. Earlier tests that trigger combat affect later tests.
3. **Missing coverage** — Can't test edge cases like "player vs 1 weak enemy" or "player vs 5 enemies" or "enemy with specific weapon".
4. **Balance iteration difficulty** — Tuning enemy stats requires editing `Program.cs` and `M0Items.cs`, rebuilding, then running the full test suite.

---

## Approach: Runtime Scenario Commands

Rather than file-based configs or command-line scenario selection, add automation commands that tests call at the start of each test class (in `InitializeAsync` or the first test). This:

- Requires **zero changes** to `Program.cs` bootstrap or `OraveyTestFixture`
- Uses the **existing pipe communication** infrastructure
- Follows the established pattern (`TeleportPlayer`, `KillEnemy`, `DamagePlayer`)
- Keeps the default 3-enemy layout for all existing tests (backward compatible)

---

## Scope

| Sub-task | Summary |
|----------|---------|
| E1 | `ResetScenario` command — remove all enemies, reset player to full HP, clear combat state, return to Exploring |
| E2 | `SpawnEnemy` command — add an enemy with custom id, position, stats, weapon, HP |
| E3 | `SetPlayerStats` command — override player SPECIAL stats and/or max HP for a test |
| E4 | `SetPlayerWeapon` command — equip a weapon by item ID or with custom stats |
| E5 | GameQueryHelpers + test fixtures for scenario-based tests |

### What's Deferred

| Item | Deferred To |
|------|-------------|
| JSON scenario files loaded from disk | Post-M0 |
| Named scenario presets ("1v1_easy", "3v3_hard") | Post-M0 |
| Custom tile maps per test | Post-M0 |
| Loot table configuration per test | Post-M0 |
| Save/restore game snapshots | Post-M0 |

---

## E1 — ResetScenario

Clears all enemies, resets player, and returns to a clean Exploring state. Called at the start of scenario-based test classes.

### Automation query

| Query | Args | Returns | Purpose |
|-------|------|---------|---------|
| `ResetScenario` | — | `{ success, playerHp, enemyCount }` | Clean slate for custom scenario setup |

### Game-side implementation

```csharp
private AutomationResponse ResetScenario()
{
    // 1. Find combat manager
    var combatManager = FindEntity("CombatManager");
    var combatScript = combatManager?.Get<CombatSyncScript>();
    var triggerScript = combatManager?.Get<EncounterTriggerScript>();
    if (combatScript == null)
        return AutomationResponse.Fail("CombatSyncScript not found");

    // 2. Remove all enemy entities from scene
    foreach (var enemy in combatScript.Enemies.ToList())
    {
        enemy.Entity.Scene?.Entities.Remove(enemy.Entity);
        enemy.Combat.InCombat = false;
    }
    combatScript.Enemies.Clear();

    if (triggerScript != null)
        triggerScript.Enemies.Clear();

    // 3. Exit combat if active
    if (combatScript.CombatState?.InCombat == true)
        combatScript.CombatState.ExitCombat();

    // 4. Reset player
    if (combatScript.PlayerCombat != null)
    {
        combatScript.PlayerCombat.InCombat = false;
        combatScript.PlayerCombat.ResetAP();
    }
    combatScript.Queue?.Clear();

    _playerHealth?.HealToMax();

    // 5. Ensure Exploring state
    if (_gameStateManager?.CurrentState != GameState.Exploring)
        _gameStateManager?.ForceState(GameState.Exploring);

    // 6. Teleport player to origin
    var player = FindEntity("Player");
    if (player != null)
        player.Transform.Position = new Vector3(0, 0.5f, 0);

    // 7. Remove loot cubes
    var lootEntities = _rootScene.Entities
        .Where(e => LootDropScript.HasLoot(e))
        .ToList();
    foreach (var loot in lootEntities)
        _rootScene.Entities.Remove(loot);

    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
    {
        success = true,
        playerHp = _playerHealth?.CurrentHP ?? 0,
        enemyCount = combatScript.Enemies.Count,
    }));
}
```

### Required change: GameStateManager.ForceState

`GameStateManager.TransitionTo` validates state transitions. During reset, we may be in `GameOver` and need to go back to `Exploring` — which is not a valid transition. Add:

```csharp
// In GameStateManager.cs
public void ForceState(GameState newState)
{
    var old = CurrentState;
    CurrentState = newState;
    _eventBus.Publish(new GameStateChangedEvent(old, newState));
}
```

This is only used for test automation, not normal gameplay.

---

## E2 — SpawnEnemy

Creates a new enemy entity with custom configuration and adds it to the scene + combat/trigger systems.

### Automation query

| Query | Args | Returns | Purpose |
|-------|------|---------|---------|
| `SpawnEnemy` | `{ id, x, z, hp, endurance, luck, weaponDamage, weaponAccuracy }` | `{ success, id, hp, maxHp }` | Add a custom enemy for scenario testing |

### Args format

Args are passed as a JSON string in `command.Args[0]`:

```csharp
AutomationCommand.GameQuery("SpawnEnemy", 
    JsonSerializer.Serialize(new { 
        id = "test_enemy_1", 
        x = 3.0, z = 3.0, 
        hp = 30, endurance = 1, luck = 3,
        weaponDamage = 4, weaponAccuracy = 0.50 
    }))
```

### Game-side implementation

```csharp
private AutomationResponse SpawnEnemy(AutomationCommand command)
{
    if (command.Args == null || command.Args.Length < 1)
        return AutomationResponse.Fail("SpawnEnemy requires JSON config argument");

    var json = command.Args[0]?.ToString();
    if (string.IsNullOrEmpty(json))
        return AutomationResponse.Fail("SpawnEnemy config is empty");

    var config = JsonSerializer.Deserialize<JsonElement>(json);

    var id = config.GetProperty("id").GetString() ?? $"enemy_{Guid.NewGuid():N}";
    var x = (float)config.GetProperty("x").GetDouble();
    var z = (float)config.GetProperty("z").GetDouble();
    var endurance = config.TryGetProperty("endurance", out var endProp) ? endProp.GetInt32() : 1;
    var luck = config.TryGetProperty("luck", out var luckProp) ? luckProp.GetInt32() : 3;
    var weaponDamage = config.TryGetProperty("weaponDamage", out var dmgProp) ? dmgProp.GetInt32() : 4;
    var weaponAccuracy = config.TryGetProperty("weaponAccuracy", out var accProp) ? (float)accProp.GetDouble() : 0.50f;

    // Create enemy entity
    var enemyEntity = new Entity(id);
    enemyEntity.Transform.Position = new Vector3(x, 0.5f, z);

    // Visual (red capsule)
    var enemyVisual = new Entity($"{id}_Visual");
    var mesh = GeometricPrimitive.Capsule.New(_game.GraphicsDevice, 0.3f, 0.8f).ToMeshDraw();
    var model = new Model();
    model.Meshes.Add(new Mesh { Draw = mesh });
    model.Materials.Add(_enemyMaterial ??= _game.CreateMaterial(new Color(0.8f, 0.15f, 0.15f)));
    enemyVisual.Add(new ModelComponent(model));
    enemyEntity.AddChild(enemyVisual);

    _rootScene.Entities.Add(enemyEntity);

    // Stats
    var stats = new StatsComponent(new Dictionary<Stat, int>
    {
        { Stat.Strength, 3 }, { Stat.Perception, 3 }, { Stat.Endurance, endurance },
        { Stat.Charisma, 2 }, { Stat.Intelligence, 2 }, { Stat.Agility, 4 },
        { Stat.Luck, luck },
    });
    var level = new LevelComponent(stats);
    var health = new HealthComponent(stats, level, _eventBus);
    var combat = new CombatComponent { InCombat = false };

    // Override HP if specified
    if (config.TryGetProperty("hp", out var hpProp))
    {
        var targetHp = hpProp.GetInt32();
        if (targetHp < health.MaxHP)
            health.TakeDamage(health.MaxHP - targetHp);
    }

    // Weapon
    var weapon = new WeaponData(
        Damage: weaponDamage, Range: 1.5f, ApCost: 3,
        Accuracy: weaponAccuracy, SkillType: "melee", CritMultiplier: 1.5f);

    var enemyInfo = new EnemyInfo
    {
        Entity = enemyEntity,
        Id = id,
        Health = health,
        Combat = combat,
        Stats = stats,
        Weapon = weapon,
    };

    // Add to combat + trigger systems
    var combatManager = FindEntity("CombatManager");
    var combatScript = combatManager?.Get<CombatSyncScript>();
    var triggerScript = combatManager?.Get<EncounterTriggerScript>();

    combatScript?.Enemies.Add(enemyInfo);
    triggerScript?.Enemies.Add(enemyInfo);

    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
    {
        success = true,
        id,
        hp = health.CurrentHP,
        maxHp = health.MaxHP,
    }));
}
```

### Required: Store shared references

`SpawnEnemy` needs `_game`, `_eventBus`, and a cached enemy material. The handler already has `_game` and `_rootScene`. Add:

```csharp
private IEventBus? _eventBus;
private MaterialInstance? _enemyMaterial;
```

Wire `_eventBus` in `SetPhaseB` or a new `SetPhaseE`.

---

## E3 — SetPlayerStats

Overrides player SPECIAL stats for a test. Affects HP (via Endurance), crit chance (Luck), etc.

### Automation query

| Query | Args | Returns | Purpose |
|-------|------|---------|---------|
| `SetPlayerStats` | `{ endurance?, luck?, strength?, hp? }` | `{ success, hp, maxHp }` | Adjust player stats for specific tests |

### Game-side implementation

```csharp
private AutomationResponse SetPlayerStats(AutomationCommand command)
{
    if (command.Args == null || command.Args.Length < 1)
        return AutomationResponse.Fail("SetPlayerStats requires JSON config argument");

    var json = command.Args[0]?.ToString();
    var config = JsonSerializer.Deserialize<JsonElement>(json!);

    var combatManager = FindEntity("CombatManager");
    var script = combatManager?.Get<CombatSyncScript>();
    if (script?.PlayerStats == null || _playerHealth == null)
        return AutomationResponse.Fail("Player stats not initialized");

    // Override individual SPECIAL stats if provided
    if (config.TryGetProperty("endurance", out var endProp))
        script.PlayerStats.SetBase(Stat.Endurance, endProp.GetInt32());
    if (config.TryGetProperty("luck", out var luckProp))
        script.PlayerStats.SetBase(Stat.Luck, luckProp.GetInt32());
    if (config.TryGetProperty("strength", out var strProp))
        script.PlayerStats.SetBase(Stat.Strength, strProp.GetInt32());

    // Heal to new max HP after stat change
    _playerHealth.HealToMax();

    // Override HP directly if specified (e.g. set to 50 HP for a low-HP test)
    if (config.TryGetProperty("hp", out var hpProp))
    {
        var targetHp = hpProp.GetInt32();
        if (targetHp < _playerHealth.MaxHP)
            _playerHealth.TakeDamage(_playerHealth.MaxHP - targetHp);
    }

    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
    {
        success = true,
        hp = _playerHealth.CurrentHP,
        maxHp = _playerHealth.MaxHP,
    }));
}
```

### Required: StatsComponent.SetBase

`StatsComponent` currently only has a constructor. Add a setter for test use:

```csharp
// In StatsComponent.cs
public void SetBase(Stat stat, int value)
{
    _baseStats[stat] = Math.Clamp(value, 1, 10);
}
```

---

## E4 — SetPlayerWeapon

Equips a weapon by item ID or with custom stats.

This is already mostly covered by the existing `EquipItem` query from Phase D. For custom weapon stats (not a known item), add an override:

### Automation query

| Query | Args | Returns | Purpose |
|-------|------|---------|---------|
| `SetPlayerWeapon` | `{ damage, accuracy, range?, apCost?, critMultiplier? }` | `{ success, damage, accuracy }` | Equip a custom weapon for balance testing |

### Game-side implementation

```csharp
private AutomationResponse SetPlayerWeapon(AutomationCommand command)
{
    if (command.Args == null || command.Args.Length < 1)
        return AutomationResponse.Fail("SetPlayerWeapon requires JSON config argument");

    var json = command.Args[0]?.ToString();
    var config = JsonSerializer.Deserialize<JsonElement>(json!);

    if (_playerEquipment == null || _playerInventory == null)
        return AutomationResponse.Fail("Equipment not initialized");

    var damage = config.GetProperty("damage").GetInt32();
    var accuracy = (float)config.GetProperty("accuracy").GetDouble();
    var range = config.TryGetProperty("range", out var rProp) ? (float)rProp.GetDouble() : 2f;
    var apCost = config.TryGetProperty("apCost", out var apProp) ? apProp.GetInt32() : 3;
    var critMult = config.TryGetProperty("critMultiplier", out var cProp) ? (float)cProp.GetDouble() : 1.5f;

    var weaponData = new WeaponData(damage, range, apCost, accuracy, "melee", CritMultiplier: critMult);
    var definition = new ItemDefinition(
        Id: "test_weapon",
        Name: "Test Weapon",
        Description: "Custom test weapon",
        Category: ItemCategory.WeaponMelee,
        Weight: 1f,
        Stackable: false,
        Value: 0,
        Slot: EquipmentSlot.PrimaryWeapon,
        Weapon: weaponData);

    var item = new ItemInstance(definition);
    _playerInventory.Add(item);
    _playerEquipment.Equip(item, EquipmentSlot.PrimaryWeapon);

    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
    {
        success = true,
        damage,
        accuracy,
    }));
}
```

---

## E5 — Test-side Helpers

### GameQueryHelpers additions

```csharp
// --- Phase E: Scenario helpers ---

public record ScenarioResetResult(bool Success, int PlayerHp, int EnemyCount);

public record SpawnEnemyResult(bool Success, string Id, int Hp, int MaxHp);

public record SetStatsResult(bool Success, int Hp, int MaxHp);

public record SetWeaponResult(bool Success, int Damage, float Accuracy);

public static ScenarioResetResult ResetScenario(IStrideTestContext context)
{
    var response = context.SendCommand(AutomationCommand.GameQuery("ResetScenario"));
    if (!response.Success)
        throw new InvalidOperationException($"ResetScenario failed: {response.Error}");

    var je = (JsonElement)response.Result!;
    return new ScenarioResetResult(
        je.GetProperty("success").GetBoolean(),
        je.GetProperty("playerHp").GetInt32(),
        je.GetProperty("enemyCount").GetInt32());
}

public static SpawnEnemyResult SpawnEnemy(
    IStrideTestContext context,
    string id, double x, double z,
    int? hp = null, int endurance = 1, int luck = 3,
    int weaponDamage = 4, float weaponAccuracy = 0.50f)
{
    var config = JsonSerializer.Serialize(new
    {
        id, x, z, hp, endurance, luck, weaponDamage, weaponAccuracy,
    });

    var response = context.SendCommand(AutomationCommand.GameQuery("SpawnEnemy", config));
    if (!response.Success)
        throw new InvalidOperationException($"SpawnEnemy failed: {response.Error}");

    var je = (JsonElement)response.Result!;
    return new SpawnEnemyResult(
        je.GetProperty("success").GetBoolean(),
        je.GetProperty("id").GetString() ?? "",
        je.GetProperty("hp").GetInt32(),
        je.GetProperty("maxHp").GetInt32());
}

public static SetStatsResult SetPlayerStats(
    IStrideTestContext context,
    int? endurance = null, int? luck = null,
    int? strength = null, int? hp = null)
{
    var config = JsonSerializer.Serialize(new { endurance, luck, strength, hp });
    var response = context.SendCommand(AutomationCommand.GameQuery("SetPlayerStats", config));
    if (!response.Success)
        throw new InvalidOperationException($"SetPlayerStats failed: {response.Error}");

    var je = (JsonElement)response.Result!;
    return new SetStatsResult(
        je.GetProperty("success").GetBoolean(),
        je.GetProperty("hp").GetInt32(),
        je.GetProperty("maxHp").GetInt32());
}

public static SetWeaponResult SetPlayerWeapon(
    IStrideTestContext context,
    int damage, float accuracy,
    float range = 2f, int apCost = 3, float critMultiplier = 1.5f)
{
    var config = JsonSerializer.Serialize(new { damage, accuracy, range, apCost, critMultiplier });
    var response = context.SendCommand(AutomationCommand.GameQuery("SetPlayerWeapon", config));
    if (!response.Success)
        throw new InvalidOperationException($"SetPlayerWeapon failed: {response.Error}");

    var je = (JsonElement)response.Result!;
    return new SetWeaponResult(
        je.GetProperty("success").GetBoolean(),
        je.GetProperty("damage").GetInt32(),
        (float)je.GetProperty("accuracy").GetDouble());
}
```

### Example: Deterministic combat test

```csharp
public class DeterministicCombatTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void Player_Beats_SingleWeakEnemy()
    {
        // Reset to clean slate
        GameQueryHelpers.ResetScenario(_fixture.Context);

        // Spawn one weak enemy right next to the player
        GameQueryHelpers.SpawnEnemy(_fixture.Context,
            id: "weak_1", x: 2, z: 0,
            hp: 20, weaponDamage: 2, weaponAccuracy: 0.30f);

        // Give player a strong weapon
        GameQueryHelpers.SetPlayerWeapon(_fixture.Context, damage: 20, accuracy: 0.90f);

        // Walk into trigger range and fight
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 1, 0.5, 0);
        for (int i = 0; i < 20; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 500);
            if (GameQueryHelpers.GetGameState(_fixture.Context) != "InCombat") break;
        }

        Assert.Equal("Exploring", GameQueryHelpers.GetGameState(_fixture.Context));
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.True(hud.Hp > 80, $"Player should barely be scratched; HP={hud.Hp}");
    }

    [Fact]
    public void Player_Dies_Against_StrongEnemy()
    {
        GameQueryHelpers.ResetScenario(_fixture.Context);

        // Spawn one very strong enemy
        GameQueryHelpers.SpawnEnemy(_fixture.Context,
            id: "boss", x: 2, z: 0,
            hp: 500, weaponDamage: 50, weaponAccuracy: 0.95f);

        GameQueryHelpers.TeleportPlayer(_fixture.Context, 1, 0.5, 0);
        for (int i = 0; i < 20; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 500);
            var state = GameQueryHelpers.GetGameState(_fixture.Context);
            if (state != "InCombat") break;
        }

        Assert.Equal("GameOver", GameQueryHelpers.GetGameState(_fixture.Context));
    }

    [Fact]
    public void BalancedFight_PlayerWins_WithLowHp()
    {
        GameQueryHelpers.ResetScenario(_fixture.Context);

        // 3 enemies tuned so player wins with ~20% HP
        for (int i = 1; i <= 3; i++)
        {
            GameQueryHelpers.SpawnEnemy(_fixture.Context,
                id: $"balanced_{i}", x: 3 + i, z: 0,
                hp: 40, weaponDamage: 3, weaponAccuracy: 0.45f);
        }

        GameQueryHelpers.TeleportPlayer(_fixture.Context, 3, 0.5, 0);
        for (int i = 0; i < 30; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 2000);
            if (GameQueryHelpers.GetGameState(_fixture.Context) != "InCombat") break;
        }

        Assert.Equal("Exploring", GameQueryHelpers.GetGameState(_fixture.Context));
    }
}
```

---

## File Layout

```
src/Oravey2.Core/
├── Character/Stats/
│   └── StatsComponent.cs                     # MODIFY — add SetBase(Stat, int)
├── Framework/State/
│   └── GameStateManager.cs                   # MODIFY — add ForceState(GameState)

src/Oravey2.Windows/
└── OraveyAutomationHandler.cs                # MODIFY — add ResetScenario, SpawnEnemy, SetPlayerStats, SetPlayerWeapon

tests/Oravey2.UITests/
├── GameQueryHelpers.cs                       # MODIFY — add scenario records + helper methods
├── DeterministicCombatTests.cs               # NEW — example scenario-based tests
└── BalanceTuningTests.cs                     # NEW — replace flaky FullCombat test with scenario-based version
```

---

## Acceptance Criteria

| # | Criterion |
|---|-----------|
| 1 | `ResetScenario` removes all enemies, heals player, returns to Exploring |
| 2 | `SpawnEnemy` creates a visible enemy entity with custom HP, weapon, and position |
| 3 | Spawned enemies trigger combat when player enters trigger radius |
| 4 | Spawned enemies attack with their configured weapon stats |
| 5 | `SetPlayerStats` updates SPECIAL stats and recalculates max HP |
| 6 | `SetPlayerWeapon` equips a custom weapon that combat reads |
| 7 | Existing 101 UI tests (Phases A-C) still pass unchanged |
| 8 | Existing 635 unit tests still pass |
| 9 | `DeterministicCombatTests` demonstrate all scenario commands working together |
| 10 | The flaky `FullCombat_PlayerSurvives` test is replaced with a deterministic scenario |
