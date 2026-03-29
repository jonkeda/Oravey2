# Perks Schema

**Asset Path:** `Assets/Data/Perks/perks.json`

---

## Top-Level Structure

```json
{
  "perks": [
    { /* PerkDefinition */ }
  ]
}
```

---

## PerkDefinition

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `id` | `string` | yes | unique, snake_case | Internal identifier |
| `name` | `string` | yes | — | Display name |
| `description` | `string` | yes | — | Tooltip explaining what the perk does |
| `icon` | `string` | no | — | Sprite atlas key |
| `condition` | `PerkCondition` | yes | — | Unlock prerequisites |
| `effects` | `string[]` | yes | coded format | List of mechanical effects |
| `mutuallyExclusive` | `string[]` | no | perk IDs | Cannot unlock if any of these are already unlocked |

---

## PerkCondition

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `requiredLevel` | `integer` | yes | Minimum player level to unlock |
| `requiredStat` | `string` | no | SPECIAL stat name (e.g. `"Strength"`) |
| `statThreshold` | `integer` | if `requiredStat` | Minimum stat value |
| `requiredPerk` | `string` | no | Must have this perk first (for perk chains) |

---

## Effects Format

Effects are strings parsed by `PerkEffectResolver`. Format: `"<type>:<target>:<value>"`

| Format | Example | Description |
|--------|---------|-------------|
| `stat:<stat>:<int>` | `"stat:Strength:+1"` | Permanent stat modifier |
| `skill:<skill>:<int>` | `"skill:Firearms:+10"` | Permanent skill bonus |
| `hp:<int>` | `"hp:+20"` | Permanent max HP bonus |
| `ap:<int>` | `"ap:+2"` | Permanent max AP bonus |
| `ap_regen:<float>` | `"ap_regen:+0.5"` | AP regen per second bonus |
| `carry:<float>` | `"carry:+25"` | Carry weight bonus |
| `crit:<float>` | `"crit:+0.05"` | Critical hit chance bonus |
| `damage:<type>:<float>` | `"damage:melee:+0.15"` | Damage multiplier for weapon type |
| `resist:<type>:<float>` | `"resist:radiation:+0.25"` | Damage/exposure resistance |
| `special:<id>` | `"special:scrounger"` | Unique mechanic handled by code |

---

## Example Perks

```json
{
  "perks": [
    {
      "id": "iron_fist_1",
      "name": "Iron Fist I",
      "description": "Your melee attacks deal 15% more damage.",
      "condition": {
        "requiredLevel": 2,
        "requiredStat": "Strength",
        "statThreshold": 3
      },
      "effects": ["damage:melee:+0.15"]
    },
    {
      "id": "iron_fist_2",
      "name": "Iron Fist II",
      "description": "Your melee attacks deal an additional 15% more damage.",
      "condition": {
        "requiredLevel": 7,
        "requiredStat": "Strength",
        "statThreshold": 5,
        "requiredPerk": "iron_fist_1"
      },
      "effects": ["damage:melee:+0.15"]
    },
    {
      "id": "quick_hands",
      "name": "Quick Hands",
      "description": "Reloading costs 1 less AP.",
      "condition": {
        "requiredLevel": 3,
        "requiredStat": "Agility",
        "statThreshold": 4
      },
      "effects": ["special:quick_hands"]
    },
    {
      "id": "lead_belly",
      "name": "Lead Belly",
      "description": "Take 25% less radiation from food and water.",
      "condition": {
        "requiredLevel": 4,
        "requiredStat": "Endurance",
        "statThreshold": 4
      },
      "effects": ["resist:radiation:+0.25"]
    },
    {
      "id": "silver_tongue",
      "name": "Silver Tongue",
      "description": "+15 to Speech skill. Unlock hidden dialogue options.",
      "condition": {
        "requiredLevel": 3,
        "requiredStat": "Charisma",
        "statThreshold": 5
      },
      "effects": ["skill:Speech:+15", "special:silver_tongue"]
    },
    {
      "id": "scrounger",
      "name": "Scrounger",
      "description": "Find more ammo in containers.",
      "condition": {
        "requiredLevel": 5,
        "requiredStat": "Luck",
        "statThreshold": 5
      },
      "effects": ["special:scrounger"]
    },
    {
      "id": "toughness",
      "name": "Toughness",
      "description": "+20 max HP.",
      "condition": {
        "requiredLevel": 2,
        "requiredStat": "Endurance",
        "statThreshold": 3
      },
      "effects": ["hp:+20"]
    },
    {
      "id": "sniper",
      "name": "Sniper",
      "description": "+5% critical hit chance with ranged weapons.",
      "condition": {
        "requiredLevel": 6,
        "requiredStat": "Perception",
        "statThreshold": 6
      },
      "effects": ["crit:+0.05"]
    }
  ]
}
```
