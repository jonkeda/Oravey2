# Quests Schema

**Asset Path:** `Assets/Data/Quests/<quest_id>.json` (one file per quest)

---

## Top-Level Structure

```json
{
  "id": "supply_run",
  "title": "Supply Run",
  "description": "Deliver the supply crate to the northern outpost.",
  "type": "side",
  "firstStageId": "deliver",
  "stages": {
    "<stageId>": { /* QuestStage */ }
  }
}
```

---

## QuestDefinition

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `id` | `string` | yes | unique, snake_case | Internal identifier |
| `title` | `string` | yes | — | Display title in quest log |
| `description` | `string` | yes | — | Summary text |
| `type` | `string` | yes | enum below | Quest classification |
| `firstStageId` | `string` | yes | — | Starting stage ID |
| `stages` | `object` | yes | — | Map of stage ID → `QuestStage` |
| `requiredLevel` | `integer` | no | ≥ 1 | Min level to start (default: 1) |
| `requiredFaction` | `object` | no | — | `{ "factionId": "...", "minRep": 20 }` |
| `xpReward` | `integer` | no | ≥ 0 | Total XP on completion |
| `icon` | `string` | no | — | Quest log icon |

### Quest Type Enum

```
"main", "faction", "side", "radiant"
```

---

## QuestStage

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | `string` | yes | Unique within this quest |
| `description` | `string` | yes | Journal text for this stage |
| `conditions` | `QuestCondition[]` | yes | All must be true to complete the stage |
| `onComplete` | `QuestAction[]` | no | Actions executed when stage completes |
| `nextStageId` | `string` | no | Next stage. `null` = quest complete |
| `failConditions` | `QuestCondition[]` | no | Any true = quest fails |
| `onFail` | `QuestAction[]` | no | Actions executed on failure |

---

## QuestCondition Types

### HasItem

```json
{ "type": "has_item", "itemId": "supply_crate", "count": 1 }
```

### Flag

```json
{ "type": "flag", "flag": "outpost_reached", "expected": true }
```

### FactionRep

```json
{ "type": "faction_rep", "factionId": "iron_brotherhood", "minRep": 30 }
```

### Level

```json
{ "type": "level", "minLevel": 5 }
```

### EntityDead

```json
{ "type": "entity_dead", "entityId": "raider_boss_alpha" }
```

### ZoneVisited

```json
{ "type": "zone_visited", "zoneId": "northern_outpost" }
```

### QuestComplete

```json
{ "type": "quest_complete", "questId": "find_the_map" }
```

---

## QuestAction Types

### GiveItem

```json
{ "type": "give_item", "itemId": "caps", "count": 100 }
```

### RemoveItem

```json
{ "type": "remove_item", "itemId": "supply_crate", "count": 1 }
```

### SpawnEntity

```json
{ "type": "spawn_entity", "prefabId": "raider_ambush_group", "position": { "x": 12.0, "y": 0.0, "z": 45.0 } }
```

### UpdateJournal

```json
{ "type": "update_journal", "text": "I delivered the supplies. The outpost leader seemed grateful." }
```

### TriggerEvent

```json
{ "type": "trigger_event", "eventId": "outpost_opens_trade" }
```

### SetFlag

```json
{ "type": "set_flag", "flag": "supply_run_complete", "value": true }
```

### ModifyFaction

```json
{ "type": "modify_faction", "factionId": "settlers_alliance", "delta": 15 }
```

### GiveXP

```json
{ "type": "give_xp", "amount": 200 }
```

---

## Full Example

```json
{
  "id": "supply_run",
  "title": "Supply Run",
  "description": "The merchant needs supplies delivered to the northern outpost before the raiders close the road.",
  "type": "side",
  "firstStageId": "deliver_crate",
  "xpReward": 200,
  "stages": {
    "deliver_crate": {
      "id": "deliver_crate",
      "description": "Deliver the supply crate to the northern outpost.",
      "conditions": [
        { "type": "has_item", "itemId": "supply_crate", "count": 1 },
        { "type": "zone_visited", "zoneId": "northern_outpost" }
      ],
      "onComplete": [
        { "type": "remove_item", "itemId": "supply_crate", "count": 1 },
        { "type": "set_flag", "flag": "outpost_reached", "value": true },
        { "type": "update_journal", "text": "I reached the outpost. The commander wants me to talk to him." }
      ],
      "nextStageId": "talk_commander",
      "failConditions": [
        { "type": "flag", "flag": "supply_crate_destroyed", "expected": true }
      ],
      "onFail": [
        { "type": "modify_faction", "factionId": "settlers_alliance", "delta": -10 },
        { "type": "update_journal", "text": "The supply crate was destroyed. The merchant won't be happy." }
      ]
    },
    "talk_commander": {
      "id": "talk_commander",
      "description": "Speak to Commander Harlow at the northern outpost.",
      "conditions": [
        { "type": "flag", "flag": "talked_to_harlow", "expected": true }
      ],
      "onComplete": [
        { "type": "give_item", "itemId": "caps", "count": 100 },
        { "type": "give_item", "itemId": "stimpak", "count": 3 },
        { "type": "give_xp", "amount": 200 },
        { "type": "modify_faction", "factionId": "settlers_alliance", "delta": 15 },
        { "type": "set_flag", "flag": "supply_run_complete", "value": true },
        { "type": "update_journal", "text": "Commander Harlow rewarded me for the delivery." }
      ],
      "nextStageId": null
    }
  }
}
```
