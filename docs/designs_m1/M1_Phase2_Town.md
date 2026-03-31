# M1 Phase 2 — Town Zone

**Goal:** A town zone with NPCs the player can talk to, a merchant, a quest giver, and a dialogue UI.

**Depends on:** Phase 1 (menus, save/load, GameState changes)

---

## 1. Town Map

### 1.1 Zone Definition

```
Zone: "Haven" (town)
Biome: Settlement
Size: 32×32 tiles (single chunk)
Radiation: 0
Enemy difficulty: None (safe zone)
Fast travel: Yes (unlocked by default)
```

### 1.2 Tile Layout

```
WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW
W..............................W
W..BBBBBB......BBBBBB.........W
W..B    B......B    B.........W
W..B    B......B    B.........W
W..BBBBBB......BBBBBB.........W
W..............................W
W........RRRRRRRR..............W
W........R      R..............W
W........R      R..............W  B = Building (wall tiles)
W........RRRRRRRR..............W  R = Road tiles
W..............................W  G = Gate/exit (zone transition)
W..BBBBBB..................BBBBW  W = Wall (map boundary)
W..B    B..................B  BW  . = Ground (walkable)
W..B    B..................B  BW  * = Spawn point
W..BBBBBB..................BBBBW
W..............................W
W............*...............GGW  ← Player spawn (*), East exit (G)
W..............................W
WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW
```

**Key locations (tile coordinates):**

| Location | Tile (X, Z) | World Pos | Purpose |
|----------|-------------|-----------|---------|
| Player Spawn | (12, 17) | (0, 0.5, 0) | New game start / respawn point |
| Elder's House | (3, 3) | (-5, 0.5, -5) | Quest giver NPC inside |
| Merchant Stall | (13, 8) | (1, 0.5, -4) | Trading NPC |
| Gate (East) | (30, 17) | (14, 0.5, 0) | Zone transition to Wasteland |
| Civilian House 1 | (3, 13) | (-5, 0.5, 3) | Ambient NPC |
| Civilian House 2 | (27, 13) | (13, 0.5, 3) | Ambient NPC |

### 1.3 Town Map Data File

**File:** `src/Oravey2.Core/Data/town_map.json`

```json
{
  "id": "town",
  "name": "Haven",
  "width": 32,
  "height": 32,
  "playerSpawn": { "x": 0.0, "y": 0.5, "z": 0.0 },
  "tiles": [ ... ],
  "npcs": [
    { "id": "elder", "x": -4.0, "z": -4.5, "dialogueTree": "elder_dialogue" },
    { "id": "merchant", "x": 1.0, "z": -3.5, "dialogueTree": "merchant_dialogue" },
    { "id": "civilian_1", "x": -4.0, "z": 3.5, "dialogueTree": "civilian_talk" },
    { "id": "civilian_2", "x": 13.0, "z": 3.5, "dialogueTree": "civilian_talk" }
  ],
  "exits": [
    { "id": "to_wasteland", "tileX": 30, "tileZ": 17, "targetZone": "wasteland", "targetSpawn": { "x": -13.0, "y": 0.5, "z": 0.0 } }
  ]
}
```

---

## 2. NPC System

### 2.1 NpcDefinition

**File:** `src/Oravey2.Core/NPC/NpcDefinition.cs`

```csharp
public record NpcDefinition(
    string Id,
    string DisplayName,
    NpcRole Role,           // QuestGiver, Merchant, Civilian
    string DialogueTreeId,
    string? ScheduleId = null);

public enum NpcRole { QuestGiver, Merchant, Civilian }
```

### 2.2 NPC Entity Spawning

Each NPC is a Stride entity with:
- **Visual:** Colored capsule (green for quest giver, blue for merchant, gray for civilians)
- **InteractionTriggerScript:** Detects player proximity (2 units), shows "Press E to talk" prompt
- **NpcComponent:** Holds `NpcDefinition` reference
- **Name label:** Floating text above head (using existing billboard approach from EnemyHpBarScript)

### 2.3 InteractionTriggerScript (SyncScript)

**File:** `src/Oravey2.Core/NPC/InteractionTriggerScript.cs`

```csharp
public class InteractionTriggerScript : SyncScript
{
    public Entity? Player { get; set; }
    public float InteractionRadius { get; set; } = 2.0f;
    public NpcDefinition? NpcDef { get; set; }

    // Set true when player is in range
    public bool PlayerInRange { get; private set; }

    // Called externally when player presses E
    public void Interact() { /* Start dialogue */ }
}
```

**Behavior:**
1. Each frame: check distance to Player entity
2. If within `InteractionRadius` → set `PlayerInRange = true`, show prompt
3. When `GameAction.Interact` (E key) pressed and `PlayerInRange`:
   - Load dialogue tree from NpcDef.DialogueTreeId
   - `GameStateManager.TransitionTo(GameState.InDialogue)`
   - `ScreenManager.Push(ScreenId.Dialogue)` with tree data

---

## 3. Dialogue UI

### 3.1 DialogueOverlayScript (SyncScript)

**File:** `src/Oravey2.Core/UI/Stride/DialogueOverlayScript.cs`

Full-screen overlay shown during `GameState.InDialogue`.

**Layout:**

```
┌──────────────────────────────────────────┐
│                                          │
│                                          │
│                                          │
│  (game world visible behind)             │
│                                          │
├──────────────────────────────────────────┤
│  [Portrait]  Speaker Name                │
│              "Dialogue text goes here.   │
│               It can be multiple lines   │
│               and wraps automatically."  │
│                                          │
│  1. Choice option A                      │
│  2. Choice option B                      │
│  3. Choice option C [Skill: Melee 20]    │
│  4. [Leave]                              │
└──────────────────────────────────────────┘
```

**Behavior:**
- Uses `DialogueProcessor.CurrentNode` to display speaker + text
- Shows choices from `DialogueProcessor.GetAvailableChoices(context)`
- Skill check choices show requirement (and pass/fail color based on player skill)
- Number keys (1-4) or click to select choice
- When choice has `NextNodeId = null` → end dialogue → `GameState.Exploring`
- Consequence actions execute on choice selection (give XP, set flag, start quest)

### 3.2 DialogueContext Wiring

```csharp
var context = new DialogueContext
{
    PlayerLevel = _playerLevel.Level,
    PlayerSkills = _playerSkills,
    Inventory = _playerInventory,
    WorldFlags = _worldState.Flags,
};
```

### 3.3 Portrait System (M1 Simple)

For M1, portraits are colored squares matching NPC role:
- Green border = quest giver
- Blue border = merchant
- Gray = civilian

No actual portrait art — just colored indicator + name text.

---

## 4. Merchant System

### 4.1 Simple Trading (M1 Scope)

No full buy/sell UI for M1. Instead, dialogue-driven:

**Merchant dialogue tree:**
```
Node: greeting
  Speaker: "Mara"
  Text: "Welcome to my stall. Need supplies?"
  Choices:
    1. "Buy a Medkit (10 caps)" → [BuyItemAction: medkit, cost: 10]
    2. "Buy a Leather Jacket (25 caps)" → [BuyItemAction: leather_jacket, cost: 25]
    3. "Sell Scrap Metal (5 caps each)" → [SellItemAction: scrap_metal, price: 5]
    4. "Never mind." → end
```

### 4.2 New Consequence Actions

| Action | Parameters | Effect |
|--------|-----------|--------|
| `BuyItemAction` | itemId, cost | Deduct caps, add item to inventory |
| `SellItemAction` | itemId, price | Remove item, add caps |
| `GiveItemAction` | itemId, count | Add item (quest reward) |

These are implementations of `IConsequenceAction` in the dialogue system.

---

## 5. Zone Transition

### 5.1 ZoneExitTriggerScript (SyncScript)

**File:** `src/Oravey2.Core/World/ZoneExitTriggerScript.cs`

Placed at gate tiles. When player walks onto a gate tile:

1. Show notification: "Entering Wasteland..."
2. Auto-save (if enabled)
3. `GameState → Loading`
4. Unload current zone entities
5. Load target zone map + entities
6. Place player at target spawn position
7. `GameState → Exploring`

**Properties:**
```csharp
public string TargetZoneId { get; set; }
public Vector3 TargetSpawnPosition { get; set; }
```

### 5.2 ZoneManager

**File:** `src/Oravey2.Core/World/ZoneManager.cs` (new)

Coordinates zone loading/unloading:

```csharp
public class ZoneManager
{
    public string CurrentZoneId { get; }
    public void LoadZone(string zoneId, Vector3 playerSpawn);
    public void UnloadCurrentZone();
}
```

- Reads zone data from JSON files
- Spawns tile map, NPCs, enemies, exits
- Registers zone in `ZoneRegistry`
- Stores current zone ID for save/load

---

## 6. New GameAction Inputs

| Action | Key | Context |
|--------|-----|---------|
| `Interact` | E | Exploring (NPC talk, door open, pickup) |
| `DialogueChoice1` | 1 | InDialogue |
| `DialogueChoice2` | 2 | InDialogue |
| `DialogueChoice3` | 3 | InDialogue |
| `DialogueChoice4` | 4 | InDialogue |

---

## 7. Files to Create / Modify

| Action | File | Notes |
|--------|------|-------|
| Create | `src/Oravey2.Core/NPC/NpcDefinition.cs` | NPC data model |
| Create | `src/Oravey2.Core/NPC/NpcComponent.cs` | Component holding NpcDefinition |
| Create | `src/Oravey2.Core/NPC/InteractionTriggerScript.cs` | Proximity + E key interaction |
| Create | `src/Oravey2.Core/UI/Stride/DialogueOverlayScript.cs` | Dialogue rendering |
| Create | `src/Oravey2.Core/World/ZoneExitTriggerScript.cs` | Zone transition trigger |
| Create | `src/Oravey2.Core/World/ZoneManager.cs` | Zone load/unload orchestration |
| Create | `src/Oravey2.Core/Dialogue/BuyItemAction.cs` | Merchant buy action |
| Create | `src/Oravey2.Core/Dialogue/SellItemAction.cs` | Merchant sell action |
| Create | `src/Oravey2.Core/Dialogue/GiveItemAction.cs` | Quest reward action |
| Create | `src/Oravey2.Core/Data/town_map.json` | Town tile layout + NPCs |
| Create | `src/Oravey2.Core/Data/town_dialogue.json` | All town NPC dialogue trees |
| Modify | `src/Oravey2.Core/Input/GameAction.cs` | Add Interact, DialogueChoice1-4 |
| Modify | `src/Oravey2.Windows/Program.cs` | Zone-based init, NPC spawning |

---

## 8. Automation Queries

| Query | Response | Purpose |
|-------|----------|---------|
| `GetNpcList` | `{ npcs: [{ id, name, role, x, z }] }` | Verify NPC spawning |
| `GetNpcInRange` | `{ npcId: "elder", inRange: true }` | Verify proximity detection |
| `InteractWithNpc` | `{ success, dialogueStarted }` | Trigger NPC interaction |
| `GetDialogueState` | `{ active, speaker, text, choices: [...] }` | Verify dialogue UI state |
| `SelectDialogueChoice` | `{ success, nextNode }` | Advance dialogue |
| `GetCurrentZone` | `{ zoneId: "town", name: "Haven" }` | Verify zone loads |
| `GetPlayerCaps` | `{ caps: 50 }` | Verify currency |

---

## 9. NPC Definitions (M1 Content)

### Elder (Quest Giver)

| Property | Value |
|----------|-------|
| Id | `elder` |
| Name | "Elder Tomas" |
| Role | QuestGiver |
| Visual | Green capsule |
| Location | Inside Elder's House (-4, 0.5, -4.5) |

### Merchant

| Property | Value |
|----------|-------|
| Id | `merchant` |
| Name | "Mara" |
| Role | Merchant |
| Visual | Blue capsule |
| Location | Market stall (1, 0.5, -3.5) |

### Civilians (×2)

| Property | Value |
|----------|-------|
| Id | `civilian_1`, `civilian_2` |
| Name | "Settler" |
| Role | Civilian |
| Visual | Gray capsule |
| Dialogue | Generic lines ("Stay safe out there.", "Watch out for radrats.") |

---

## 10. Test Plan

### Unit Tests (target: +20)

| Test | Validates |
|------|-----------|
| `NpcDefinition_CreatesCorrectly` | Record properties |
| `InteractionTrigger_DetectsProximity` | Distance check logic |
| `InteractionTrigger_OutOfRange_NoInteract` | Beyond 2 units |
| `DialogueProcessor_StartsTree` | ActiveTree set, CurrentNode = start |
| `DialogueProcessor_SelectChoice_AdvancesNode` | Tree traversal |
| `DialogueProcessor_EndDialogue_ClearsState` | IsActive = false |
| `DialogueProcessor_SkillCheck_FiltersChoices` | Unavailable choices hidden |
| `BuyItemAction_DeductsCaps` | 50 caps - 10 = 40 |
| `BuyItemAction_InsufficientCaps_Fails` | Cannot buy with 5 caps |
| `SellItemAction_AddsCaps` | 50 caps + 5 = 55 |
| `SellItemAction_NoItem_Fails` | Cannot sell what you don't have |
| `GiveItemAction_AddsToInventory` | Item added with correct count |
| `ZoneManager_LoadZone_SetsCurrentId` | Zone tracking |
| `ZoneManager_UnloadZone_ClearsEntities` | Cleanup |
| `ZoneExitTrigger_DetectsPlayer` | Gate tile detection |
| `DialogueContext_ReadsPlayerSkills` | Correct context wiring |
| `DialogueCondition_FlagCheck_True` | World flag evaluation |
| `DialogueCondition_FlagCheck_False` | Condition gates choice |
| `QuestGiver_StartsQuest_ViaDialogue` | StartQuestAction fires |
| `WorldState_SetFlag_Persists` | Flag survives save/load |

### UI Tests (target: +15)

| Test | Validates |
|------|-----------|
| `Town_HasFourNpcs` | NPC count after zone load |
| `Town_ElderExists_AtCorrectPosition` | Quest giver placement |
| `Town_MerchantExists_AtCorrectPosition` | Merchant placement |
| `WalkToElder_ShowsPrompt` | "Press E" within 2 units |
| `InteractWithElder_OpensDialogue` | Dialogue UI visible |
| `DialogueUI_ShowsSpeakerAndText` | Correct text rendered |
| `DialogueUI_ShowsChoices` | Choice buttons present |
| `SelectChoice_AdvancesDialogue` | Next node displayed |
| `EndDialogue_ReturnsToExploring` | GameState transition |
| `BuyMedkit_DeductsCaps` | 50 → 40 caps |
| `BuyMedkit_AddsToInventory` | Medkit count +1 |
| `SellScrap_AddsCaps` | Caps increase |
| `WalkToGate_TransitionsZone` | Zone changes to wasteland |
| `ZoneTransition_SavesPosition` | Auto-save on zone exit |
| `Town_CiviliansHaveDialogue` | Generic lines work |

---

## 11. Acceptance Criteria

Phase 2 is complete when:

1. Town zone loads with correct tile layout (buildings, roads, walls)
2. 4 NPCs visible with colored capsule visuals and floating names
3. Walking within 2 units of NPC shows interaction prompt
4. Pressing E opens dialogue UI with speaker name, text, and choices
5. Dialogue choices advance through the tree correctly
6. Skill-gated choices show requirement text
7. Merchant can sell items for caps (buy/sell via dialogue)
8. Walking to east gate transitions to wasteland zone
9. Zone transition auto-saves
10. All M0 + Phase 1 tests still pass
