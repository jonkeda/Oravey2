# M1 Phase 3 — Task List

**Baseline:** 718 unit tests + 28 UI tests passing (8 smoke + 17 town + 3 wasteland)

**Design:** [M1_Phase3_Combat_Quests.md](../designs_m1/M1_Phase3_Combat_Quests.md)

---

## Sub-Phase 3.1 — World State Counters + Kill Tracking Foundation ✅

Foundation work: extend WorldStateService for numeric counters, add enemy tags, create KillTracker.

- [x] `WorldStateService` — Add `SetCounter(string, int)`, `GetCounter(string)`, `IncrementCounter(string)` methods + `Counters` readonly dictionary
- [x] `EnemyInfo` — Add `string? Tag` property (e.g. `"radrat"`, `"scar"`)
- [x] `EntityDiedEvent` — Enhanced with `EntityId` and `Tag` parameters
- [x] `CombatSyncScript` — Add `EventBus` property, publish `EntityDiedEvent(Id, Tag)` in `CleanupDead()`
- [x] `ScenarioLoader` — Wire `EventBus` to `CombatSyncScript` in both `LoadM0Combat` and `LoadTown`
- [x] `KillTracker.cs` — NEW: `RegisterCounter(tag, counterName)`, `RegisterFlag(tag, flagName)`, subscribes to `EntityDiedEvent`
- [x] `WorldStateServiceTests.cs` — +5 counter unit tests (get default zero, set/get, increment from zero, increment accumulates, counters collection)
- [x] `KillTrackerTests.cs` — +6 unit tests (counter increments, accumulates, flag set on boss, unregistered ignored, null tag ignored, both counter+flag same tag)

## Sub-Phase 3.2 — Enemy Spawner + Wasteland Zone ✅

Extract reusable enemy spawning, build the wasteland map, load enemies at configured positions.

- [x] `EnemySpawnPoint.cs` — NEW: record with GroupId, X, Z, Count, stats, Tag, optional RequiredQuestId/Stage
- [x] `EnemySpawner.cs` — NEW: `Spawn()` + `SpawnFromPoints()` extracted from OraveyAutomationHandler
- [x] `OraveyAutomationHandler` — Refactored `SpawnEnemy` handler to delegate to `EnemySpawner`
- [x] `EnemyInfo` — Made `public` (was `internal`) so `EnemySpawner` can return it
- [x] `WastelandMapBuilder.cs` — NEW: 32×32 wasteland tile map (walls, road, water obstacles, ruins, west gate)
- [x] `ScenarioLoader` — `LoadWasteland()`: player, tile map, HUD, 3 radrat enemies, combat system, zone exit trigger, `CombatScript` property
- [x] `ZoneManager` — "wasteland" → "wasteland" scenario mapping
- [x] Uses existing `GetCombatState` automation query for enemy positions (no separate endpoint needed)
- [x] `WastelandMapBuilderTests.cs` — 7 unit tests (dimensions, walls except gate, gate walkable, road, water non-walkable, ruins walls, ruins interior)
- [x] `EnemySpawnPointTests.cs` — 3 unit tests (defaults, tag preserved, quest-gated)
- [x] `WastelandTests.cs` — 3 UI tests (has enemies on load, enemies at correct positions, gate returns to town)

## Sub-Phase 3.3 — Quest Chain + Elder Dialogue ✅

Wire QuestProcessor, define the 3-quest chain, update Elder dialogue with flag-gated branches.

- [x] `QuestProcessor` wiring — Connect existing QuestProcessor to GameBootstrapper/ScenarioLoader
- [x] Quest definitions — Define 3 quests (q_rat_hunt, q_raider_camp, q_safe_passage) with stages/conditions/actions
- [x] `StartQuestAction` — Wire to dialogue consequence system (may already exist from M0 core)
- [x] `TownDialogueTrees` — Rewrite Elder tree with `FlagCondition` gates: initial → quest 1 offer → quest 1 active → quest 1 done → quest 2 offer → quest 2 active → quest 3 report
- [x] `KillTracker` wiring — Subscribe to enemy death events in `CombatSyncScript.CleanupDead()`, publish `EntityDiedEvent`
- [x] Boss enemy "Scar" — Conditional spawn at raider camp (only when q_raider_camp active), higher stats, named entity
- [x] Quest 3 auto-start — `q_raider_camp` completion triggers `q_safe_passage` start
- [x] `AutomationContracts` — `ActiveQuestsResponse`, `WorldFlagResponse`, `SetWorldFlagRequest`
- [x] `OraveyAutomationHandler` — `GetActiveQuests`, `GetWorldFlag`, `SetWorldFlag` handlers (both Core + Windows)
- [x] `GameQueryHelpers` — `GetActiveQuests()`, `GetWorldFlag()`, `SetWorldFlag()` helpers
- [x] `QuestChainTests.cs` — Unit tests: start quest sets active, evaluate advances stage, complete awards XP, quest 2 requires quest 1 done, quest 3 auto-starts, elder dialogue branches per state, m1_complete flag set
- [x] UI tests: accept quest 1 from Elder, kill counter increments, quest 1 complete after 3 kills, quest 1 reward on return, quest 2 available after quest 1, quest 2 accept sets active, quest 3 report to Elder completes

## Sub-Phase 3.4 — Quest HUD Tracker + Journal ✅

Quest objective HUD widget and full quest journal overlay.

- [x] `QuestTrackerScript.cs` — NEW: SyncScript showing active quest objective (top-right), live counter updates, fades when no quest
- [x] `QuestJournalScript.cs` — NEW: Full-screen overlay (J key), lists active/completed quests with details
- [x] `GameAction` — Add `OpenJournal`
- [x] `KeyboardMouseInputProvider` — Map J key to `GameAction.OpenJournal`
- [x] `ScenarioLoader` — Wire QuestTrackerScript and QuestJournalScript in LoadTown/LoadWasteland
- [x] `AutomationContracts` — `QuestTrackerStateResponse`, `QuestJournalStateResponse`, `QuestJournalEntryDto`
- [x] `OraveyAutomationHandler` — `GetQuestTrackerState` + `GetQuestJournalState` handlers (both Core + Windows)
- [x] `GameQueryHelpers` — `GetQuestTrackerState()` + `GetQuestJournalState()` helpers
- [x] `QuestTrackerTests.cs` — 7 unit tests: tracked quest ID, no active quest, completed not tracked, counter progress, zero kills, no-counter empty, stage description
- [x] `QuestJournalTests.cs` — 8 unit tests: empty lists, active quest in active list, completed in completed list, shows objective, counter progress, null objective/progress for completed, multiple quests categorization, order preserved
- [x] UI tests (QuestTrackerJournalTests.cs): tracker hidden by default, tracker visible after accept, tracker shows progress, tracker hidden after complete, journal hidden by default, journal toggles with J, journal closes with Esc, journal shows active quest, journal shows completed quest

## Sub-Phase 3.5 — Integration + Polish ✅

End-to-end quest chain, save/load preservation, enemy respawn behavior.

- [x] Enemy respawn — Enemies re-spawn when zone is re-entered (LoadWasteland creates fresh spawn points each time)
- [x] Boss no-respawn — Scar does NOT respawn after `scar_killed` flag set (only spawns when q_raider_camp Active)
- [x] Save/load quest state — SaveData.WorldCounters added, SaveDataBuilder.WithQuestStates accepts counters, QuestLogComponent.RestoreFromSave + WorldStateService.RestoreFromSave added, both handlers wire save/load of quest states + world flags + counters
- [x] Full quest chain E2E — UI test: accept Q1 → kill 3 rats → report → accept Q2 → kill Scar → Q3 auto-starts → report → m1_complete
- [x] `QuestStatePersistenceTests.cs` — 6 unit tests: builder captures quest status, restorer exposes state, QuestLog restore, WorldState restore, round-trip, completed quest no stage
- [x] `EnemyRespawnTests.cs` — 5 unit tests: radrats always present, scar spawns when active, scar absent when not started, scar absent when completed, fresh list each call
- [x] UI tests (QuestIntegrationTests.cs): enemies respawn on zone re-enter, boss does not respawn after kill, save/load preserves quest state, full quest chain E2E

---

## Progress Summary

| Sub-Phase | Status | Unit Tests | UI Tests |
|-----------|--------|-----------|----------|
| 3.1 World State + Kill Tracking | ✅ Done | +11 | — |
| 3.2 Enemy Spawner + Wasteland | ✅ Done | +10 | +3 |
| 3.3 Quest Chain + Dialogue | ✅ Done | +9 | +7 |
| 3.4 Quest HUD + Journal | ✅ Done | +15 | +9 |
| 3.5 Integration + Polish | ✅ Done | +11 | +4 |
| **Totals** | **✅ All Done** | **+56** | **+23** |

**Actual:** 774 unit tests + 23 new UI tests
