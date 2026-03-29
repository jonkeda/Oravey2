# Recipes Schema

**Asset Path:** `Assets/Data/Recipes/recipes.json`

---

## Top-Level Structure

```json
{
  "recipes": [
    { /* RecipeDefinition */ }
  ]
}
```

---

## RecipeDefinition

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `id` | `string` | yes | unique, snake_case | Internal identifier |
| `name` | `string` | yes | — | Display name |
| `description` | `string` | no | — | Flavour text / crafting notes |
| `outputItemId` | `string` | yes | valid item ID | Produced item |
| `outputCount` | `integer` | yes | ≥ 1 | Quantity produced |
| `ingredients` | `object` | yes | itemId → count | Required items consumed |
| `requiredStation` | `string` | yes | enum below | Crafting station type |
| `requiredSkill` | `string` | no | SkillType name | Skill gating |
| `skillThreshold` | `integer` | if `requiredSkill` | 0-100 | Minimum skill |
| `discoveredBy` | `string` | no | — | How the recipe is learned |
| `category` | `string` | no | — | UI grouping: `"weapons"`, `"armor"`, `"chems"`, `"food"`, `"ammo"` |

### StationType Enum

```
"workbench", "chem_lab", "cooking_fire"
```

### DiscoveredBy Values

| Value | Meaning |
|-------|---------|
| `"default"` | Known from the start |
| `"schematic:<itemId>"` | Learned by using a schematic item |
| `"npc:<npcId>"` | Taught by an NPC via dialogue |
| `"experiment"` | Unlocked by attempting to craft with correct ingredients |

---

## Example Recipes

```json
{
  "recipes": [
    {
      "id": "craft_stimpak",
      "name": "Craft Stimpak",
      "description": "Combine antiseptic and a syringe to create a basic healing stimpak.",
      "outputItemId": "stimpak",
      "outputCount": 1,
      "ingredients": {
        "antiseptic": 1,
        "empty_syringe": 1
      },
      "requiredStation": "chem_lab",
      "requiredSkill": "Science",
      "skillThreshold": 20,
      "discoveredBy": "default",
      "category": "chems"
    },
    {
      "id": "craft_pipe_pistol",
      "name": "Assemble Pipe Pistol",
      "description": "Cobble together a functional pistol from scrap.",
      "outputItemId": "pipe_pistol",
      "outputCount": 1,
      "ingredients": {
        "scrap_metal": 5,
        "spring": 2,
        "adhesive": 1
      },
      "requiredStation": "workbench",
      "requiredSkill": "Mechanics",
      "skillThreshold": 15,
      "discoveredBy": "default",
      "category": "weapons"
    },
    {
      "id": "craft_ammo_9mm",
      "name": "Reload 9mm Rounds",
      "description": "Repack shell casings with gunpowder and lead.",
      "outputItemId": "ammo_9mm",
      "outputCount": 10,
      "ingredients": {
        "shell_casing": 5,
        "gunpowder": 2,
        "lead": 1
      },
      "requiredStation": "workbench",
      "requiredSkill": "Mechanics",
      "skillThreshold": 25,
      "discoveredBy": "default",
      "category": "ammo"
    },
    {
      "id": "cook_grilled_meat",
      "name": "Grilled Meat",
      "description": "Cook raw meat over a fire for a filling meal.",
      "outputItemId": "grilled_meat",
      "outputCount": 1,
      "ingredients": {
        "raw_meat": 1,
        "wood": 1
      },
      "requiredStation": "cooking_fire",
      "requiredSkill": "Survival",
      "skillThreshold": 5,
      "discoveredBy": "default",
      "category": "food"
    },
    {
      "id": "craft_rad_away",
      "name": "Brew Rad-Away",
      "description": "A chemical solution that purges radiation. Requires real chemistry knowledge.",
      "outputItemId": "rad_away",
      "outputCount": 1,
      "ingredients": {
        "antiseptic": 2,
        "glowing_fungus": 1,
        "purified_water": 1
      },
      "requiredStation": "chem_lab",
      "requiredSkill": "Science",
      "skillThreshold": 40,
      "discoveredBy": "schematic:rad_away_formula",
      "category": "chems"
    },
    {
      "id": "repair_kit",
      "name": "Field Repair Kit",
      "description": "Compact toolkit for patching up gear in the field.",
      "outputItemId": "repair_kit",
      "outputCount": 1,
      "ingredients": {
        "scrap_metal": 3,
        "adhesive": 2,
        "cloth": 1
      },
      "requiredStation": "workbench",
      "requiredSkill": "Mechanics",
      "skillThreshold": 30,
      "discoveredBy": "npc:mechanic_jill",
      "category": "misc"
    }
  ]
}
```
