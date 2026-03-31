# M1 Phase 2 — Task List

**Baseline:** 697 unit tests + 25 UI tests passing (8 smoke + 17 town)

---

## Sub-Phase 2.1 — Bare Town Scenario ✅

- [x] `TownMapBuilder.cs` — 32×32 town tile map
- [x] `ScenarioLoader` — `case "town": LoadTown(...)` 
- [x] `TownMapBuilderTests.cs` — 9 unit tests (including wall regression test)
- [x] Town loads via `--scenario town`, player walks, HUD works

## Sub-Phase 2.2 — NPC Data Model + Spawning ✅

- [x] `NpcDefinition.cs` — `NpcRole` enum + `NpcDefinition` record
- [x] `NpcComponent.cs` — EntityComponent with `Definition` + `CollisionRadius`
- [x] `NpcNameLabelScript.cs` — Floating billboard name label
- [x] `ScenarioLoader.LoadTown()` — Spawns 4 NPCs (Elder, Mara, 2 Settlers)
- [x] `AutomationContracts` — `NpcDto`, `NpcListResponse`
- [x] `OraveyAutomationHandler` — `GetNpcList` handler
- [x] `GameQueryHelpers` — `GetNpcList()` helper
- [x] `NpcDefinitionTests.cs` — 5 unit tests
- [x] `TownTests.cs` — 3 UI tests (TownTestFixture + load/count/position)

## Bug Fixes (applied during 2.1/2.2) ✅

- [x] ISSUE-001: Menu overlay visible in automation mode — `_pendingHide` flag in `StartMenuScript`
- [x] ISSUE-002: Elder NPC color too similar to player — color changed 
- [x] ISSUE-003: Player walks through walls — radius-based bbox collision + road/wall paint order fix
- [x] ISSUE-004: Player walks through NPCs — circle-circle collision in `PlayerMovementScript`

## Sub-Phase 2.3 — NPC Interaction (Walk Up + Press F) ✅

- [x] `InteractionTriggerScript.cs` — SyncScript: proximity check, publishes `NpcInteractionEvent` on F
- [x] `NpcInteractionEvent.cs` — Event record: `NpcId`, `DialogueTreeId` (implements `IGameEvent`)
- [x] `ScenarioLoader.LoadTown()` — Attach `InteractionTriggerScript` to each NPC
- [x] `AutomationContracts` — `NpcInRangeResponse`, `InteractWithNpcRequest`, `InteractResponse`
- [x] `OraveyAutomationHandler` — `GetNpcInRange` + `InteractWithNpc` handlers
- [x] `GameQueryHelpers` — `GetNpcInRange()` + `InteractWithNpc()` helpers
- [x] Unit tests (+4): event construction, trigger defaults, component tests
- [x] UI tests (+3): teleport near elder → in range, far → not in range, interact fires event

## Sub-Phase 2.4 — Dialogue Wiring (NPC → Dialogue UI) ✅

- [x] `DialogueOverlayScript.cs` — SyncScript: renders dialogue panel (speaker, text, choices)
- [x] `TownDialogueTrees.cs` — Static methods: Elder, Merchant, Civilian trees + `GetTree()` resolver
- [x] `GameAction` — Added `DialogueChoice1` through `DialogueChoice4`
- [x] `KeyboardMouseInputProvider` — Map D1-D4 to dialogue choices
- [x] `ScenarioLoader.LoadTown()` — Creates DialogueProcessor/Context, subscribes to NpcInteractionEvent, creates DialogueOverlayScript
- [x] `ScenarioLoader` — Added `DialogueProcessor` and `DialogueContext` properties
- [x] `AutomationContracts` — `DialogueStateResponse`, `DialogueChoiceDto`, `SelectDialogueChoiceRequest`, `DialogueChoiceResponse`
- [x] `OraveyAutomationHandler` — `GetDialogueState` + `SelectDialogueChoice` handlers
- [x] `GameQueryHelpers` — `GetDialogueState()` + `SelectDialogueChoice()` helpers
- [x] `TownDialogueTreeTests.cs` — 10 unit tests (tree structure, choices, consequences, dangling refs)
- [x] UI tests (+5): open dialogue, speaker/text, choices, leave ends, civilian flow

## Sub-Phase 2.5 — Merchant Buy/Sell Actions ✅

- [x] `ItemResolver.cs` — Static lookup mapping item IDs to ItemDefinition (delegates to M0Items)
- [x] `BuyItemAction.cs` — IConsequenceAction: deduct caps, add item
- [x] `SellItemAction.cs` — IConsequenceAction: remove item, add caps
- [x] `GiveItemAction.cs` — IConsequenceAction: add item (quest reward)
- [x] `TownDialogueTrees` — Wire real BuyItemAction/SellItemAction in merchant tree
- [x] `AutomationContracts` — `GiveItemToPlayerRequest`/`GiveItemToPlayerResponse`
- [x] `OraveyAutomationHandler` — `GiveItemToPlayer` handler (test helper)
- [x] `GameQueryHelpers` — `GiveItemToPlayer()` helper
- [x] `TradeActionTests.cs` — 12 unit tests (buy/sell/give + dialogue processor integration)
- [x] UI tests (+3): buy deducts caps, buy adds to inventory, sell adds caps

## Sub-Phase 2.6 — Zone Transition (Town → Wasteland) ✅

- [x] `ZoneExitTriggerScript.cs` — SyncScript: player proximity to gate → fires callback
- [x] `ZoneManager.cs` — Tracks current zone, delegates load/unload to ScenarioLoader
- [x] `ScenarioLoader.LoadTown()` — Place `ZoneExitTriggerScript` at gate
- [x] `GameBootstrapper` — Create `ZoneManager`, wire zone exit + auto-save
- [x] `AutomationContracts` — `CurrentZoneResponse`
- [x] `OraveyAutomationHandler` — `GetCurrentZone` handler
- [x] `ZoneManagerTests.cs` — 5 unit tests (zone tracking, defaults, overwrite)
- [x] UI tests (+3): current zone = town, teleport to gate → wasteland, player at spawn

---

## Progress Summary

| Sub-Phase | Status | Unit Tests | UI Tests |
|-----------|--------|-----------|----------|
| 2.1 Town Map | ✅ Done | +9 | — |
| 2.2 NPCs | ✅ Done | +5 | +3 |
| Bug Fixes | ✅ Done | +1 | — |
| 2.3 Interaction | ✅ Done | +4 | +3 |
| 2.4 Dialogue | ✅ Done | +10 | +5 |
| 2.5 Merchant | ✅ Done | +12 | +3 |
| 2.6 Zones | ✅ Done | +5 | +3 |

**Final: 697 unit tests + 25 UI tests — M1 Phase 2 COMPLETE**
