# Zones Schema

**Asset Path:** `Assets/Data/Zones/zones.json`

---

## Top-Level Structure

```json
{
  "zones": [
    { /* ZoneDefinition */ }
  ]
}
```

---

## ZoneDefinition

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `id` | `string` | yes | unique, snake_case | Internal identifier |
| `name` | `string` | yes | — | Display name on map |
| `description` | `string` | no | — | Lore text shown on discovery |
| `biomeType` | `string` | yes | enum below | Visual / audio theme |
| `radiationLevel` | `number` | yes | 0.0-10.0 | Rads per second when in zone |
| `enemyDifficultyTier` | `integer` | yes | 1-5 | Scales enemy levels |
| `isFastTravelTarget` | `boolean` | yes | — | Can player fast-travel here? |
| `ambientAudioId` | `string` | no | — | Ambient sound loop ID |
| `musicTheme` | `string` | no | — | Zone-specific music track |
| `weatherBias` | `string` | no | WeatherState name | More likely weather |
| `chunkRange` | `object` | yes | — | Which chunks belong to this zone |

### BiomeType Enum

```
"ruined_city", "wasteland", "bunker", "settlement",
"industrial", "forest_overgrown", "irradiated_crater",
"underground", "coastal"
```

### ChunkRange

```json
{
  "startX": 0,
  "startY": 0,
  "endX": 3,
  "endY": 2
}
```

Inclusive range: chunks from `(startX, startY)` to `(endX, endY)`.

---

## Example

```json
{
  "zones": [
    {
      "id": "haven_settlement",
      "name": "Haven",
      "description": "A fortified settlement built in the shell of a pre-war shopping mall.",
      "biomeType": "settlement",
      "radiationLevel": 0.0,
      "enemyDifficultyTier": 1,
      "isFastTravelTarget": true,
      "ambientAudioId": "amb_settlement",
      "musicTheme": "music_haven",
      "chunkRange": { "startX": 4, "startY": 4, "endX": 5, "endY": 5 }
    },
    {
      "id": "downtown_ruins",
      "name": "Downtown Ruins",
      "description": "Crumbling skyscrapers and collapsed highways. Raiders lurk in every shadow.",
      "biomeType": "ruined_city",
      "radiationLevel": 0.5,
      "enemyDifficultyTier": 2,
      "isFastTravelTarget": true,
      "ambientAudioId": "amb_ruins",
      "musicTheme": "music_urban_decay",
      "chunkRange": { "startX": 6, "startY": 3, "endX": 9, "endY": 6 }
    },
    {
      "id": "the_glow",
      "name": "The Glow",
      "description": "A massive irradiated crater surrounding the old power plant. The Cult calls it holy ground.",
      "biomeType": "irradiated_crater",
      "radiationLevel": 5.0,
      "enemyDifficultyTier": 4,
      "isFastTravelTarget": false,
      "ambientAudioId": "amb_radiation",
      "musicTheme": "music_eerie",
      "weatherBias": "foggy",
      "chunkRange": { "startX": 10, "startY": 0, "endX": 12, "endY": 2 }
    },
    {
      "id": "bunker_seven",
      "name": "Bunker Seven",
      "description": "A sealed pre-war military installation. What lies inside is anyone's guess.",
      "biomeType": "bunker",
      "radiationLevel": 0.0,
      "enemyDifficultyTier": 3,
      "isFastTravelTarget": false,
      "ambientAudioId": "amb_bunker",
      "musicTheme": "music_tension",
      "chunkRange": { "startX": 2, "startY": 8, "endX": 2, "endY": 8 }
    },
    {
      "id": "northern_outpost",
      "name": "Northern Outpost",
      "description": "A Settler's Alliance outpost on the edge of raider territory.",
      "biomeType": "settlement",
      "radiationLevel": 0.0,
      "enemyDifficultyTier": 2,
      "isFastTravelTarget": true,
      "ambientAudioId": "amb_settlement",
      "chunkRange": { "startX": 5, "startY": 0, "endX": 5, "endY": 1 }
    }
  ]
}
```
