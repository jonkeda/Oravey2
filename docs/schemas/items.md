# Items Schema

**Asset Path:** `Assets/Data/Items/items.json`

---

## Master Item Catalog

Top-level structure is an array of item definitions.

```json
{
  "items": [
    { /* ItemDefinition */ }
  ]
}
```

---

## ItemDefinition

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `id` | `string` | yes | unique, snake_case | Internal identifier |
| `name` | `string` | yes | — | Display name |
| `description` | `string` | yes | — | Tooltip / inspect text |
| `category` | `string` | yes | enum below | Classification for UI sorting |
| `weight` | `number` | yes | ≥ 0 | Weight in lbs |
| `stackable` | `boolean` | yes | — | Can multiple occupy one slot |
| `maxStack` | `integer` | if stackable | 1-999 | Maximum per stack |
| `value` | `integer` | yes | ≥ 0 | Base trade value (caps) |
| `slot` | `string` | no | enum below | Equipment slot, `null` for non-equippable |
| `effects` | `object` | no | key-value pairs | Generic effects dictionary |
| `weapon` | `WeaponData` | no | — | Present only for weapons |
| `armor` | `ArmorData` | no | — | Present only for armor |
| `durability` | `DurabilityData` | no | — | Present only for degradable items |
| `icon` | `string` | no | — | Sprite atlas key |
| `tags` | `string[]` | no | — | Arbitrary tags: `"junk"`, `"quest"`, `"rare"` |

### Category Enum

```
"weapon_melee", "weapon_ranged", "armor",
"consumable", "ammo", "crafting_material",
"quest_item", "junk", "schematic"
```

### EquipmentSlot Enum

```
"head", "torso", "legs", "feet",
"primary_weapon", "secondary_weapon",
"accessory_1", "accessory_2"
```

---

## WeaponData (embedded in ItemDefinition)

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `damage` | `integer` | yes | > 0 | Base damage per hit |
| `range` | `number` | yes | > 0 | Effective range in tiles |
| `apCost` | `integer` | yes | 1-10 | AP per attack |
| `accuracy` | `number` | yes | 0.0-1.0 | Base hit chance at optimal range |
| `ammoType` | `string` | no | — | Required ammo item ID, `null` for melee |
| `fireRate` | `number` | no | > 0 | Attacks per second (auto weapons) |
| `critMultiplier` | `number` | no | ≥ 1.0 | Critical hit damage multiplier (default: 2.0) |
| `skillType` | `string` | yes | `"firearms"` or `"melee"` | Which skill governs this weapon |

---

## ArmorData (embedded in ItemDefinition)

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `damageReduction` | `integer` | yes | ≥ 0 | Flat damage absorbed per hit |
| `coverageZones` | `object` | yes | zone→float | Body zone coverage ratios, sum ≤ 1.0 |

### Coverage Zones

```json
{
  "head": 0.2,
  "torso": 0.5,
  "arms": 0.15,
  "legs": 0.15
}
```

---

## DurabilityData (embedded in ItemDefinition)

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `maxDurability` | `integer` | yes | > 0 | Starting / max durability |
| `degradePerUse` | `number` | yes | > 0 | Durability lost per use |

---

## Effects Dictionary

Generic key-value pairs interpreted at runtime. Keys are strings; values are strings parsed by effect handlers.

| Key | Value Format | Example | Description |
|-----|-------------|---------|-------------|
| `heal` | `"<int>"` | `"20"` | Restore HP |
| `radReduction` | `"<int>"` | `"50"` | Reduce radiation |
| `hungerRestore` | `"<float>"` | `"25.0"` | Reduce hunger stat |
| `thirstRestore` | `"<float>"` | `"30.0"` | Reduce thirst stat |
| `fatigueRestore` | `"<float>"` | `"20.0"` | Reduce fatigue stat |
| `cureEffect` | `"<status>"` | `"Poisoned"` | Remove a status effect |
| `statBuff` | `"<stat>:<amount>:<duration>"` | `"Strength:2:300"` | Temporary stat boost (seconds) |
| `apBoost` | `"<float>"` | `"0.5"` | Temporary AP regen bonus |

---

## Example Items

```json
{
  "items": [
    {
      "id": "pipe_pistol",
      "name": "Pipe Pistol",
      "description": "A crude pistol fashioned from scrap pipe and springs.",
      "category": "weapon_ranged",
      "weight": 3.5,
      "stackable": false,
      "maxStack": 1,
      "value": 50,
      "slot": "primary_weapon",
      "tags": ["common"],
      "weapon": {
        "damage": 12,
        "range": 15,
        "apCost": 2,
        "accuracy": 0.65,
        "ammoType": "ammo_9mm",
        "fireRate": 2.0,
        "critMultiplier": 2.0,
        "skillType": "firearms"
      },
      "durability": {
        "maxDurability": 100,
        "degradePerUse": 2.0
      }
    },
    {
      "id": "combat_knife",
      "name": "Combat Knife",
      "description": "A sturdy military-issue blade.",
      "category": "weapon_melee",
      "weight": 1.0,
      "stackable": false,
      "maxStack": 1,
      "value": 30,
      "slot": "primary_weapon",
      "tags": ["common"],
      "weapon": {
        "damage": 8,
        "range": 1,
        "apCost": 3,
        "accuracy": 0.9,
        "ammoType": null,
        "critMultiplier": 1.5,
        "skillType": "melee"
      },
      "durability": {
        "maxDurability": 150,
        "degradePerUse": 1.0
      }
    },
    {
      "id": "leather_vest",
      "name": "Leather Vest",
      "description": "Thick leather offering modest protection.",
      "category": "armor",
      "weight": 5.0,
      "stackable": false,
      "maxStack": 1,
      "value": 40,
      "slot": "torso",
      "tags": ["common"],
      "armor": {
        "damageReduction": 3,
        "coverageZones": { "torso": 0.6, "arms": 0.2 }
      },
      "durability": {
        "maxDurability": 120,
        "degradePerUse": 1.5
      }
    },
    {
      "id": "stimpak",
      "name": "Stimpak",
      "description": "A quick-inject medical syringe that restores health.",
      "category": "consumable",
      "weight": 0.5,
      "stackable": true,
      "maxStack": 20,
      "value": 25,
      "slot": null,
      "effects": {
        "heal": "30"
      },
      "tags": ["medical"]
    },
    {
      "id": "canned_food",
      "name": "Canned Food",
      "description": "Pre-war tinned meat. Still edible. Probably.",
      "category": "consumable",
      "weight": 0.8,
      "stackable": true,
      "maxStack": 10,
      "value": 8,
      "slot": null,
      "effects": {
        "hungerRestore": "25.0"
      },
      "tags": ["food"]
    },
    {
      "id": "purified_water",
      "name": "Purified Water",
      "description": "Clean drinking water. Worth its weight in gold.",
      "category": "consumable",
      "weight": 0.5,
      "stackable": true,
      "maxStack": 20,
      "value": 15,
      "slot": null,
      "effects": {
        "thirstRestore": "30.0"
      },
      "tags": ["drink"]
    },
    {
      "id": "rad_away",
      "name": "Rad-Away",
      "description": "Intravenous solution that purges radiation.",
      "category": "consumable",
      "weight": 0.5,
      "stackable": true,
      "maxStack": 10,
      "value": 30,
      "slot": null,
      "effects": {
        "radReduction": "100"
      },
      "tags": ["medical"]
    },
    {
      "id": "ammo_9mm",
      "name": "9mm Rounds",
      "description": "Standard 9mm pistol ammunition.",
      "category": "ammo",
      "weight": 0.02,
      "stackable": true,
      "maxStack": 200,
      "value": 1,
      "slot": null,
      "tags": ["ammo"]
    },
    {
      "id": "scrap_metal",
      "name": "Scrap Metal",
      "description": "Bent metal pieces. Useful for repairs and crafting.",
      "category": "crafting_material",
      "weight": 1.0,
      "stackable": true,
      "maxStack": 50,
      "value": 3,
      "slot": null,
      "tags": ["junk", "material"]
    },
    {
      "id": "mysterious_key",
      "name": "Mysterious Key",
      "description": "An ornate key with strange markings. Someone must want this.",
      "category": "quest_item",
      "weight": 0.1,
      "stackable": false,
      "maxStack": 1,
      "value": 0,
      "slot": null,
      "tags": ["quest"]
    }
  ]
}
```
