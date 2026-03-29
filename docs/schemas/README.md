# Data Schemas

JSON schema definitions for all data-driven content in Oravey2.

Each file below defines the exact JSON format that the game's loader will deserialize into the corresponding C# records/classes from the [Class Architecture](../CLASS_ARCHITECTURE.md).

| File | C# Target | Used By Steps |
|------|-----------|---------------|
| [items.md](items.md) | `ItemDefinition`, `WeaponDefinition`, `ArmorDefinition` | 2, 3, 6 |
| [perks.md](perks.md) | `PerkDefinition`, `PerkCondition` | 2 |
| [factions.md](factions.md) | `FactionDefinition` | 2, 5 |
| [dialogues.md](dialogues.md) | `DialogueTree`, `DialogueNode`, `DialogueChoice` | 5 |
| [quests.md](quests.md) | `QuestDefinition`, `QuestStage` | 5 |
| [recipes.md](recipes.md) | `RecipeDefinition` | 6 |
| [zones.md](zones.md) | `ZoneDefinition`, `ChunkData` | 7 |
| [world.md](world.md) | `WorldMapData`, entity spawn data | 7 |
