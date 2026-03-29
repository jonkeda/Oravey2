# Factions Schema

**Asset Path:** `Assets/Data/Factions/factions.json`

---

## Top-Level Structure

```json
{
  "factions": [
    { /* FactionDefinition */ }
  ],
  "defaultRelations": [
    { /* FactionRelationEntry */ }
  ]
}
```

---

## FactionDefinition

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `id` | `string` | yes | unique, snake_case | Internal identifier |
| `name` | `string` | yes | — | Display name |
| `description` | `string` | yes | — | Lore / background text |
| `icon` | `string` | no | — | Faction emblem sprite key |
| `defaultReputation` | `integer` | yes | -100 to 100 | Starting player reputation |
| `hostileThreshold` | `integer` | yes | -100 to 100 | Rep below this = faction attacks on sight |
| `friendlyThreshold` | `integer` | yes | -100 to 100 | Rep above this = discounts, quests, safe passage |
| `alliedThreshold` | `integer` | yes | -100 to 100 | Rep above this = full ally benefits |

### Reputation Tiers (derived from thresholds)

| Range | Relation | Gameplay Effect |
|-------|----------|----------------|
| `< hostileThreshold` | Hostile | Attack on sight, no trading |
| `hostileThreshold` to -1 | Unfriendly | Won't attack, limited trading, no quests |
| 0 to `friendlyThreshold - 1` | Neutral | Normal trading, basic quests |
| `friendlyThreshold` to `alliedThreshold - 1` | Friendly | Discounts, faction quests, safe passage |
| `≥ alliedThreshold` | Allied | Best prices, unique quests, faction gear |

---

## FactionRelationEntry

Pre-set relations between factions (not player rep — inter-faction diplomacy).

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `factionA` | `string` | yes | Faction ID |
| `factionB` | `string` | yes | Faction ID |
| `relation` | `string` | yes | `"hostile"`, `"unfriendly"`, `"neutral"`, `"friendly"`, `"allied"` |

---

## Example

```json
{
  "factions": [
    {
      "id": "settlers_alliance",
      "name": "Settler's Alliance",
      "description": "A loose coalition of survivors rebuilding communities from the ruins.",
      "defaultReputation": 10,
      "hostileThreshold": -50,
      "friendlyThreshold": 30,
      "alliedThreshold": 75
    },
    {
      "id": "iron_brotherhood",
      "name": "Iron Brotherhood",
      "description": "Militant technologists who hoard pre-war tech and heavy weapons.",
      "defaultReputation": 0,
      "hostileThreshold": -30,
      "friendlyThreshold": 40,
      "alliedThreshold": 80
    },
    {
      "id": "wasteland_raiders",
      "name": "Wasteland Raiders",
      "description": "Loosely organized bands of scavengers and thugs who take what they want.",
      "defaultReputation": -20,
      "hostileThreshold": -40,
      "friendlyThreshold": 20,
      "alliedThreshold": 60
    },
    {
      "id": "vault_remnants",
      "name": "Vault Remnants",
      "description": "Descendants of vault-dwellers clinging to pre-war order and science.",
      "defaultReputation": 0,
      "hostileThreshold": -40,
      "friendlyThreshold": 35,
      "alliedThreshold": 70
    },
    {
      "id": "cult_of_the_glow",
      "name": "Cult of the Glow",
      "description": "Radiation worshippers who believe the apocalypse was divine purification.",
      "defaultReputation": -10,
      "hostileThreshold": -25,
      "friendlyThreshold": 30,
      "alliedThreshold": 65
    }
  ],
  "defaultRelations": [
    { "factionA": "settlers_alliance", "factionB": "iron_brotherhood", "relation": "neutral" },
    { "factionA": "settlers_alliance", "factionB": "wasteland_raiders", "relation": "hostile" },
    { "factionA": "settlers_alliance", "factionB": "vault_remnants", "relation": "friendly" },
    { "factionA": "settlers_alliance", "factionB": "cult_of_the_glow", "relation": "unfriendly" },
    { "factionA": "iron_brotherhood", "factionB": "wasteland_raiders", "relation": "hostile" },
    { "factionA": "iron_brotherhood", "factionB": "vault_remnants", "relation": "friendly" },
    { "factionA": "iron_brotherhood", "factionB": "cult_of_the_glow", "relation": "hostile" },
    { "factionA": "wasteland_raiders", "factionB": "vault_remnants", "relation": "hostile" },
    { "factionA": "wasteland_raiders", "factionB": "cult_of_the_glow", "relation": "neutral" },
    { "factionA": "vault_remnants", "factionB": "cult_of_the_glow", "relation": "unfriendly" }
  ]
}
```
