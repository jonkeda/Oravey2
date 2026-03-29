# Step 2 — Character & Inventory

**Goal:** Player entity has stats, skills, perks, health, levelling, and a weight-based inventory.

**Depends on:** Step 1

---

## Deliverables

1. `StatsComponent` — 7 SPECIAL-style attributes (Strength, Perception, Endurance, Charisma, Intelligence, Agility, Luck), range 1-10
2. `SkillsComponent` — Firearms, Melee, Survival, Science, Speech, Stealth, Mechanics (0-100 scale)
3. `HealthComponent` — current/max HP derived from Endurance, radiation level, status effect list
4. `PerkTreeComponent` — perk definitions loaded from JSON, unlock conditions (level + stat requirements)
5. `LevelComponent` — XP tracking, level-up thresholds (quadratic curve), stat/skill point allocation on level-up
6. `InventoryComponent` — item list, max carry weight (derived from Strength), equipped slots
7. Item data model — `ItemDefinition` (id, name, weight, stackable, slot, effects) loaded from JSON
8. `InventoryProcessor` — enforces weight limits, handles equip/unequip, publishes events
9. Character creation data flow — initial stat allocation (point-buy system)
10. Unit tests for stat derivation, levelling math, inventory weight enforcement

---

## Key Formulas (Defaults)

| Formula | Expression |
|---------|-----------|
| Max HP | `50 + (Endurance × 10) + (Level × 5)` |
| Carry Weight | `50 + (Strength × 10)` lbs |
| XP to Level N | `100 × N²` |
| Skill point gain per level | `5 + (Intelligence ÷ 2)` |

---

## Data Files Created

- `Assets/Data/Items/items.json` — master item catalog
- `Assets/Data/Perks/perks.json` — perk definitions
