# M1 Phase 3 — Combat Zone & Quests

**Goal:** A wasteland zone with enemies, a 3-quest chain, quest HUD tracking, and zone-based enemy spawning.

**Depends on:** Phase 2 (town, NPCs, dialogue, zone transitions)

---

## 1. Wasteland Zone

### 1.1 Zone Definition

```
Zone: "Scorched Outskirts" (wasteland)
Biome: Wasteland
Size: 32×32 tiles (single chunk)
Radiation: Low (ambient, no mechanical effect in M1)
Enemy difficulty: Tier 1
Fast travel: Unlockable (after first visit)
```

### 1.2 Tile Layout

```
WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW
W..............................W
W...RR.........................W
W...RR.........................W
W...RR.........~~~~............W
W...RR.........~~~~............W  ~ = Water/rubble (non-walkable)
W...RR.........................W  R = Road (walkable)
W...RR.........................W  B = Ruins (wall tiles)
W...RR..............BBBB.......W  W = Wall (boundary)
W...RR..............B  B.......W  . = Ground (walkable)
W...RR..............B  B.......W  G = Gate/exit (to town)
W...RR..............BBBB.......W  E = Enemy spawn points
W...RR.........................W  C = Camp location (quest target)
W...RR.........................W
W..............................W
W..........E......E............W
W..............................W
GG.........E.........C.........W  ← West exit (G) to Town
W..............................W
WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW
```

**Key locations:**

| Location | Tile (X, Z) | World Pos | Purpose |
|----------|-------------|-----------|---------|
| West Gate | (0, 17) | (-14, 0.5, 0) | Return to town |
| Enemy Spawn 1 | (10, 15) | (-2, 0.5, -2) | Radrat group |
| Enemy Spawn 2 | (14, 15) | (2, 0.5, -2) | Radrat group |
| Enemy Spawn 3 | (10, 17) | (-2, 0.5, 0) | Radrat group |
| Raider Camp | (22, 17) | (10, 0.5, 0) | Quest 2 target (ruins building) |
| Ruins | (20, 8) | (8, 0.5, -4) | Exploration / loot |

### 1.3 Wasteland Map Data File

**File:** `src/Oravey2.Core/Data/wasteland_map.json`

Same format as town_map.json but with enemy spawn points instead of NPCs.

---

## 2. Enemy Spawning System

### 2.1 EnemySpawnPoint

**File:** `src/Oravey2.Core/Combat/EnemySpawnPoint.cs`

```csharp
public record EnemySpawnPoint(
    string GroupId,          // "radrats_south", "raider_camp"
    double X, double Z,
    int Count,               // How many enemies in this group
    int Endurance,
    int Luck,
    int WeaponDamage,
    float WeaponAccuracy,
    string? RequiredQuestId = null,   // Only spawn if quest active
    string? RequiredQuestStage = null);
```

### 2.2 Zone-Based Spawning

When the wasteland zone loads:

1. Read spawn points from `wasteland_map.json`
2. For each spawn point:
   - Check `RequiredQuestId` — only spawn if quest/stage matches
   - Use existing `SpawnEnemy` logic from `OraveyAutomationHandler` (extract to reusable `EnemySpawner` class)
   - Place enemies at configured positions
3. Enemies respawn when zone is re-entered (unless permanently killed by quest flag)

### 2.3 EnemySpawner (Extracted from handler)

**File:** `src/Oravey2.Core/Combat/EnemySpawner.cs`

Extract the `SpawnEnemy` logic from `OraveyAutomationHandler` into a reusable service:

```csharp
public class EnemySpawner
{
    public EnemyInfo SpawnEnemy(
        Scene scene, Game game,
        string id, float x, float z,
        int endurance, int luck,
        int weaponDamage, float weaponAccuracy,
        CombatSyncScript combatScript);
}
```

Both `OraveyAutomationHandler.SpawnEnemy()` and the zone loader use this shared service.

---

## 3. Quest Chain

### 3.1 Overview

Three quests form a linear chain. Each must be completed before the next is offered.

| # | Quest ID | Title | Type | Objective |
|---|----------|-------|------|-----------|
| 1 | `q_rat_hunt` | "Rat Problem" | Side | Kill 3 radrats in the wasteland |
| 2 | `q_raider_camp` | "Clear the Camp" | Side | Kill the raider leader at the camp ruins |
| 3 | `q_safe_passage` | "Safe Passage" | Main | Return to Elder Tomas and report |

### 3.2 Quest 1: Rat Problem

**Given by:** Elder Tomas (dialogue)
**Trigger:** Talk to Elder, choose "Any work?"

**Dialogue flow:**
```
Elder: "Radrats have been attacking scavengers near the road.
        Kill three of them and I'll make it worth your while."
  1. "I'll handle it." → [StartQuestAction: q_rat_hunt] → end
  2. "Not now." → end
```

**Quest Definition:**
```json
{
  "id": "q_rat_hunt",
  "title": "Rat Problem",
  "description": "Kill 3 radrats in the Scorched Outskirts.",
  "type": "Side",
  "xpReward": 50,
  "stages": {
    "kill_rats": {
      "description": "Kill 3 radrats (0/3)",
      "conditions": [{ "type": "WorldFlag", "flag": "rats_killed", "minValue": 3 }],
      "onComplete": [{ "type": "SetFlag", "flag": "q_rat_hunt_done", "value": true }],
      "nextStage": null
    }
  },
  "firstStage": "kill_rats"
}
```

**Kill tracking:**
- When an enemy dies in the wasteland zone, increment `WorldStateService.SetFlag("rats_killed", count + 1)`
- `CombatSyncScript.CleanupDead()` publishes `EntityDiedEvent` → listener increments flag
- Quest evaluates `rats_killed >= 3` → stage complete → quest complete

**Reward:** 50 XP, 15 caps (via dialogue when reporting back)

### 3.3 Quest 2: Clear the Camp

**Given by:** Elder Tomas (after Quest 1 complete)
**Trigger:** Talk to Elder, new dialogue option appears

**Dialogue flow:**
```
Elder: "Good work with the rats. But there's a bigger problem.
        Raiders set up camp in the eastern ruins. Their leader
        is a nasty piece of work called Scar. Take him out."
  1. "Consider it done." → [StartQuestAction: q_raider_camp] → end
  2. "I need to prepare first." → end
```

**Quest Definition:**
```json
{
  "id": "q_raider_camp",
  "title": "Clear the Camp",
  "description": "Kill the raider leader Scar at the eastern ruins.",
  "type": "Side",
  "xpReward": 100,
  "stages": {
    "kill_scar": {
      "description": "Kill the raider leader Scar",
      "conditions": [{ "type": "WorldFlag", "flag": "scar_killed", "value": true }],
      "onComplete": [{ "type": "SetFlag", "flag": "q_raider_camp_done", "value": true }],
      "nextStage": null
    }
  },
  "firstStage": "kill_scar"
}
```

**Scar (Boss Enemy):**
- Spawns at raider camp ruins only when quest is active
- Higher stats: Endurance 3, Luck 5, Weapon Damage 8, Accuracy 0.65
- Named entity ("Scar") — not generic "enemy_N"
- On death: set `WorldFlag("scar_killed", true)`

### 3.4 Quest 3: Safe Passage

**Given by:** Automatically started when Quest 2 completes (via `OnComplete` action)

**Quest Definition:**
```json
{
  "id": "q_safe_passage",
  "title": "Safe Passage",
  "description": "Return to Elder Tomas and report your success.",
  "type": "Main",
  "xpReward": 150,
  "stages": {
    "report_back": {
      "description": "Report to Elder Tomas in Haven",
      "conditions": [{ "type": "WorldFlag", "flag": "reported_to_elder", "value": true }],
      "onComplete": [
        { "type": "GiveXP", "amount": 150 },
        { "type": "SetFlag", "flag": "m1_complete", "value": true }
      ],
      "nextStage": null
    }
  },
  "firstStage": "report_back"
}
```

**Completion:**
- Return to town, talk to Elder
- New dialogue option: "The camp is clear."
- Elder: "You've done it. Haven is safer because of you."
  → `[SetFlag: reported_to_elder] [GiveItemAction: 50 caps]`
- Quest completes → `m1_complete` flag set → triggers victory screen (Phase 4)

---

## 4. Quest HUD

### 4.1 QuestTrackerScript (SyncScript)

**File:** `src/Oravey2.Core/UI/Stride/QuestTrackerScript.cs`

Persistent HUD element showing current active quest objective.

**Layout (top-right corner):**
```
┌─────────────────────┐
│  ⬦ Rat Problem      │
│    Kill 3 radrats    │
│    (1/3)             │
└─────────────────────┘
```

**Behavior:**
- Reads `QuestLogComponent` for active quests
- Shows first active quest's current stage description
- Updates counter in real-time (listens to `WorldStateService` flag changes)
- Fades out when no active quest
- Flashes green briefly when objective updates

### 4.2 Quest Journal Screen

**File:** `src/Oravey2.Core/UI/Stride/QuestJournalScript.cs`

Full-screen overlay accessible via `J` key (`GameAction.OpenJournal`).

**Layout:**
```
┌──────────┬───────────────────────────┐
│ Quests   │                           │
│          │  Rat Problem              │
│ ● Active │  "Kill 3 radrats in the   │
│  > Rat   │   Scorched Outskirts."    │
│    Problem│                          │
│          │  Objective: Kill 3 (1/3)  │
│ ○ Done   │  Reward: 50 XP           │
│          │                           │
│          │                           │
│          │             [Close: J/Esc]│
└──────────┴───────────────────────────┘
```

---

## 5. Kill Tracking System

### 5.1 KillTracker

**File:** `src/Oravey2.Core/Combat/KillTracker.cs`

Listens to `EntityDiedEvent` and updates world flags for quest tracking.

```csharp
public class KillTracker
{
    // Register: when enemy with tag X dies, increment flag Y
    public void Register(string enemyTag, string flagName);

    // Called by event handler when any entity dies
    public void OnEntityDied(string entityId);
}
```

**Wiring:**
- Radrat enemies tagged with `"radrat"` → increments `"rats_killed"` flag
- Scar tagged with `"scar"` → sets `"scar_killed"` flag

### 5.2 Enemy Tags

Add `Tag` property to `EnemyInfo`:
```csharp
public class EnemyInfo
{
    // ... existing properties ...
    public string? Tag { get; set; }  // "radrat", "scar", etc.
}
```

---

## 6. World Flag Extensions

### 6.1 Numeric Flags

`WorldStateService` currently stores `Dictionary<string, bool>`. For kill counters, extend to support int values:

```csharp
public void SetFlag(string name, bool value);
public bool GetFlag(string name);

// New:
public void SetCounter(string name, int value);
public int GetCounter(string name);
public void IncrementCounter(string name);
```

Quest conditions check counters: `rats_killed >= 3`.

---

## 7. Elder Dialogue — Full Tree

### 7.1 Dialogue States

The Elder's dialogue changes based on quest state. Use `FlagCondition` to gate sections:

```
Node: greeting
  [If no active quest and !q_rat_hunt_done]:
    "Welcome, stranger. Haven isn't much, but it's ours."
    Choices:
      1. "Any work?" → quest_offer_1
      2. "Just passing through." → end

  [If q_rat_hunt active]:
    "How's the rat hunt going?"
    Choices:
      1. "Still working on it." → end
      2. [If rats_killed >= 3] "They're dead." → quest_1_complete

  [If q_rat_hunt_done and !q_raider_camp started]:
    "Good work with those rats. I've got another job..."
    Choices:
      1. "What is it?" → quest_offer_2
      2. "Not yet." → end

  [If q_raider_camp active and !scar_killed]:
    "Scar still breathing?"
    Choices:
      1. "Working on it." → end

  [If scar_killed and q_safe_passage active]:
    "Well? Is it done?"
    Choices:
      1. "The camp is clear." → quest_3_complete

Node: quest_1_complete
  "Excellent. Here's your reward."
  → [GiveXP: 50] [GiveItemAction: 15 caps] [SetFlag: q_rat_hunt_done]
  → greeting (loop back with new state)

Node: quest_offer_2
  "Raiders at the eastern ruins. Leader called Scar. Take him out."
  Choices:
    1. "Consider it done." → [StartQuest: q_raider_camp] → end
    2. "I need to prepare." → end

Node: quest_3_complete
  "You've made Haven safe. We won't forget this."
  → [GiveXP: 150] [GiveItemAction: 50 caps] [SetFlag: m1_complete, reported_to_elder]
  → end
```

This is implemented as a single `DialogueTree` with `FlagCondition` gates on choices.

---

## 8. Files to Create / Modify

| Action | File | Notes |
|--------|------|-------|
| Create | `src/Oravey2.Core/Combat/EnemySpawnPoint.cs` | Spawn point data |
| Create | `src/Oravey2.Core/Combat/EnemySpawner.cs` | Reusable spawn logic |
| Create | `src/Oravey2.Core/Combat/KillTracker.cs` | Death → flag updates |
| Create | `src/Oravey2.Core/UI/Stride/QuestTrackerScript.cs` | Quest HUD widget |
| Create | `src/Oravey2.Core/UI/Stride/QuestJournalScript.cs` | Full quest journal |
| Create | `src/Oravey2.Core/Data/wasteland_map.json` | Wasteland zone data |
| Create | `src/Oravey2.Core/Data/quest_chain.json` | 3 quest definitions |
| Create | `src/Oravey2.Core/Data/enemy_spawns.json` | Spawn configurations |
| Modify | `src/Oravey2.Core/Combat/CombatSyncScript.cs` | EnemyInfo.Tag, death events |
| Modify | `src/Oravey2.Core/World/WorldStateService.cs` | Add counter methods |
| Modify | `src/Oravey2.Core/Input/GameAction.cs` | Add OpenJournal (J) |
| Modify | `src/Oravey2.Windows/Program.cs` | Register KillTracker, QuestProcessor |

---

## 9. Automation Queries

| Query | Response | Purpose |
|-------|----------|---------|
| `GetActiveQuests` | `{ quests: [{ id, title, stage, description }] }` | Verify quest state |
| `GetQuestTrackerState` | `{ visible, questId, objective, progress }` | Verify HUD widget |
| `GetWorldFlag` | `{ flag, value }` | Check flag state |
| `SetWorldFlag` | `{ success }` | Force flag for testing |
| `GetEnemySpawns` | `{ spawns: [{ id, x, z, alive }] }` | Verify zone enemies |

---

## 10. Test Plan

### Unit Tests (target: +25)

| Test | Validates |
|------|-----------|
| `QuestProcessor_StartQuest_SetsActive` | Quest status = Active |
| `QuestProcessor_EvaluateQuest_AdvancesStage` | Stage progression |
| `QuestProcessor_CompleteQuest_AwardsXP` | XP reward fires |
| `QuestChain_Quest2_RequiresQuest1Complete` | Dependency gating |
| `QuestChain_Quest3_AutoStartsAfterQuest2` | Chain linking |
| `KillTracker_IncrementCounter_OnDeath` | radrat → rats_killed++ |
| `KillTracker_SetFlag_OnBossDeath` | scar → scar_killed = true |
| `WorldState_IncrementCounter_FromZero` | 0 → 1 |
| `WorldState_IncrementCounter_Accumulates` | 1 → 2 → 3 |
| `WorldState_GetCounter_DefaultZero` | Unset counter = 0 |
| `EnemySpawnPoint_ConditionalSpawn_QuestActive` | Spawns when quest active |
| `EnemySpawnPoint_ConditionalSpawn_QuestInactive` | Skips when no quest |
| `EnemySpawner_CreatesEntity` | Entity in scene |
| `EnemySpawner_SetsStats` | Correct HP/damage |
| `QuestCondition_FlagCheck_Counter` | rats_killed >= 3 |
| `QuestCondition_FlagCheck_Boolean` | scar_killed == true |
| `ElderDialogue_InitialState_OffersQuest1` | Correct dialogue branch |
| `ElderDialogue_Quest1Active_ShowsProgress` | "Still hunting?" branch |
| `ElderDialogue_Quest1Done_OffersQuest2` | New option appears |
| `ElderDialogue_Quest3Complete_SetsFlag` | m1_complete = true |
| `QuestTracker_ShowsActiveObjective` | HUD displays current stage |
| `QuestTracker_UpdatesOnFlagChange` | Counter refreshes |
| `QuestJournal_ListsActiveQuests` | Active quest visible |
| `QuestJournal_ListsCompletedQuests` | Done section populated |
| `EnemyTag_TrackedOnDeath` | Tag → correct flag |

### UI Tests (target: +20)

| Test | Validates |
|------|-----------|
| `Wasteland_HasEnemies_OnLoad` | Enemies spawned |
| `Wasteland_EnemiesAt_CorrectPositions` | Spawn positions match config |
| `Wasteland_GateReturnsToTown` | Zone transition works both ways |
| `AcceptQuest1_FromElder` | Dialogue → quest active |
| `Quest1_KillCounter_Increments` | Kill radrat → counter +1 |
| `Quest1_Complete_After3Kills` | Quest status = Completed |
| `Quest1_Reward_OnReturn` | 50 XP + 15 caps given |
| `Quest2_Available_AfterQuest1` | New dialogue option |
| `Quest2_ScarSpawns_WhenActive` | Boss appears at camp |
| `Quest2_ScarKill_CompletesQuest` | scar_killed → quest done |
| `Quest3_AutoStarts_AfterQuest2` | Active without dialogue |
| `Quest3_ReportToElder_Completes` | Dialogue → m1_complete |
| `QuestTracker_ShowsOnHUD` | Top-right widget visible |
| `QuestTracker_Updates_OnKill` | "(1/3)" → "(2/3)" |
| `QuestJournal_OpensWithJ` | J key → journal screen |
| `QuestJournal_ShowsActiveQuest` | Quest listed |
| `QuestJournal_ClosesWithEsc` | Escape → back to game |
| `EnemiesRespawn_OnZoneReenter` | Leave and return → enemies back |
| `BossDoesNotRespawn_AfterKill` | scar_killed flag prevents |
| `SaveLoad_PreservesQuestState` | Save mid-quest, load, verify |

---

## 11. Acceptance Criteria

Phase 3 is complete when:

1. Wasteland zone loads with enemies at configured positions
2. Enemies respawn when zone is re-entered (unless quest-flagged)
3. Quest 1 obtainable from Elder Tomas dialogue
4. Killing 3 radrats completes Quest 1
5. Quest 2 offered after Quest 1 complete — boss enemy "Scar" spawns
6. Killing Scar completes Quest 2, auto-starts Quest 3
7. Returning to Elder and talking completes Quest 3
8. Quest tracker HUD shows active objective with live progress
9. Quest journal (J key) shows active and completed quests
10. Kill tracking correctly updates world flags
11. All M0 + Phase 1 + Phase 2 tests still pass
