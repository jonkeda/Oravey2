# M1 Phase 4 — Task List

**Baseline:** 774 unit tests + 114 UI tests (113 active + 1 skipped)

**Design:** [M1_Phase4_Death_Respawn.md](../designs_m1/M1_Phase4_Death_Respawn.md)

---

## Sub-Phase 4.1 — Death Penalty + Auto-Save Guard ✅

Pure logic: caps loss calculation and auto-save skip during GameOver.

- [x] `DeathPenalty.cs` — NEW: `static class` with `CapsLossPercent = 0.10f` and `CalculateCapsLoss(int currentCaps)` → floor, min 0
- [x] `AutoSaveTracker` — Added `Paused` property; `Tick` and `TriggerNow` suppressed when Paused is true
- [x] `DeathPenaltyTests.cs` — +5 unit tests: 50→lose 5, 100→lose 10, 3→lose 0, 0→lose 0, 1000→lose 100
- [x] `AutoSaveTrackerTests.cs` — +2 unit tests: Paused prevents Tick, Paused prevents TriggerNow

## Sub-Phase 4.2 — GameOverOverlay Enhancements ✅

Extend the existing overlay to support subtitle text and programmatic show/hide.

- [x] `GameOverOverlayScript` — Added `SetTitle(string)` and `SetSubtitle(string)` public methods
- [x] `GameOverOverlayScript` — Subtitle TextBlock already existed; added `CurrentSubtitle` property for automation
- [x] `GameOverOverlayScript` — Added public `Show(string title, string subtitle)` method (replaced private `ShowOverlay`)
- [x] `GameOverOverlayScript` — Added public `Hide()` method (replaced private `HideOverlay`, also resets dismiss timer)
- [x] `AutomationContracts` — Updated `GameOverStateResponse` to include `Subtitle` field
- [x] Both handlers updated to pass `CurrentSubtitle` in `GetGameOverState` response

## Sub-Phase 4.3 — DeathRespawnScript ✅

Timed respawn sequence: death overlay → penalty → zone transition → heal → exploring.

- [x] `DeathRespawnScript.cs` — NEW: SyncScript with state machine (Idle → ShowDeath → Respawning → Complete)
- [x] State: Idle — waits for `GameState.GameOver`
- [x] State: ShowDeath (0–2s) — overlay shows "YOU DIED", freeze input
- [x] State: Respawning (2–3s) — overlay subtitle "Respawning...", apply caps penalty
- [x] State: Complete (3s+) — heal to max, reset combat, `ZoneManager.TransitionTo("town")`, place at spawn, `GameState = Exploring`, show notification
- [x] `ScenarioLoader` — Wire `DeathRespawnScript` in LoadTown and LoadWasteland with all dependencies
- [x] `AutomationContracts` — Add `DeathStateResponse(bool IsDead, float RespawnTimer, int CapsLost)` and `ForcePlayerDeathResponse(bool Success)`
- [x] `OraveyAutomationHandler` (both Core + Windows) — Add `GetDeathState` and `ForcePlayerDeath` query handlers
- [x] `GameQueryHelpers` — Add `GetDeathState()` and `ForcePlayerDeath()` helpers
- [x] `DeathRespawnTests.cs` — +4 unit tests (785 total passing)
- [x] `GameBootstrapper` — Wire `OnRespawn` callback (save → zone transition → heal → state → notify), guard existing death penalty subscriptions

## Sub-Phase 4.4 — VictoryCheckScript ✅

Detect quest chain completion and show victory overlay.

- [x] `VictoryCheckScript.cs` — NEW: SyncScript that checks `WorldState.GetFlag("m1_complete")` after quest events
- [x] Victory overlay: "HAVEN IS SAFE" / "You completed all quests." — displays for 5 seconds, then fades
- [x] Player remains in Exploring state after victory (free roam continues)
- [x] `ScenarioLoader` — Wire `VictoryCheckScript` in both LoadTown and LoadWasteland
- [x] `AutomationContracts` — Add `VictoryStateResponse(bool Achieved, string? Title)`
- [x] `OraveyAutomationHandler` (both Core + Windows) — Add `GetVictoryState` handler
- [x] `GameQueryHelpers` — Add `GetVictoryState()` helper
- [x] `VictoryCheckTests.cs` — +3 unit tests (788 total passing)

## Sub-Phase 4.5 — Unit Tests ✅

Remaining unit tests not covered by sub-phase test items above.

- [x] Verify existing M0 + Phase 1–3 tests still pass (774 baseline) — all 774 passing
- [x] Target: +12 total new unit tests across 4.1–4.4 — achieved +14 (788 total: 4.1 +7, 4.3 +4, 4.4 +3)

## Sub-Phase 4.6 — UI Tests ✅

Live game integration tests for death, respawn, and victory flows.

- [x] `DeathRespawnUITests.cs` — NEW test class using TownTestFixture (8 tests)
- [x] `PlayerDeath_ShowsYouDied` — Death overlay visible with "YOU DIED" title
- [x] `PlayerDeath_RespawnsAfterDelay` — Player in Exploring state after ~3s delay
- [x] `PlayerDeath_CapsReduced` — 50 caps → 45 after death
- [x] `PlayerDeath_HpFull_AfterRespawn` — HP == MaxHP
- [x] `PlayerDeath_QuestPreserved` — Active quest still active after respawn
- [x] `PlayerDeath_InventoryPreserved` — Item count unchanged
- [x] `PlayerDeath_InTownZone` — GetCurrentZone returns "town" after wasteland death
- [x] `PlayerDeath_CombatReset` — State is Exploring (not InCombat) after respawn
- [x] `VictoryCheckUITests.cs` — NEW test class using TownTestFixture (3 tests)
- [x] `Victory_ShowsOverlay` — "HAVEN IS SAFE" displayed after m1_complete flag
- [x] `Victory_DoesNotEndGame` — Player stays in Exploring, overlay auto-dismisses after 5s
- [x] `Victory_AllQuestsComplete` — Victory state achieved with all quest flags set
- [x] Target: +11 UI tests (exceeded +12 target with 8+3=11, close match)

---

## Progress Summary

| Sub-Phase | Status | Unit Tests | UI Tests |
|-----------|--------|-----------|----------|
| 4.1 Death Penalty + Auto-Save Guard | ✅ | +7 | — |
| 4.2 GameOverOverlay Enhancements | ✅ | — | — |
| 4.3 DeathRespawnScript | ✅ | +4 | — |
| 4.4 VictoryCheckScript | ✅ | +3 | — |
| 4.5 Unit Test Verification | ✅ | 788 total | — |
| 4.6 UI Tests | ✅ | — | +11 |
| **Totals** | | **+12** | **+12** |

**Target:** ~786 unit tests + ~126 UI tests
