# M1 Phase 2 — Sub-Phase Plan

**Parent:** M1_Phase2_Town.md
**Strategy:** Town loads first, then wire one feature at a time. Each sub-phase is testable.

---

## Existing Infrastructure (already in Core)

| System | Status | Notes |
|--------|--------|-------|
| DialogueTree / DialogueNode / DialogueChoice | ✅ | Records in `Dialogue/` |
| DialogueProcessor | ✅ | StartDialogue, SelectChoice, EndDialogue |
| DialogueContext | ✅ | Skills, Inventory, WorldState, Level, EventBus |
| IDialogueCondition | ✅ | SkillCheck, Flag, Item, Level conditions |
| IConsequenceAction | ✅ | SetFlag, GiveXP, StartQuest actions |
| GameState.InDialogue | ✅ | Enum value exists |
| GameAction.Interact | ✅ | Mapped to F key |
| WorldStateService | ✅ | SetFlag / GetFlag |
| InventoryComponent.Caps | ✅ | get/set, starts at 50 |
| ZoneDefinition / ZoneRegistry | ✅ | Chunk-based zone metadata |
| TileMapData / TileType | ✅ | Ground, Road, Wall, etc. + CreateDefault() |
| SkillsComponent | ✅ | GetEffective(SkillType) |
| DialogueProcessorTests | ✅ | Existing test coverage |
| DialogueConditionTests | ✅ | Existing test coverage |

---

## Sub-Phase 2.1 — Bare Town Scenario (Loadable + Walkable)

**Scope:** Add a `"town"` scenario to ScenarioLoader that creates the town tile map, player at spawn, camera, HUD, and notifications. No NPCs yet. Testable: town loads, player can walk, automation queries work.

### New Files

| File | Contents |
|------|----------|
| `src/Oravey2.Core/World/TownMapBuilder.cs` | Static method `CreateTownMap()` → `TileMapData` with the 32×32 layout from the design (walls, buildings, roads, ground, gate tiles) |
| `tests/Oravey2.Tests/World/TownMapBuilderTests.cs` | Map shape tests |

### Modify

| File | Change |
|------|--------|
| `src/Oravey2.Core/Bootstrap/ScenarioLoader.cs` | Add `case "town": LoadTown(...)` — reuses `CreatePlayer()`, adds town tile map, HUD, notifications, no enemies |

### TownMapBuilder

```csharp
public static class TownMapBuilder
{
    public static TileMapData CreateTownMap()
    {
        var map = new TileMapData(32, 32);
        // Border walls (W)
        // Building blocks (Wall tiles) at design coords
        // Road strip in center (Road tiles)
        // Gate tiles at (30,17) and (30,18) — Ground for now
        // Everything else = Ground
        return map;
    }
}
```

### LoadTown (ScenarioLoader)

Follows same pattern as existing `LoadEmpty()`:
1. `CreatePlayer()` at spawn (0, 0.5, 0)
2. `TownMapBuilder.CreateTownMap()` for tile map
3. HUD entity (reuse `HudSyncScript`)
4. Notification feed (reuse `NotificationFeedScript`)
5. No enemies, no combat manager needed for town
6. Expose `PlayerInventory`, `PlayerHealth`, etc. via existing properties

### Unit Tests (+8)

| Test | Validates |
|------|-----------|
| `TownMap_Is32x32` | Width and Height |
| `TownMap_Border_IsWall` | All edge tiles are `TileType.Wall` |
| `TownMap_PlayerSpawn_IsWalkable` | Tile at (12,17) is Ground |
| `TownMap_EldersHouse_HasWalls` | Tiles at building perimeter are Wall |
| `TownMap_MerchantStall_HasRoad` | Road tiles in center strip |
| `TownMap_GateTile_IsGround` | (30,17) is walkable |
| `TownMap_InsideBuilding_IsGround` | Interior tiles are walkable |
| `TownMap_CenterArea_AllWalkable` | Spot-check open ground |

### Acceptance

- `dotnet build` passes
- 8 new unit tests pass (660 total)
- `--scenario town` loads without crash
- Existing tests unaffected

---

## Sub-Phase 2.2 — NPC Data Model + Spawning NPCs in Town

**Scope:** Create NPC data types, spawn 4 NPC entities with colored capsules and floating name labels. Testable: NPCs exist at correct positions, automation can query them.

### New Files

| File | Contents |
|------|----------|
| `src/Oravey2.Core/NPC/NpcDefinition.cs` | `NpcRole` enum + `NpcDefinition` record |
| `src/Oravey2.Core/NPC/NpcComponent.cs` | Stride `EntityComponent` holding `NpcDefinition` |
| `src/Oravey2.Core/NPC/NpcNameLabelScript.cs` | SyncScript: floating name text above NPC (billboard) |
| `tests/Oravey2.Tests/NPC/NpcDefinitionTests.cs` | Record construction + equality |

### Modify

| File | Change |
|------|--------|
| `src/Oravey2.Core/Bootstrap/ScenarioLoader.cs` | `LoadTown()` spawns 4 NPC entities with capsule visuals, `NpcComponent`, `NpcNameLabelScript` |
| `src/Oravey2.Core/Automation/OraveyAutomationHandler.cs` | Add `GetNpcList` handler |
| `src/Oravey2.Core/Automation/AutomationContracts.cs` | Add `NpcListResponse`, `NpcDto` |

### NPC Entities (spawned in LoadTown)

| Entity Name | NpcDefinition | Color | World Position |
|-------------|--------------|-------|---------------|
| `npc_elder` | Elder Tomas, QuestGiver | Green (0.2, 0.8, 0.2) | (-4, 0.5, -4.5) |
| `npc_merchant` | Mara, Merchant | Blue (0.2, 0.3, 0.9) | (1, 0.5, -3.5) |
| `npc_civilian_1` | Settler, Civilian | Gray (0.6, 0.6, 0.6) | (-4, 0.5, 3.5) |
| `npc_civilian_2` | Settler, Civilian | Gray (0.6, 0.6, 0.6) | (13, 0.5, 3.5) |

Each NPC entity gets: capsule model, `NpcComponent`, `NpcNameLabelScript`.

### Unit Tests (+5)

| Test | Validates |
|------|-----------|
| `NpcDefinition_Creates_WithCorrectProperties` | Record construction |
| `NpcDefinition_QuestGiver_Role` | Role assignment |
| `NpcDefinition_Merchant_Role` | Role assignment |
| `NpcDefinition_Civilian_Role` | Role assignment |
| `NpcDefinition_Equality` | Record equality, same values == equal |

### UI Tests (+3, in `tests/Oravey2.UITests/TownTests.cs`)

| Test | Validates |
|------|-----------|
| `Town_LoadsSuccessfully` | `--scenario town`, GameState = Exploring |
| `Town_HasFourNpcs` | `GetNpcList` returns 4 NPCs |
| `Town_ElderExists_AtCorrectPosition` | elder NPC at (-4, 0.5, -4.5) |

### Acceptance

- Build passes
- 5 new unit tests + 3 new UI tests pass (668 total)
- 4 colored NPCs visible in town with floating name labels

---

## Sub-Phase 2.3 — NPC Interaction (Walk Up + Press F)

**Scope:** Add proximity detection and interaction dispatch. When the player walks within 2 units of an NPC and presses F, an event fires. Testable: automation can detect NPC in range and trigger interaction.

### New Files

| File | Contents |
|------|----------|
| `src/Oravey2.Core/NPC/InteractionTriggerScript.cs` | SyncScript: proximity check each frame, publishes `NpcInteractionEvent` on F press |
| `src/Oravey2.Core/NPC/NpcInteractionEvent.cs` | Event record: `NpcId`, `DialogueTreeId` |

### Modify

| File | Change |
|------|--------|
| `src/Oravey2.Core/Bootstrap/ScenarioLoader.cs` | `LoadTown()` adds `InteractionTriggerScript` to each NPC entity |
| `src/Oravey2.Core/Automation/OraveyAutomationHandler.cs` | Add `GetNpcInRange` + `InteractWithNpc` handlers |
| `src/Oravey2.Core/Automation/AutomationContracts.cs` | Add `NpcInRangeResponse`, `InteractResponse` |

### InteractionTriggerScript

```csharp
public class InteractionTriggerScript : SyncScript
{
    public Entity? Player { get; set; }
    public float InteractionRadius { get; set; } = 2.0f;
    public NpcDefinition? NpcDef { get; set; }
    public IInputProvider? InputProvider { get; set; }
    public IEventBus? EventBus { get; set; }
    public GameStateManager? StateManager { get; set; }

    public bool PlayerInRange { get; private set; }

    public override void Update()
    {
        if (StateManager?.CurrentState != GameState.Exploring) { PlayerInRange = false; return; }
        if (Player == null) return;

        var dist = (Player.Transform.Position - Entity.Transform.Position).Length();
        PlayerInRange = dist <= InteractionRadius;

        if (PlayerInRange && InputProvider?.IsActionPressed(GameAction.Interact) == true && NpcDef != null)
            EventBus?.Publish(new NpcInteractionEvent(NpcDef.Id, NpcDef.DialogueTreeId));
    }
}
```

### Unit Tests (+4)

| Test | Validates |
|------|-----------|
| `NpcInteractionEvent_Constructor` | Event record properties |
| `InteractionTriggerScript_DefaultRadius_IsTwo` | Default 2.0f |
| `NpcComponent_HoldsDefinition` | Property get/set |
| `NpcInteractionEvent_Equality` | Record equality |

### UI Tests (+3, appended to `TownTests.cs`)

| Test | Validates |
|------|-----------|
| `Town_TeleportToElder_ShowsInRange` | Teleport player near elder, `GetNpcInRange` returns elder |
| `Town_FarFromNpc_NotInRange` | Player at spawn, no NPC in range |
| `Town_InteractWithElder_FiresEvent` | `InteractWithNpc` returns success |

### Acceptance

- Build passes
- 4 new unit tests + 3 new UI tests pass (675 total)
- Walking near an NPC and pressing F fires the interaction event

---

## Sub-Phase 2.4 — Dialogue Wiring (Talk to NPC → Dialogue UI)

**Scope:** Wire `NpcInteractionEvent` → `DialogueProcessor.StartDialogue()` → `DialogueOverlayScript` renders conversation. Add dialogue choice input (keys 1-4). Create the town dialogue trees. Testable: full conversation flow visible, dialogue state queryable.

### New Files

| File | Contents |
|------|----------|
| `src/Oravey2.Core/UI/Stride/DialogueOverlayScript.cs` | SyncScript: renders dialogue panel (speaker, text, choices) |
| `src/Oravey2.Core/NPC/TownDialogueTrees.cs` | Static methods building Elder, Merchant, Civilian dialogue trees |
| `tests/Oravey2.Tests/NPC/TownDialogueTreeTests.cs` | Tree structure + validation tests |

### Modify

| File | Change |
|------|--------|
| `src/Oravey2.Core/Input/GameAction.cs` | Add `DialogueChoice1` through `DialogueChoice4` |
| `src/Oravey2.Core/Input/KeyboardMouseInputProvider.cs` | Map `DialogueChoice1-4` to keys `D1-D4` |
| `src/Oravey2.Core/Bootstrap/ScenarioLoader.cs` | `LoadTown()` creates `DialogueOverlayScript` entity, subscribes to `NpcInteractionEvent` to start dialogue |
| `src/Oravey2.Core/Bootstrap/GameBootstrapper.cs` | Wire `DialogueProcessor`, `DialogueContext`, `WorldStateService`, `SkillsComponent` as shared services |
| `src/Oravey2.Core/Automation/OraveyAutomationHandler.cs` | Add `GetDialogueState` + `SelectDialogueChoice` handlers |
| `src/Oravey2.Core/Automation/AutomationContracts.cs` | Add `DialogueStateResponse`, `DialogueChoiceResponse` |

### DialogueOverlayScript Layout

```
┌──────────────────────────────────────────┐
│  (game world visible)                    │
├──────────────────────────────────────────┤
│  [■] Speaker Name                        │
│      "Dialogue text..."                  │
│  1. Choice A                             │
│  2. Choice B                             │
│  3. Choice C [Melee 20]                  │
│  4. [Leave]                              │
└──────────────────────────────────────────┘
```

- Visible when `DialogueProcessor.IsActive` and `GameState == InDialogue`
- Portrait = colored square (green=QuestGiver, blue=Merchant, gray=Civilian)
- Keys 1-4 select choices; unavailable choices grayed out
- When NextNodeId = null → `EndDialogue()` → `GameState.Exploring`

### Dialogue Event Flow

```
NpcInteractionEvent fired
  → GameBootstrapper subscriber receives it
  → Looks up tree by DialogueTreeId from TownDialogueTrees
  → DialogueProcessor.StartDialogue(tree)
  → GameState → InDialogue
  → DialogueOverlayScript renders current node

Player presses 1-4
  → DialogueOverlayScript calls Processor.SelectChoice(index, context)
  → Consequences execute (caps change, flag set, etc.)
  → Next node or EndDialogue → GameState.Exploring
```

### Town Dialogue Trees

**`TownDialogueTrees.ElderDialogue()`** — 3 nodes:
- `greeting`: "I'm Elder Tomas..." → choices: "What's going on?" / "Goodbye"
- `quest_offer`: "Radrats have been..." → choices: "I'll help" [StartQuestAction] / "Not now"
- End nodes

**`TownDialogueTrees.MerchantDialogue()`** — 2 nodes:
- `greeting`: "Welcome to my stall..." → choices: Buy medkit (10 caps) / Buy jacket (25 caps) / Sell scrap (5 caps) / "Never mind"
- Buy/sell cycle back to greeting. Leave → end.

**`TownDialogueTrees.CivilianDialogue()`** — 1 node:
- `greeting`: "Stay safe out there." → choices: "Thanks" → end

### Unit Tests (+10)

| Test | Validates |
|------|-----------|
| `GameAction_HasDialogueChoices` | Enum values 1-4 exist |
| `TownDialogueTrees_ElderTree_HasStartNode` | Tree construction valid |
| `TownDialogueTrees_ElderTree_HasChoices` | Greeting has ≥ 2 choices |
| `TownDialogueTrees_ElderTree_QuestSetsFlag` | StartQuestAction in consequences |
| `TownDialogueTrees_MerchantTree_HasBuyChoices` | Medkit + jacket choices |
| `TownDialogueTrees_MerchantTree_BuyMedkit_Cost10` | BuyItemAction with cost 10 |
| `TownDialogueTrees_MerchantTree_SellScrap_Price5` | SellItemAction with price 5 |
| `TownDialogueTrees_CivilianTree_HasEndChoice` | Leave → NextNodeId null |
| `TownDialogueTrees_AllTrees_NoDanglingRefs` | Every NextNodeId exists in Nodes |
| `TownDialogueTrees_MerchantTree_LeaveEndsDialogue` | "Never mind" → null |

### UI Tests (+5, appended to `TownTests.cs`)

| Test | Validates |
|------|-----------|
| `Town_InteractWithElder_OpensDialogue` | GameState = InDialogue |
| `Town_DialogueShows_SpeakerAndText` | GetDialogueState has "Elder Tomas" + text |
| `Town_DialogueChoices_ArePresent` | GetDialogueState has choices array |
| `Town_SelectLeave_EndsDialogue` | SelectDialogueChoice(leave) → GameState.Exploring |
| `Town_CivilianDialogue_Works` | InteractWithNpc(civilian_1) → dialogue → end |

### Acceptance

- Build passes
- 10 new unit tests + 5 new UI tests pass (690 total)
- Full NPC→dialogue→choices→end flow works

---

## Sub-Phase 2.5 — Merchant Buy/Sell Actions

**Scope:** Implement the 3 trade consequence actions (BuyItem, SellItem, GiveItem) and verify via merchant dialogue. Testable: caps change, inventory changes.

### New Files

| File | Contents |
|------|----------|
| `src/Oravey2.Core/Dialogue/BuyItemAction.cs` | `IConsequenceAction` — deduct caps, add item to inventory |
| `src/Oravey2.Core/Dialogue/SellItemAction.cs` | `IConsequenceAction` — remove item, add caps |
| `src/Oravey2.Core/Dialogue/GiveItemAction.cs` | `IConsequenceAction` — add item (quest reward) |
| `tests/Oravey2.Tests/Dialogue/TradeActionTests.cs` | All trade action unit tests |

### Trade Action Behavior

```csharp
// BuyItemAction.Execute(context):
if (context.Inventory.Caps >= Cost)
{
    context.Inventory.Caps -= Cost;
    context.Inventory.Add(new ItemInstance(ItemResolver.Resolve(ItemId)));
}

// SellItemAction.Execute(context):
if (context.Inventory.Contains(ItemId))
{
    context.Inventory.Remove(ItemId);
    context.Inventory.Caps += Price;
}

// GiveItemAction.Execute(context):
context.Inventory.Add(new ItemInstance(ItemResolver.Resolve(ItemId), Count));
```

`ItemResolver` is a small static lookup mapping item IDs to `ItemDefinition` (delegates to `M0Items`). Keeps actions decoupled from the static factory.

### Modify

| File | Change |
|------|--------|
| `src/Oravey2.Core/NPC/TownDialogueTrees.cs` | Merchant tree choices now use real `BuyItemAction`/`SellItemAction` in consequences |

### Unit Tests (+12)

| Test | Validates |
|------|-----------|
| `BuyItemAction_DeductsCaps_AddsItem` | 50 caps → buy medkit 10 → 40 caps + medkit |
| `BuyItemAction_InsufficientCaps_NoChange` | 5 caps, cost 10 → stays 5, no item |
| `BuyItemAction_ExactCaps_Works` | 10 caps, cost 10 → 0 caps + item |
| `BuyItemAction_ZeroCaps_NoNegative` | 0 caps, can't buy |
| `SellItemAction_RemovesItem_AddsCaps` | Has scrap → sell → +5 caps, scrap gone |
| `SellItemAction_NoItem_NoChange` | No scrap → caps unchanged |
| `SellItemAction_StackOf3_SellOne_StackOf2` | Stack decrements |
| `GiveItemAction_AddsItem` | Empty → give medkit ×2 → has 2 |
| `GiveItemAction_StacksWithExisting` | Has 1 → give 2 → 3 total |
| `BuyItemAction_ViaDialogueProcessor` | Full flow: start merchant tree → select buy → caps change |
| `SellItemAction_ViaDialogueProcessor` | Full flow: start merchant tree → select sell → caps change |
| `GiveItemAction_ViaDialogueProcessor` | Full flow: start elder tree → accept quest → item added |

### UI Tests (+3, appended to `TownTests.cs`)

| Test | Validates |
|------|-----------|
| `Town_BuyMedkit_DeductsCaps` | InteractWithNpc(merchant) → SelectChoice(buy medkit) → 50→40 caps |
| `Town_BuyMedkit_AddsToInventory` | GetInventoryState shows medkit |
| `Town_SellScrap_AddsCaps` | Give player scrap → sell → caps increase |

### Acceptance

- Build passes
- 12 new unit tests + 3 new UI tests pass (705 total)
- Merchant buy/sell works end-to-end through dialogue

---

## Sub-Phase 2.6 — Zone Transition (Town → Wasteland)

**Scope:** Zone exit trigger at gate tiles, ZoneManager orchestration, auto-save on zone exit. Testable: walk to gate → zone changes.

### New Files

| File | Contents |
|------|----------|
| `src/Oravey2.Core/World/ZoneExitTriggerScript.cs` | SyncScript: player proximity to gate → fires callback |
| `src/Oravey2.Core/World/ZoneManager.cs` | Tracks current zone, delegates load/unload to ScenarioLoader |
| `tests/Oravey2.Tests/World/ZoneManagerTests.cs` | Zone tracking unit tests |

### Modify

| File | Change |
|------|--------|
| `src/Oravey2.Core/Bootstrap/ScenarioLoader.cs` | `LoadTown()` places `ZoneExitTriggerScript` at gate position (14, 0.5, 0) |
| `src/Oravey2.Core/Bootstrap/GameBootstrapper.cs` | Create `ZoneManager`, wire zone exit callback (unload town → load wasteland → auto-save) |
| `src/Oravey2.Core/Automation/OraveyAutomationHandler.cs` | Add `GetCurrentZone` handler |
| `src/Oravey2.Core/Automation/AutomationContracts.cs` | Add `CurrentZoneResponse` |

### ZoneExitTriggerScript

```csharp
public class ZoneExitTriggerScript : SyncScript
{
    public Entity? Player { get; set; }
    public string TargetZoneId { get; set; } = "";
    public Vector3 TargetSpawnPosition { get; set; }
    public float TriggerRadius { get; set; } = 1.5f;
    public GameStateManager? StateManager { get; set; }
    public Action<string, Vector3>? OnZoneExit { get; set; }

    private bool _triggered;

    public override void Update()
    {
        if (_triggered || StateManager?.CurrentState != GameState.Exploring || Player == null) return;
        var dist = (Player.Transform.Position - Entity.Transform.Position).Length();
        if (dist <= TriggerRadius)
        {
            _triggered = true;
            OnZoneExit?.Invoke(TargetZoneId, TargetSpawnPosition);
        }
    }
}
```

### ZoneManager

```csharp
public class ZoneManager
{
    private readonly ScenarioLoader _scenarioLoader;
    public string? CurrentZoneId { get; private set; }

    public ZoneManager(ScenarioLoader scenarioLoader) { _scenarioLoader = scenarioLoader; }

    public void SetCurrentZone(string zoneId) { CurrentZoneId = zoneId; }
    public void TransitionTo(string zoneId, Scene rootScene, Game game,
        Entity cameraEntity, GameStateManager gsm, IEventBus eventBus,
        IInputProvider input, ILogger logger, Vector3 playerSpawn)
    {
        _scenarioLoader.Unload(rootScene);
        // Map zone IDs to scenario methods
        var scenarioId = zoneId switch { "town" => "town", "wasteland" => "m0_combat", _ => zoneId };
        _scenarioLoader.Load(scenarioId, rootScene, game, cameraEntity, gsm, eventBus, input, logger);
        if (_scenarioLoader.PlayerEntity != null)
            _scenarioLoader.PlayerEntity.Transform.Position = playerSpawn;
        CurrentZoneId = zoneId;
    }
}
```

### Unit Tests (+5)

| Test | Validates |
|------|-----------|
| `ZoneManager_SetCurrentZone_TracksId` | CurrentZoneId set correctly |
| `ZoneManager_InitialZone_IsNull` | Starts null |
| `ZoneExitTrigger_DefaultRadius_Is1_5` | Default 1.5f |
| `ZoneExitTrigger_PropertiesAssignable` | TargetZoneId, TargetSpawnPosition |
| `ZoneManager_SetZone_Twice_OverwritesPrevious` | Last wins |

### UI Tests (+3, appended to `TownTests.cs`)

| Test | Validates |
|------|-----------|
| `Town_CurrentZone_IsTown` | `GetCurrentZone` = "town" |
| `Town_TeleportToGate_TransitionsZone` | TeleportPlayer near gate → `GetCurrentZone` = "wasteland" |
| `Town_ZoneTransition_PlayerAtSpawn` | After transition, player at wasteland spawn position |

### Acceptance

- Build passes
- 5 new unit tests + 3 new UI tests pass (716 total)
- All M0 + Phase 1 tests still pass
- Town → Wasteland zone transition works end-to-end

---

## Summary

| Sub-Phase | What Ships | Unit | UI | Running Total |
|-----------|-----------|------|-----|--------------|
| **2.1** | Town loads: tile map, player, HUD, walkable | +8 | — | 660 |
| **2.2** | 4 NPC entities with visuals + names + `GetNpcList` | +5 | +3 | 668 |
| **2.3** | Proximity trigger + F interaction + events | +4 | +3 | 675 |
| **2.4** | Dialogue UI + trees + keys 1-4 + full conversation flow | +10 | +5 | 690 |
| **2.5** | BuyItem / SellItem / GiveItem + merchant trading | +12 | +3 | 705 |
| **2.6** | Zone exit trigger + ZoneManager + town→wasteland | +5 | +3 | 716 |
| **Total** | | **+44** | **+17** | **~716** |

Each sub-phase produces a working, testable increment. The town is playable after 2.1 and gains one new feature per sub-phase.
