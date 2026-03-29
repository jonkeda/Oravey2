# World Map Schema

**Asset Path:** `Assets/Data/World/world.json` + `Assets/Data/World/chunks/<cx>_<cy>.json`

---

## World Definition

`world.json` defines the overall map grid and metadata.

```json
{
  "chunksWide": 16,
  "chunksHigh": 12,
  "tileSize": 1.0,
  "chunkTileSize": 16,
  "startChunkX": 4,
  "startChunkY": 4,
  "startPosition": { "x": 8.0, "y": 0.0, "z": 8.0 }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `chunksWide` | `integer` | World width in chunks |
| `chunksHigh` | `integer` | World height in chunks |
| `tileSize` | `number` | World units per tile |
| `chunkTileSize` | `integer` | Tiles per chunk side (always 16) |
| `startChunkX` | `integer` | Player starting chunk X |
| `startChunkY` | `integer` | Player starting chunk Y |
| `startPosition` | `Vector3` | Player start position within starting chunk |

---

## Chunk File

Each chunk is a separate file at `chunks/<cx>_<cy>.json`. Only chunks with content need files; missing files default to empty wasteland.

```json
{
  "chunkX": 4,
  "chunkY": 4,
  "tiles": [ /* 2D array, row-major */ ],
  "entities": [ /* EntitySpawnInfo[] */ ],
  "containers": [ /* ContainerSpawn[] */ ]
}
```

### Tile Layer

`tiles` is a 16×16 2D array of tile type integers matching the `TileType` enum.

```json
{
  "tiles": [
    [1, 1, 1, 2, 2, 1, 1, 1, 1, 1, 1, 1, 5, 5, 1, 1],
    [1, 1, 2, 2, 2, 2, 1, 1, 3, 3, 1, 1, 5, 5, 1, 1]
  ]
}
```

| Value | TileType |
|-------|----------|
| 0 | Empty |
| 1 | Ground |
| 2 | Road |
| 3 | Rubble |
| 4 | Water |
| 5 | Wall |

---

## EntitySpawnInfo

Defines an entity to spawn when the chunk loads.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `prefabId` | `string` | yes | Entity template / prefab identifier |
| `position` | `Vector3` | yes | Local position within chunk |
| `rotation` | `number` | no | Y-axis rotation in degrees (default: 0) |
| `faction` | `string` | no | Faction ID for NPCs |
| `level` | `integer` | no | Override level (else uses zone tier) |
| `dialogueId` | `string` | no | Dialogue tree ID (for NPCs) |
| `lootTable` | `string` | no | Loot table ID |
| `persistent` | `boolean` | no | If `true`, death/state saved permanently |
| `conditionFlag` | `string` | no | Only spawn if this world flag is `false` |

### Example

```json
{
  "entities": [
    {
      "prefabId": "npc_merchant",
      "position": { "x": 7.0, "y": 0.0, "z": 5.0 },
      "rotation": 180,
      "faction": "settlers_alliance",
      "dialogueId": "merchant_intro",
      "persistent": true
    },
    {
      "prefabId": "enemy_raider",
      "position": { "x": 12.0, "y": 0.0, "z": 3.0 },
      "faction": "wasteland_raiders",
      "lootTable": "loot_raider_basic",
      "persistent": false
    },
    {
      "prefabId": "env_campfire",
      "position": { "x": 7.0, "y": 0.0, "z": 4.5 },
      "persistent": true
    }
  ]
}
```

---

## ContainerSpawn

Lootable containers placed in the chunk.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `containerId` | `string` | yes | Unique within chunk, used for save state |
| `prefabId` | `string` | yes | Visual template (`"container_trash"`, `"container_safe"`, etc.) |
| `position` | `Vector3` | yes | Local position within chunk |
| `lootTable` | `string` | yes | Loot table to roll from |
| `locked` | `boolean` | no | Requires lockpick skill check |
| `lockDifficulty` | `integer` | if `locked` | Mechanics skill threshold |
| `conditionFlag` | `string` | no | Only available if flag is `false` |

### Example

```json
{
  "containers": [
    {
      "containerId": "safe_01",
      "prefabId": "container_safe",
      "position": { "x": 3.0, "y": 0.0, "z": 8.0 },
      "lootTable": "loot_safe_tier2",
      "locked": true,
      "lockDifficulty": 40
    },
    {
      "containerId": "trash_01",
      "prefabId": "container_trash",
      "position": { "x": 10.0, "y": 0.0, "z": 2.0 },
      "lootTable": "loot_junk"
    }
  ]
}
```

---

## Loot Table Schema (referenced by entities and containers)

**Asset Path:** `Assets/Data/LootTables/loot_tables.json`

```json
{
  "lootTables": {
    "loot_raider_basic": {
      "rolls": 2,
      "entries": [
        { "itemId": "ammo_9mm", "count": [5, 15], "weight": 40 },
        { "itemId": "stimpak", "count": [1, 1], "weight": 15 },
        { "itemId": "caps", "count": [5, 25], "weight": 30 },
        { "itemId": "scrap_metal", "count": [1, 3], "weight": 15 }
      ]
    },
    "loot_safe_tier2": {
      "rolls": 3,
      "entries": [
        { "itemId": "caps", "count": [20, 80], "weight": 30 },
        { "itemId": "stimpak", "count": [1, 3], "weight": 20 },
        { "itemId": "ammo_9mm", "count": [10, 30], "weight": 25 },
        { "itemId": "combat_knife", "count": [1, 1], "weight": 10 },
        { "itemId": "rad_away", "count": [1, 2], "weight": 15 }
      ]
    },
    "loot_junk": {
      "rolls": 1,
      "entries": [
        { "itemId": "scrap_metal", "count": [1, 3], "weight": 40 },
        { "itemId": "cloth", "count": [1, 2], "weight": 30 },
        { "itemId": "adhesive", "count": [1, 1], "weight": 20 },
        { "itemId": "caps", "count": [1, 5], "weight": 10 }
      ]
    }
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `rolls` | `integer` | Number of times to roll on the table |
| `entries[].itemId` | `string` | Item to grant |
| `entries[].count` | `[min, max]` | Random quantity range (inclusive) |
| `entries[].weight` | `integer` | Relative probability weight |
