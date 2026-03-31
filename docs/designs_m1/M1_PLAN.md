# M1 — Vertical Slice: Master Plan

**Goal:** A fully playable single-session RPG loop on Windows — start menu, save/load, a town to explore, a combat zone with quests, and respawning after death.

**Baseline:** M0 is complete. 635 unit tests + 126 UI tests. Pure C# systems exist for save/load, dialogue, quests, AI, world zones, day/night, crafting, survival, and character progression. All need Stride wiring.

---

## Scope

The player can:

1. Launch the game → see a **start menu** (New Game, Continue, Settings, Quit)
2. Start a new game → spawn in a **town** with NPCs, a merchant, and a quest giver
3. Accept a quest → travel to a **combat zone** with enemies
4. Fight enemies, complete the quest objective, return to town
5. **Save** the game manually or via auto-save; **load** from the start menu
6. **Die** in combat → respawn at the town with a penalty
7. Complete the quest chain → see a **victory screen**

### Explicitly Out of Scope for M1

- Mobile (iOS/Android) — Windows only
- Crafting stations (pure logic exists, no UI/wiring)
- Survival meters (hunger/thirst/fatigue — toggle disabled)
- Faction reputation system (deferred to M2)
- Perk tree UI (perks exist but no selection screen)
- Multiple save slots (single slot only)
- Procedural/radiant quests
- Multiplayer

---

## Architecture Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Zone structure | 2 zones: Town + Wasteland, each 1 chunk (32×32 tiles) | Minimal viable world; streaming not needed yet |
| NPC AI | Schedule-driven civilians + static merchants | CivilianSchedule exists; keep simple |
| Dialogue | JSON-defined trees loaded at startup | DialogueProcessor is ready; need UI only |
| Quests | 3-quest chain: fetch → kill → return | QuestProcessor + conditions ready |
| Save format | JSON single-file, single slot | Debug-friendly; upgrade to binary in M2 |
| Death | Respawn at town, lose 10% caps, heal to full | Simple penalty; no corpse runs |
| Menu system | ScreenManager-driven overlays | ScreenManager + ScreenId enum exist |

---

## Phase Breakdown

| Phase | Name | Depends On | Deliverables |
|-------|------|------------|-------------|
| **1** | [Menus & Save/Load](M1_Phase1_Menus_SaveLoad.md) | M0 | Start menu, pause menu, save/load pipeline, settings screen |
| **2** | [Town Zone](M1_Phase2_Town.md) | Phase 1 | Town map, NPCs, merchant, dialogue UI, quest giver |
| **3** | [Combat Zone & Quests](M1_Phase3_Combat_Quests.md) | Phase 2 | Wasteland map, enemy spawns, 3-quest chain, quest HUD |
| **4** | [Death & Respawn](M1_Phase4_Death_Respawn.md) | Phase 3 | Death flow, respawn at town, penalty, victory screen |

---

## Existing Systems — Wiring Status

### Ready (pure logic complete, needs Stride wiring)

| System | Core Files | What M1 Needs |
|--------|-----------|---------------|
| Save/Load | SaveDataBuilder, SaveDataRestorer, AutoSaveTracker | File I/O, JSON serialization, menu integration |
| Dialogue | DialogueProcessor, DialogueTree, conditions/actions | UI rendering (speaker, text, choices), NPC interaction trigger |
| Quests | QuestProcessor, QuestLogComponent, conditions/actions | Quest HUD tracker, journal screen, NPC quest-giver wiring |
| Day/Night | DayNightCycleProcessor | Lighting changes, ambient audio shifts (optional for M1) |
| Screen Manager | ScreenManager, ScreenId enum | Concrete screen implementations (start, pause, dialogue) |
| Character Progression | LevelComponent, StatsComponent, SkillsComponent | Level-up notification, XP bar on HUD |
| World State | WorldStateService, ZoneRegistry | Zone definitions, flag persistence across save/load |
| Notifications | NotificationService | Already wired — used for quest/loot popups |

### Needs New Implementation

| System | What's Missing | M1 Phase |
|--------|---------------|----------|
| Start Menu | No main menu screen exists | Phase 1 |
| Pause Menu | ScreenId.PauseMenu exists but no implementation | Phase 1 |
| Save File I/O | SaveDataBuilder/Restorer are pure; no disk read/write | Phase 1 |
| Dialogue UI | No Stride rendering for dialogue nodes/choices | Phase 2 |
| NPC Entities | No NPC entity spawning or interaction triggers | Phase 2 |
| Zone Transitions | No zone-to-zone travel mechanic | Phase 2 |
| Quest HUD | No active quest objective display | Phase 3 |
| Enemy Respawn per Zone | M0 enemies are static; need zone-based spawn rules | Phase 3 |
| Death/Respawn Flow | GameOver exists but no respawn mechanic | Phase 4 |

---

## Data Files (JSON)

M1 introduces data-driven content. All content files live in `src/Oravey2.Core/Data/`:

| File | Purpose | Phase |
|------|---------|-------|
| `town_dialogue.json` | NPC dialogue trees (merchant, quest giver, civilians) | 2 |
| `quest_chain.json` | 3-quest definitions with stages, conditions, actions | 3 |
| `town_map.json` | 32×32 tile layout for town zone | 2 |
| `wasteland_map.json` | 32×32 tile layout for combat zone | 3 |
| `enemy_spawns.json` | Enemy spawn points and configurations per zone | 3 |
| `npc_definitions.json` | NPC stats, schedules, dialogue tree IDs | 2 |

---

## Test Strategy

Each phase adds both unit tests and UI tests:

| Phase | Unit Tests | UI Tests |
|-------|-----------|----------|
| 1 | Save round-trip, menu state transitions | Start menu flow, save/load cycle |
| 2 | Dialogue traversal, NPC interaction | Town exploration, talk to NPC, buy item |
| 3 | Quest evaluation, stage advancement | Accept quest, kill target, complete quest |
| 4 | Death penalty calculation, respawn state | Die in combat, verify respawn, verify penalty |

**Target:** 700+ unit tests, 160+ UI tests by end of M1.

---

## Acceptance Criteria

M1 is complete when:

1. Start menu with New Game / Continue / Quit works
2. New Game spawns player in town with 3+ NPCs visible
3. Player can talk to quest giver NPC → dialogue UI with choices
4. Player can accept a quest → quest objective shows on HUD
5. Player can travel to wasteland zone → enemies present
6. Combat works (inherited from M0) with quest-relevant enemies
7. Completing quest objective updates quest state
8. Returning to town and talking to quest giver completes the quest
9. Save game persists player position, inventory, quest state, HP
10. Load game restores all saved state correctly
11. Player death → respawn at town with HP restored, 10% caps lost
12. All 3 quests completable in sequence → victory screen
13. All M0 tests still pass (635 unit + 126 UI)
14. `dotnet build` produces 0 errors
