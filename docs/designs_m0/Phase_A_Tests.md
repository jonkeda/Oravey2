# Design: Phase A Tests — Combat & Collision UI Tests

Adds Brinell UI tests for the Phase A combat integration and RCA-fix wall collision. Requires new automation queries to observe and control combat state.

**Depends on:** Phase A implementation (CombatSyncScript, EncounterTriggerScript, enemy spawning), RCA fix (tile-based wall collision, 32×32 map)

---

## Problem

The existing UI test suite has no coverage for:
- Enemies existing in the scene
- Combat encounter triggering (proximity → InCombat)
- Player attacks dealing damage
- Enemy death and removal
- Combat-end transition (InCombat → Exploring)
- Wall collision preventing player from leaving the map

Tests cannot currently observe enemy HP, combat state details, or force-kill enemies. Tests also cannot teleport the player, so triggering combat deterministically requires fragile long key-holds across a 32×32 map.

---

## New Automation Queries

### Game-side: OraveyAutomationHandler

| Query | Args | Returns | Purpose |
|-------|------|---------|---------|
| `GetCombatState` | — | `{inCombat, enemyCount, enemies: [{id, hp, maxHp, ap, maxAp, isAlive, x, y, z}], playerHp, playerMaxHp, playerAp, playerMaxAp}` | Full combat snapshot |
| `TeleportPlayer` | `x, y, z` | `{x, y, z}` | Move player to exact world position |
| `KillEnemy` | `enemyId` | `{killed, remainingAlive}` | Force-kill a specific enemy for deterministic testing |

### Why these three

- **`GetCombatState`** — The only way to observe enemy HP, alive count, and the `InCombat` flag in one call. `GetGameState` returns "Exploring"/"InCombat" but not HP values.
- **`TeleportPlayer`** — Without this, reaching enemy_1 at (8, 0.5, 8) from origin requires ~1.6s of W hold in the right direction, and the direction depends on camera yaw. Teleport makes encounter-trigger tests deterministic and fast.
- **`KillEnemy`** — Combat is RNG-based (75% hit, variable damage). Testing "all enemies dead → Exploring" by pressing Space repeatedly is non-deterministic. `KillEnemy` lets us test the state-transition logic directly.

### Implementation: OraveyAutomationHandler additions

```csharp
"GetCombatState" => GetCombatState(),
"TeleportPlayer" => TeleportPlayer(command),
"KillEnemy" => KillEnemy(command),
```

#### GetCombatState

Finds the `CombatSyncScript` on the "CombatManager" entity. Reads `Enemies` list (internal, visible via `InternalsVisibleTo`), `PlayerHealth`, `PlayerCombat`, and `CombatState.InCombat`.

```csharp
private AutomationResponse GetCombatState()
{
    var combatManager = FindEntity("CombatManager");
    if (combatManager == null)
        return AutomationResponse.Fail("CombatManager entity not found");

    var script = combatManager.Get<CombatSyncScript>();
    if (script == null)
        return AutomationResponse.Fail("CombatSyncScript not found");

    var enemies = script.Enemies.Select(e => new
    {
        id = e.Id,
        hp = e.Health.CurrentHP,
        maxHp = e.Health.MaxHP,
        ap = (int)e.Combat.CurrentAP,
        maxAp = e.Combat.MaxAP,
        isAlive = e.Health.IsAlive,
        x = e.Entity.Transform.Position.X,
        y = e.Entity.Transform.Position.Y,
        z = e.Entity.Transform.Position.Z,
    });

    var result = new
    {
        inCombat = script.CombatState?.InCombat ?? false,
        enemyCount = script.Enemies.Count,
        enemies,
        playerHp = script.PlayerHealth?.CurrentHP ?? 0,
        playerMaxHp = script.PlayerHealth?.MaxHP ?? 0,
        playerAp = (int)(script.PlayerCombat?.CurrentAP ?? 0),
        playerMaxAp = script.PlayerCombat?.MaxAP ?? 0,
    };
    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(result));
}
```

#### TeleportPlayer

Sets `Player.Transform.Position` directly. Safe because `PlayerMovementScript` will apply tile collision on the next frame if the position is invalid.

```csharp
private AutomationResponse TeleportPlayer(AutomationCommand command)
{
    if (command.Args == null || command.Args.Length < 3)
        return AutomationResponse.Fail("TeleportPlayer requires x, y, z arguments");

    float x = Convert.ToSingle(command.Args[0]?.ToString());
    float y = Convert.ToSingle(command.Args[1]?.ToString());
    float z = Convert.ToSingle(command.Args[2]?.ToString());

    var player = FindEntity("Player");
    if (player == null)
        return AutomationResponse.Fail("Player entity not found");

    player.Transform.Position = new Vector3(x, y, z);
    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(
        new { x, y, z }));
}
```

#### KillEnemy

Finds the enemy by ID in `CombatSyncScript.Enemies`, calls `TakeDamage(CurrentHP)`. The script's `CleanupDead()` removes the entity on the next game frame.

```csharp
private AutomationResponse KillEnemy(AutomationCommand command)
{
    var enemyId = command.Args?.FirstOrDefault()?.ToString();
    if (string.IsNullOrEmpty(enemyId))
        return AutomationResponse.Fail("Enemy ID required as first argument");

    var combatManager = FindEntity("CombatManager");
    var script = combatManager?.Get<CombatSyncScript>();
    if (script == null)
        return AutomationResponse.Fail("CombatSyncScript not found");

    var enemy = script.Enemies.FirstOrDefault(e => e.Id == enemyId);
    if (enemy == null)
        return AutomationResponse.Fail($"Enemy '{enemyId}' not found");

    enemy.Health.TakeDamage(enemy.Health.CurrentHP);
    var remaining = script.Enemies.Count(e => e.Health.IsAlive);

    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(
        new { killed = true, remainingAlive = remaining }));
}
```

---

## Test-side: GameQueryHelpers additions

```csharp
public record CombatState(
    bool InCombat, int EnemyCount,
    List<EnemyState> Enemies,
    int PlayerHp, int PlayerMaxHp,
    int PlayerAp, int PlayerMaxAp);

public record EnemyState(
    string Id, int Hp, int MaxHp, int Ap, int MaxAp,
    bool IsAlive, double X, double Y, double Z);

public static CombatState GetCombatState(IStrideTestContext context) { ... }
public static Position TeleportPlayer(IStrideTestContext context, double x, double y, double z) { ... }
public static (bool Killed, int RemainingAlive) KillEnemy(IStrideTestContext context, string enemyId) { ... }
```

---

## File Layout

```
src/Oravey2.Windows/
└── OraveyAutomationHandler.cs    # MODIFY — add GetCombatState, TeleportPlayer, KillEnemy

tests/Oravey2.UITests/
├── GameQueryHelpers.cs           # MODIFY — add CombatState, EnemyState records + 3 new helpers
├── CombatTriggerTests.cs         # NEW — encounter trigger + state transition tests
├── CombatGameplayTests.cs        # NEW — attack, damage, death, combat-end tests
└── WallCollisionTests.cs         # NEW — boundary enforcement tests
```

---

## World Reference (32×32 map)

```
32×32 grid, TileSize=1.0, centered at world origin.
Tile (0,0)   = world (-15.5, *, -15.5)    — border wall
Tile (1,1)   = world (-14.5, *, -14.5)    — first walkable tile
Tile (16,16) = world (0.5,  *, 0.5)       — near centre (road intersection)
Tile (30,30) = world (14.5, *, 14.5)      — last walkable tile
Tile (31,31) = world (15.5, *, 15.5)      — border wall

Player starts at world (0, 0.5, 0).
Enemies:
  enemy_1 at (8,   0.5,  8)    — NE quadrant
  enemy_2 at (-6,  0.5, 10)    — NW quadrant
  enemy_3 at (10,  0.5, -6)    — SE quadrant

Trigger radius: 5 units
```

### Key teleport positions

| Name | Position | Purpose |
|------|----------|---------|
| Origin (safe) | `(0, 0.5, 0)` | Player start, far from all enemies |
| Near enemy_1 | `(4, 0.5, 8)` | Distance 4.0 < trigger radius 5 → triggers combat |
| Near enemy_2 | `(-2, 0.5, 10)` | Distance 4.0 < 5 → triggers combat |
| Near enemy_3 | `(6, 0.5, -6)` | Distance 4.0 < 5 → triggers combat |
| North wall | `(0, 0.5, 14)` | Near border for wall collision test |
| South wall | `(0, 0.5, -14)` | Near border for wall collision test |
| East wall | `(14, 0.5, 0)` | Near border for wall collision test |
| West wall | `(-14, 0.5, 0)` | Near border for wall collision test |

---

## Test Class 1: CombatTriggerTests

Tests encounter detection and GameState transitions. Each test starts with a fresh game (class-level fixture).

| # | Test | Steps | Assertion |
|---|------|-------|-----------|
| 1 | `StartState_IsExploring` | Query game state | `== "Exploring"` |
| 2 | `ThreeEnemies_ExistAtStartup` | `GetCombatState()` | `EnemyCount == 3`, all `IsAlive` |
| 3 | `AllEnemies_InsideMapBounds` | `GetCombatState()` → check each enemy X/Z | All within `(-14.5, 14.5)` |
| 4 | `PlayerAtOrigin_NoCombat` | Query state at origin | `== "Exploring"`, `InCombat == false` |
| 5 | `TeleportNearEnemy1_TriggersCombat` | `TeleportPlayer(4, 0.5, 8)` → `PressKey(Space)` (frame tick) → `GetGameState()` | `== "InCombat"` |
| 6 | `TeleportFarFromEnemies_StaysExploring` | `TeleportPlayer(0, 0.5, 0)` → tick → `GetGameState()` | `== "Exploring"` |
| 7 | `CombatState_ShowsInCombat` | Teleport near enemy_1 → tick → `GetCombatState()` | `InCombat == true` |

**Note on frame ticks:** After `TeleportPlayer`, the encounter trigger runs on the next game frame. A single `PressKey(VirtualKey.None)` or short `HoldKey(VirtualKey.W, 50)` advances the game loop enough for the trigger to fire. Alternatively, poll `GetGameState` in a loop (up to 500ms).

---

## Test Class 2: CombatGameplayTests

Tests attack, damage, death, and combat-end. Uses `TeleportPlayer` + `KillEnemy` for deterministic control.

| # | Test | Steps | Assertion |
|---|------|-------|-----------|
| 1 | `PlayerCanAttack_DamagesEnemy` | Teleport near enemy_1 → wait for InCombat → press Space 5 times (spaced 500ms for AP regen) → `GetCombatState()` | `enemy_1.Hp < enemy_1.MaxHp` (at least 1 hit out of 5 at 75% accuracy) |
| 2 | `EnemiesAttackPlayer_OverTime` | Teleport near enemy_1 → wait for InCombat → idle 4s → `GetCombatState()` | `PlayerHp < PlayerMaxHp` |
| 3 | `KillEnemy_RemovesFromList` | Teleport near enemy_1 → wait for InCombat → `KillEnemy("enemy_1")` → wait → `GetCombatState()` | `EnemyCount == 2`, no enemy with `id == "enemy_1"` |
| 4 | `KillEnemy_EntityRemovedFromScene` | After killing enemy_1 → `GetEntityPosition("enemy_1")` | Response fails (entity removed) |
| 5 | `AllEnemiesDead_ReturnsToExploring` | Teleport near enemy_1 → wait for InCombat → `KillEnemy` all 3 → wait → `GetGameState()` | `== "Exploring"` |
| 6 | `AllEnemiesDead_CombatStateReset` | After killing all → `GetCombatState()` | `InCombat == false`, `EnemyCount == 0` |

**"wait for InCombat" pattern:**

```csharp
GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
// Poll until combat triggers (encounter script runs on next frame)
CombatState state;
for (int i = 0; i < 10; i++)
{
    _fixture.Context.HoldKey(VirtualKey.Space, 50); // tick the game loop
    state = GameQueryHelpers.GetCombatState(_fixture.Context);
    if (state.InCombat) break;
}
Assert.True(state.InCombat, "Combat should have triggered");
```

**"wait after kill" pattern:**

```csharp
GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
// CleanupDead runs next frame
_fixture.Context.HoldKey(VirtualKey.Space, 100); // advance frames
var state = GameQueryHelpers.GetCombatState(_fixture.Context);
```

---

## Test Class 3: WallCollisionTests

Tests that tile-based collision prevents the player from walking off the map or through walls.

| # | Test | Steps | Assertion |
|---|------|-------|-----------|
| 1 | `PlayerCannot_WalkPastNorthWall` | `TeleportPlayer(0, 0.5, 13.5)` → `HoldKey(W, 2000)` → `GetPlayerPosition()` | `Z <= 14.5` (inside walkable area) |
| 2 | `PlayerCannot_WalkPastSouthWall` | `TeleportPlayer(0, 0.5, -13.5)` → `HoldKey(S, 2000)` → pos | `Z >= -14.5` |
| 3 | `PlayerCannot_WalkPastEastWall` | `TeleportPlayer(13.5, 0.5, 0)` → `HoldKey(D, 2000)` → pos | `X <= 14.5` |
| 4 | `PlayerCannot_WalkPastWestWall` | `TeleportPlayer(-13.5, 0.5, 0)` → `HoldKey(A, 2000)` → pos | `X >= -14.5` |
| 5 | `PlayerSlides_AlongWallDiagonal` | Teleport near NE corner `(13.5, 0.5, 13.5)` → `HoldKey(W, 1000)` → pos | Player position changed (not stuck at teleport point) but stayed inside bounds |
| 6 | `PlayerOnWalkableTile_AfterCollision` | After any wall test → `GetTileAtWorldPos(pos.X, pos.Z)` | Tile type is Ground, Road, or Rubble (not Wall, Water, or Empty) |

**Direction note:** W maps to camera-relative "forward", which at default yaw=45° moves in both +X and -Z world directions. For clean N/S/E/W tests, we use the fact that holding W from `(0, 0.5, 13.5)` will try to move the player further +Z (toward the wall) — the Z component of movement includes a -Z factor at yaw=45°, so we may need to verify using absolute bounds rather than exact direction. The key assertion is "player position stays inside walkable bounds" regardless of exact direction.

---

## Acceptance Criteria

| # | Criterion |
|---|-----------|
| 1 | `GetCombatState`, `TeleportPlayer`, `KillEnemy` queries work via automation pipe |
| 2 | `CombatTriggerTests` — 7 tests pass |
| 3 | `CombatGameplayTests` — 6 tests pass |
| 4 | `WallCollisionTests` — 6 tests pass |
| 5 | All existing UI tests still pass |
| 6 | All 635 unit tests still pass |
