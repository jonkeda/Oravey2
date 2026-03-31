# M1 Phase 3 — Task List

**Baseline:** 697 unit tests + 25 UI tests passing (8 smoke + 17 town)

**Design:** [M1_Phase3_Combat_Quests.md](../designs_m1/M1_Phase3_Combat_Quests.md)

---

## Sub-Phase 3.1 — World State Counters + Kill Tracking Foundation ⬜

Foundation work: extend WorldStateService for numeric counters, add enemy tags, create KillTracker.

- [ ] `WorldStateService` — Add `SetCounter(string, int)`, `GetCounter(string)`, `IncrementCounter(string)` methods
- [ ] `EnemyInfo` — Add `string? Tag` property (e.g. `"radrat"`, `"scar"`)
- [ ] `KillTracker.cs` — NEW: `Register(enemyTag, flagName)`, `OnEntityDied(entityId)` increments counters via WorldStateService
- [ ] `WorldStateCounterTests.cs` — Unit tests: get default zero, set/get, increment from zero, increment accumulates
- [ ] `KillTrackerTests.cs` — Unit tests: register tag, on-death increments flag, on-death sets boolean flag (boss), unregistered tag ignored

## Sub-Phase 3.2 — Enemy Spawner + Wasteland Zone ⬜

Extract reusable enemy spawning, build the wasteland map, load enemies at configured positions.

- [ ] `EnemySpawnPoint.cs` — NEW: record with GroupId, X, Z, Count, stats, optional RequiredQuestId/Stage
- [ ] `EnemySpawner.cs` — NEW: extract `SpawnEnemy` logic from `OraveyAutomationHandler` into reusable service
- [ ] `OraveyAutomationHandler` — Refactor `SpawnEnemy` handler to delegate to `EnemySpawner`
- [ ] `WastelandMapBuilder.cs` — NEW: 32×32 wasteland tile map (walls, road, rubble, ruins, gate)
- [ ] `ScenarioLoader` — Add `LoadWasteland()` method: player, tile map, HUD, enemies at spawn points, gate back to town
- [ ] `ZoneManager` — Update zone ID → scenario mapping: "wasteland" → calls `LoadWasteland()` (not `m0_combat`)
- [ ] `AutomationContracts` — `EnemySpawnDto`, `EnemySpawnListResponse`
- [ ] `OraveyAutomationHandler` — `GetEnemySpawns` query handler
- [ ] `GameQueryHelpers` — `GetEnemySpawns()` helper
- [ ] `EnemySpawnerTests.cs` — Unit tests: creates entity, sets stats, applies tag
- [ ] `WastelandMapBuilderTests.cs` — Unit tests: dimensions, walls, rubble non-walkable, gate walkable, road tiles
- [ ] UI tests: wasteland has enemies on load, enemies at correct positions, gate returns to town

## Sub-Phase 3.3 — Quest Chain + Elder Dialogue ⬜

Wire QuestProcessor, define the 3-quest chain, update Elder dialogue with flag-gated branches.

- [ ] `QuestProcessor` wiring — Connect existing QuestProcessor to GameBootstrapper/ScenarioLoader
- [ ] Quest definitions — Define 3 quests (q_rat_hunt, q_raider_camp, q_safe_passage) with stages/conditions/actions
- [ ] `StartQuestAction` — Wire to dialogue consequence system (may already exist from M0 core)
- [ ] `TownDialogueTrees` — Rewrite Elder tree with `FlagCondition` gates: initial → quest 1 offer → quest 1 active → quest 1 done → quest 2 offer → quest 2 active → quest 3 report
- [ ] `KillTracker` wiring — Subscribe to enemy death events in `CombatSyncScript.CleanupDead()`, publish `EntityDiedEvent`
- [ ] Boss enemy "Scar" — Conditional spawn at raider camp (only when q_raider_camp active), higher stats, named entity
- [ ] Quest 3 auto-start — `q_raider_camp` completion triggers `q_safe_passage` start
- [ ] `AutomationContracts` — `ActiveQuestsResponse`, `WorldFlagResponse`, `SetWorldFlagRequest`
- [ ] `OraveyAutomationHandler` — `GetActiveQuests`, `GetWorldFlag`, `SetWorldFlag` handlers
- [ ] `GameQueryHelpers` — `GetActiveQuests()`, `GetWorldFlag()`, `SetWorldFlag()` helpers
- [ ] `QuestChainTests.cs` — Unit tests: start quest sets active, evaluate advances stage, complete awards XP, quest 2 requires quest 1 done, quest 3 auto-starts, elder dialogue branches per state, m1_complete flag set
- [ ] UI tests: accept quest 1 from Elder, kill counter increments, quest 1 complete after 3 kills, quest 1 reward on return, quest 2 available after quest 1, Scar spawns when quest active, Scar kill completes quest, quest 3 report to Elder completes

## Sub-Phase 3.4 — Quest HUD Tracker + Journal ⬜

Quest objective HUD widget and full quest journal overlay.

- [ ] `QuestTrackerScript.cs` — NEW: SyncScript showing active quest objective (top-right), live counter updates, fades when no quest
- [ ] `QuestJournalScript.cs` — NEW: Full-screen overlay (J key), lists active/completed quests with details
- [ ] `GameAction` — Add `OpenJournal`
- [ ] `KeyboardMouseInputProvider` — Map J key to `GameAction.OpenJournal`
- [ ] `ScenarioLoader` — Wire QuestTrackerScript and QuestJournalScript in LoadTown/LoadWasteland
- [ ] `AutomationContracts` — `QuestTrackerStateResponse`
- [ ] `OraveyAutomationHandler` — `GetQuestTrackerState` handler
- [ ] `GameQueryHelpers` — `GetQuestTrackerState()` helper
- [ ] `QuestTrackerTests.cs` — Unit tests: shows active objective, updates on flag change, hidden when no quest
- [ ] `QuestJournalTests.cs` — Unit tests: lists active quests, lists completed quests
- [ ] UI tests: tracker shows on HUD, tracker updates on kill, journal opens with J, journal shows active quest, journal closes with Esc

## Sub-Phase 3.5 — Integration + Polish ⬜

End-to-end quest chain, save/load preservation, enemy respawn behavior.

- [ ] Enemy respawn — Enemies re-spawn when zone is re-entered (not permanent kill unless quest-flagged)
- [ ] Boss no-respawn — Scar does NOT respawn after `scar_killed` flag is set
- [ ] Save/load quest state — WorldStateService flags + QuestLogComponent persist through save/load cycle
- [ ] Full quest chain E2E — Automate: accept quest 1 → kill 3 rats → return → accept quest 2 → kill Scar → auto-start quest 3 → return → complete
- [ ] UI tests: enemies respawn on zone re-enter, boss does not respawn after kill, save/load preserves quest state, full quest chain E2E

---

## Progress Summary

| Sub-Phase | Status | Unit Tests | UI Tests |
|-----------|--------|-----------|----------|
| 3.1 World State + Kill Tracking | ⬜ | ~8 | — |
| 3.2 Enemy Spawner + Wasteland | ⬜ | ~8 | +3 |
| 3.3 Quest Chain + Dialogue | ⬜ | ~12 | +8 |
| 3.4 Quest HUD + Journal | ⬜ | ~5 | +5 |
| 3.5 Integration + Polish | ⬜ | — | +4 |
| **Totals** | | **~33** | **+20** |

**Target:** ~730 unit tests + ~45 UI tests
