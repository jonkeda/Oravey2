# Test Design: Phase E — Test Scenario System

Tests verify that the four scenario automation commands work correctly and compose together for deterministic combat testing.

**Total:** 13 tests across 3 classes

---

## Class 1: ScenarioResetTests (4 tests)

Verifies `ResetScenario` returns the game to a clean Exploring state regardless of starting conditions.

### Test 1.1 — ResetScenario_RemovesAllEnemies
**Setup:** Default game launch (3 enemies in scene)  
**Action:** Call `ResetScenario`  
**Assert:** Response `enemyCount == 0`. `GetCombatState` returns `InCombat == false`.

### Test 1.2 — ResetScenario_HealsPlayerToMax
**Setup:** `DamagePlayer` to reduce HP to 50  
**Action:** Call `ResetScenario`  
**Assert:** Response `playerHp` equals max HP from `GetHudState`.

### Test 1.3 — ResetScenario_ExitsCombat
**Setup:** `TeleportPlayer` next to enemies, `HoldKey(Space, 500)` to trigger combat  
**Precondition Assert:** `GetGameState` returns `InCombat`  
**Action:** Call `ResetScenario`  
**Assert:** `GetGameState` returns `Exploring`.

### Test 1.4 — ResetAfterGameOver_RestoresToExploring
**Setup:** `DamagePlayer(1000)` to trigger GameOver  
**Precondition Assert:** `GetGameState` returns `GameOver`  
**Action:** Call `ResetScenario`  
**Assert:** `GetGameState` returns `Exploring`. `GetHudState` shows full HP.

---

## Class 2: ScenarioSpawnTests (5 tests)

Verifies `SpawnEnemy`, `SetPlayerStats`, and `SetPlayerWeapon` create/modify entities with correct properties.

### Test 2.1 — SpawnEnemy_CreatesEnemyAtPosition
**Setup:** `ResetScenario` to clear enemies  
**Action:** `SpawnEnemy(id: "test_1", x: 3, z: 0, hp: 50)`  
**Assert:** Response `success == true`, `id == "test_1"`, `hp == 50`. `GetSceneDiagnostics` shows entity count increased.

### Test 2.2 — SpawnEnemy_CustomWeaponStats
**Setup:** `ResetScenario`, then `SpawnEnemy(id: "custom_wpn", x: 3, z: 0, weaponDamage: 10, weaponAccuracy: 0.80)`  
**Action:** `GetCombatConfig`  
**Assert:** Enemy weapon damage and accuracy match the spawn config values.

### Test 2.3 — SpawnEnemy_MultipleEnemies
**Setup:** `ResetScenario`  
**Action:** Spawn 3 enemies at different positions: `(3,0)`, `(0,3)`, `(-3,0)`  
**Assert:** Each returns `success == true` with unique IDs. `GetSceneDiagnostics` shows 3 new entities.

### Test 2.4 — SetPlayerStats_UpdatesMaxHp
**Setup:** Default launch  
**Action:** `SetPlayerStats(endurance: 8)`  
**Assert:** Response `maxHp` > baseline HP (Endurance 5 → 8 should increase derived HP). `GetHudState` confirms new HP.

### Test 2.5 — SetPlayerWeapon_EquipsCustomWeapon
**Setup:** Default launch  
**Action:** `SetPlayerWeapon(damage: 25, accuracy: 0.90)`  
**Assert:** Response `success == true`, `damage == 25`, `accuracy == 0.90`. `GetCombatConfig` confirms player weapon updated.

---

## Class 3: DeterministicCombatTests (4 tests)

Composes all scenario commands into deterministic combat tests that replace the flaky `FullCombat_PlayerSurvives`.

### Test 3.1 — Player_Beats_SingleWeakEnemy
**Setup:** `ResetScenario`. `SpawnEnemy(id: "weak_1", x: 2, z: 0, hp: 15, weaponDamage: 1, weaponAccuracy: 0.20)`. `SetPlayerWeapon(damage: 20, accuracy: 0.95)`.  
**Action:** `TeleportPlayer(1, 0.5, 0)`. Loop `HoldKey(Space, 500)` until not InCombat (max 20 iterations).  
**Assert:** `GetGameState == "Exploring"`. `GetHudState` HP > 80% of max.

### Test 3.2 — Player_Dies_Against_OverpoweredEnemy
**Setup:** `ResetScenario`. `SpawnEnemy(id: "boss", x: 2, z: 0, hp: 999, weaponDamage: 80, weaponAccuracy: 0.99)`.  
**Action:** `TeleportPlayer(1, 0.5, 0)`. Loop `HoldKey(Space, 500)` until not InCombat (max 20 iterations).  
**Assert:** `GetGameState == "GameOver"`.

### Test 3.3 — ThreeEnemyFight_PlayerSurvives
**Setup:** `ResetScenario`. Spawn 3 enemies with tuned-down stats: `hp: 30, weaponDamage: 2, weaponAccuracy: 0.30`. `SetPlayerWeapon(damage: 15, accuracy: 0.85)`.  
**Action:** Walk into trigger range. Loop `HoldKey(Space, 2000)` until not InCombat (max 30 iterations).  
**Assert:** `GetGameState == "Exploring"`. Player has `HP > 0`.

### Test 3.4 — ArmorReducesDamage_InScenario
**Setup:** `ResetScenario`. `SpawnEnemy(id: "hitter", x: 2, z: 0, hp: 999, weaponDamage: 10, weaponAccuracy: 1.0)`. Equip leather armor via `EquipItem`. Set player HP to known value.  
**Action:** `TeleportPlayer` into range. `HoldKey(Space, 500)` for exactly 1 combat round.  
**Assert:** Player lost less HP than `10 × number_of_hits` due to armor DR.  
**Note:** This test replaces manual DamagePlayer + assertion in ArmorEffectTests.

---

## Test Execution Order

1. **ScenarioResetTests** — verifies the reset foundation
2. **ScenarioSpawnTests** — verifies entity creation on a reset base
3. **DeterministicCombatTests** — composes everything into full scenarios

---

## Dependencies

| Test class | Requires (E sub-tasks) |
|---|---|
| ScenarioResetTests | E1 |
| ScenarioSpawnTests | E1, E2, E3, E4 |
| DeterministicCombatTests | E1, E2, E3, E4, E5 |
