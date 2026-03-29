# Step 6 — Crafting & Survival

**Goal:** Crafting stations, recipe system, item degradation, and optional survival mechanics.

**Depends on:** Steps 2, 5

---

## Deliverables

1. `CraftingStationComponent` — station type (Workbench, ChemLab, CookingFire), available recipes
2. Recipe data model: `RecipeDefinition` (inputs, output, station type, skill requirement) — JSON-loaded
3. `CraftingProcessor` — validates ingredients in inventory, skill check, produces output, consumes inputs
4. `DurabilityComponent` — current/max durability on weapons and armor, degrades per use
5. Repair system: repair at station (costs materials) or via NPC service (costs currency)
6. `SurvivalComponent` — hunger (0-100), thirst (0-100), fatigue (0-100) — optional toggle in settings
7. `SurvivalProcessor` — ticks hunger/thirst/fatigue over time, applies debuffs at thresholds
8. `RadiationComponent` — radiation level (0-1000), accumulates in irradiated zones, applies debuffs
9. `RadiationProcessor` — checks player position against radiation zones, increments/decrements exposure
10. Consumable items: food restores hunger, water restores thirst, meds cure radiation / status effects
11. Crafting UI: station interaction screen, recipe list, ingredient check display

---

## Survival Thresholds

| Stat | 0-25 | 26-50 | 51-75 | 76-100 |
|------|------|-------|-------|--------|
| Hunger | Well Fed (+buff) | Normal | Hungry (−Strength) | Starving (HP drain) |
| Thirst | Hydrated (+buff) | Normal | Thirsty (−Perception) | Dehydrated (HP drain) |
| Fatigue | Rested (+AP regen) | Normal | Tired (−Agility) | Exhausted (AP halved) |
