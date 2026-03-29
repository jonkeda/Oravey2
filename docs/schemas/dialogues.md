# Dialogues Schema

**Asset Path:** `Assets/Data/Dialogues/<npc_id>.json` (one file per NPC or conversation)

---

## Top-Level Structure

```json
{
  "id": "merchant_intro",
  "startNodeId": "start",
  "nodes": {
    "<nodeId>": { /* DialogueNode */ }
  }
}
```

---

## DialogueTree

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | `string` | yes | Unique dialogue tree identifier |
| `startNodeId` | `string` | yes | ID of the first node to display |
| `nodes` | `object` | yes | Map of node ID → `DialogueNode` |

---

## DialogueNode

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | `string` | yes | Unique within this tree |
| `speaker` | `string` | yes | Display name of the speaker |
| `text` | `string` | yes | Dialogue text shown to the player |
| `portrait` | `string` | no | Speaker portrait sprite key |
| `choices` | `DialogueChoice[]` | yes | Available responses (empty array = end conversation) |

---

## DialogueChoice

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `text` | `string` | yes | Text displayed on the choice button |
| `nextNodeId` | `string` | no | Node to jump to. `null` = end dialogue |
| `condition` | `Condition` | no | If present, choice only visible/enabled when condition passes |
| `consequences` | `Consequence[]` | no | Actions executed when this choice is selected |

---

## Condition Types

Each condition has a `type` field that determines interpretation.

### SkillCheck

```json
{
  "type": "skill_check",
  "skill": "Speech",
  "threshold": 40,
  "hidden": false
}
```

| Field | Type | Description |
|-------|------|-------------|
| `skill` | `string` | SkillType name |
| `threshold` | `integer` | Minimum effective skill |
| `hidden` | `boolean` | If `true`, requirement not shown to player |

### Flag

```json
{
  "type": "flag",
  "flag": "rescued_prisoner",
  "expected": true
}
```

### Item

```json
{
  "type": "has_item",
  "itemId": "mysterious_key",
  "count": 1
}
```

### Faction

```json
{
  "type": "faction_rep",
  "factionId": "settlers_alliance",
  "minRelation": "friendly"
}
```

| `minRelation` values | `"hostile"`, `"unfriendly"`, `"neutral"`, `"friendly"`, `"allied"` |

### Stat

```json
{
  "type": "stat_check",
  "stat": "Intelligence",
  "minimum": 6
}
```

### Level

```json
{
  "type": "level",
  "minLevel": 5
}
```

---

## Consequence Types

### GiveItem

```json
{ "type": "give_item", "itemId": "stimpak", "count": 3 }
```

### RemoveItem

```json
{ "type": "remove_item", "itemId": "mysterious_key", "count": 1 }
```

### ModifyFaction

```json
{ "type": "modify_faction", "factionId": "settlers_alliance", "delta": 10 }
```

### SetFlag

```json
{ "type": "set_flag", "flag": "met_merchant", "value": true }
```

### StartQuest

```json
{ "type": "start_quest", "questId": "supply_run" }
```

### ModifyStat

```json
{ "type": "modify_stat", "stat": "Charisma", "delta": 1 }
```

### GiveXP

```json
{ "type": "give_xp", "amount": 50 }
```

---

## Full Example

```json
{
  "id": "merchant_intro",
  "startNodeId": "greeting",
  "nodes": {
    "greeting": {
      "id": "greeting",
      "speaker": "Merchant",
      "text": "What do you want, stranger? I don't have all day.",
      "portrait": "npc_merchant",
      "choices": [
        {
          "text": "I need supplies.",
          "nextNodeId": "trade_offer"
        },
        {
          "text": "[Speech 40] How about a discount for a friend?",
          "nextNodeId": "discount_success",
          "condition": { "type": "skill_check", "skill": "Speech", "threshold": 40, "hidden": false },
          "consequences": [
            { "type": "set_flag", "flag": "merchant_discount", "value": true },
            { "type": "modify_faction", "factionId": "settlers_alliance", "delta": 5 }
          ]
        },
        {
          "text": "[Intelligence 6] These prices are inflated by at least 30%.",
          "nextNodeId": "intel_discount",
          "condition": { "type": "stat_check", "stat": "Intelligence", "minimum": 6 }
        },
        {
          "text": "I'm looking for work.",
          "nextNodeId": "quest_offer",
          "condition": { "type": "flag", "flag": "supply_run_complete", "expected": false }
        },
        {
          "text": "Goodbye.",
          "nextNodeId": null
        }
      ]
    },
    "trade_offer": {
      "id": "trade_offer",
      "speaker": "Merchant",
      "text": "Take a look at what I've got. Fair prices, all of it.",
      "choices": []
    },
    "discount_success": {
      "id": "discount_success",
      "speaker": "Merchant",
      "text": "Ha! Fine, you've got a silver tongue. I'll knock 10% off.",
      "choices": [
        { "text": "Thanks. Let me see your wares.", "nextNodeId": "trade_offer" },
        { "text": "I'll come back later.", "nextNodeId": null }
      ]
    },
    "intel_discount": {
      "id": "intel_discount",
      "speaker": "Merchant",
      "text": "...You're sharper than you look. Alright, I'll drop the markup.",
      "consequences": [
        { "type": "set_flag", "flag": "merchant_discount", "value": true }
      ],
      "choices": [
        { "text": "Smart choice. Show me what you have.", "nextNodeId": "trade_offer" }
      ]
    },
    "quest_offer": {
      "id": "quest_offer",
      "speaker": "Merchant",
      "text": "As a matter of fact... I need someone to run supplies to the northern outpost. Interested?",
      "choices": [
        {
          "text": "I'll do it.",
          "nextNodeId": "quest_accepted",
          "consequences": [
            { "type": "start_quest", "questId": "supply_run" },
            { "type": "give_item", "itemId": "supply_crate", "count": 1 }
          ]
        },
        { "text": "Not right now.", "nextNodeId": null }
      ]
    },
    "quest_accepted": {
      "id": "quest_accepted",
      "speaker": "Merchant",
      "text": "Here's the crate. Get it there in one piece and I'll make it worth your while.",
      "choices": []
    }
  }
}
```
