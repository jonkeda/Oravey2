# Game Constants & Formulas

All balance numbers, formulas, and tuning constants consolidated in one place. These values are used as defaults in code and can be overridden via `Assets/Data/Config/balance.json` for data-driven tuning.

---

## Table of Contents

1. [Character Stats](#1-character-stats)
2. [Levelling & XP](#2-levelling--xp)
3. [Skills](#3-skills)
4. [Combat](#4-combat)
5. [Weapons & Armor](#5-weapons--armor)
6. [Survival](#6-survival)
7. [World & Time](#7-world--time)
8. [AI](#8-ai)
9. [Economy](#9-economy)
10. [UI & Camera](#10-ui--camera)

---

## 1. Character Stats

### SPECIAL Attributes

| Stat | Min | Max | Default (NPC) | Character Creation Pool |
|------|-----|-----|---------------|------------------------|
| Strength | 1 | 10 | 5 | 28 points across all 7 stats |
| Perception | 1 | 10 | 5 | (minimum 1 each = 7 spent, 21 free) |
| Endurance | 1 | 10 | 5 | |
| Charisma | 1 | 10 | 5 | |
| Intelligence | 1 | 10 | 5 | |
| Agility | 1 | 10 | 5 | |
| Luck | 1 | 10 | 5 | |

### Derived Stats

| Derived Stat | Formula | Example (5 End, Level 1) |
|-------------|---------|--------------------------|
| Max HP | $50 + (End \times 10) + (Level \times 5)$ | $50 + 50 + 5 = 105$ |
| Carry Weight | $50 + (Str \times 10)$ lbs | $50 + 50 = 100$ lbs |
| Max AP | $8 + \lfloor Agi / 2 \rfloor$ | $8 + 2 = 10$ |
| AP Regen | $1.5 + (Agi \times 0.1)$ per second | $1.5 + 0.5 = 2.0$ /sec |
| Crit Chance | $Luck \times 0.01$ | $5\% $ |
| Initiative | $Agi + Per / 2$ | Used for turn order priority |

---

## 2. Levelling & XP

| Parameter | Formula / Value |
|-----------|----------------|
| XP to reach level N | $100 \times N^2$ |
| Max Level | 30 |
| Stat points per level | 1 |
| Skill points per level | $5 + \lfloor Int / 2 \rfloor$ |
| Perk available at level | Every 2 levels (2, 4, 6, ...) |

### XP Curve Table

| Level | XP Required | Cumulative XP |
|-------|------------|---------------|
| 1 → 2 | 400 | 400 |
| 2 → 3 | 900 | 1,300 |
| 3 → 4 | 1,600 | 2,900 |
| 4 → 5 | 2,500 | 5,400 |
| 5 → 6 | 3,600 | 9,000 |
| 10 → 11 | 12,100 | 38,500 |
| 15 → 16 | 25,600 | 121,600 |
| 20 → 21 | 44,100 | 282,100 |
| 29 → 30 | 90,000 | 890,000 |

### XP Rewards

| Source | XP Amount |
|--------|-----------|
| Kill enemy (per tier) | $25 \times tier$ |
| Complete quest stage | 50 - 200 (per stage data) |
| Complete quest (bonus) | 100 - 500 (per quest data) |
| Discover new zone | 50 |
| Skill check passed (dialogue) | 25 |
| Lockpick success | 15 × difficulty tier |
| Craft item | 10 |

---

## 3. Skills

### Skill Ranges

| Parameter | Value |
|-----------|-------|
| Minimum | 0 |
| Maximum | 100 |
| Starting value | $10 + (linked\_stat \times 2)$ |

### Skill-to-Stat Links

| Skill | Linked Stat | Starting (stat=5) |
|-------|------------|-------------------|
| Firearms | Perception | 20 |
| Melee | Strength | 20 |
| Survival | Endurance | 20 |
| Science | Intelligence | 20 |
| Speech | Charisma | 20 |
| Stealth | Agility | 20 |
| Mechanics | Intelligence | 20 |

### Use-Based XP

Skills also improve through use. Each successful use grants skill XP.

| Action | Skill XP |
|--------|----------|
| Hit enemy (ranged) | 1 Firearms |
| Hit enemy (melee) | 1 Melee |
| Craft item | 1 Mechanics or Science |
| Persuade in dialogue | 2 Speech |
| Successful sneak | 1 Stealth |
| Harvest / forage | 1 Survival |

Skill-up threshold: $current\_level \times 5$ skill XP to gain +1 skill.

---

## 4. Combat

### Action Point Costs

| Action | AP Cost |
|--------|---------|
| Melee attack | 3 |
| Pistol shot | 2 |
| Rifle shot | 4 |
| Shotgun blast | 3 |
| Reload | 2 |
| Use item (consumable) | 2 |
| Move (per tile) | 1 |
| Take cover | 1 |
| Sprint (per tile) | 0.5 (no attack same turn) |

### Hit Chance

$$
hitChance = weapon.accuracy \times (1.0 + skill / 200) \times coverPenalty \times rangePenalty
$$

| Factor | Value |
|--------|-------|
| Cover penalty (half) | $\times 0.70$ (−30%) |
| Cover penalty (full) | $\times 0.40$ (−60%) |
| Range penalty | $\times \max(0.3,\; 1.0 - distance / (weapon.range \times 1.5))$ |
| Darkness penalty | $\times 0.85$ (−15%) at Night |

### Damage Formula

$$
damage = weapon.damage \times (1.0 + skill / 100) \times critMultiplier - armor.damageReduction
$$

| Parameter | Value |
|-----------|-------|
| Minimum damage | 1 (always at least 1 on hit) |
| Critical hit base chance | $Luck \times 0.01$ |
| Critical multiplier | Per weapon (default 2.0) |
| Headshot multiplier | 1.5 |
| Limb cripple threshold | 25% of enemy max HP in one hit |

### Hit Location Table

| Location | Probability | Damage Modifier |
|----------|-------------|----------------|
| Torso | 40% | ×1.0 |
| Head | 10% | ×1.5 |
| Arms | 25% | ×0.8 |
| Legs | 25% | ×0.8 |

### Combat State Transitions

| Trigger | Effect |
|---------|--------|
| Enemy enters aggro range | GameState → InCombat |
| All enemies dead or fled | GameState → Exploring |
| Player flees beyond leash | GameState → Exploring |
| Player or enemy HP ≤ 0 | EntityDiedEvent |

---

## 5. Weapons & Armor

### Weapon Defaults

| Weapon Type | Damage | Range | AP | Accuracy | Ammo | Fire Rate | Crit |
|-------------|--------|-------|----|---------| -----|-----------|------|
| Pipe Pistol | 12 | 15 | 2 | 0.65 | 9mm | 2.0 | 2.0 |
| Hunting Rifle | 25 | 30 | 4 | 0.75 | .308 | 0.8 | 2.5 |
| Shotgun | 35 | 8 | 3 | 0.50 | shells | 1.2 | 1.5 |
| Combat Knife | 8 | 1 | 3 | 0.90 | — | — | 1.5 |
| Baseball Bat | 15 | 1.5 | 3 | 0.85 | — | — | 2.0 |
| Pipe Rifle | 18 | 20 | 3 | 0.70 | 9mm | 1.5 | 2.0 |

### Durability

| Parameter | Default |
|-----------|---------|
| Weapon durability | 100-200 |
| Armor durability | 100-200 |
| Degrade per melee swing | 1.0 |
| Degrade per shot fired | 0.5-2.0 |
| Degrade per hit taken (armor) | 1.0-2.0 |
| Broken penalty | Weapon: −50% damage; Armor: 0 DR |

### Repair Costs

| Method | Cost |
|--------|------|
| Self-repair (workbench) | 1 repair kit or 3 scrap metal per 50 durability |
| NPC repair | 10 caps per 25 durability |

---

## 6. Survival

### Need Decay Rates (per in-game hour)

| Need | Rate | Notes |
|------|------|-------|
| Hunger | +2.0 | 0 = full, 100 = starving |
| Thirst | +3.0 | Dehydrates faster than hunger |
| Fatigue | +1.5 | Sleeping 8 hours resets to 0 |

### Survival Thresholds & Effects

| Stat | 0-25 | 26-50 | 51-75 | 76-100 |
|------|------|-------|-------|--------|
| Hunger | Well Fed (+1 Str) | Normal | Hungry (−1 Str) | Starving (−2 HP/min) |
| Thirst | Hydrated (+1 Per) | Normal | Thirsty (−1 Per) | Dehydrated (−3 HP/min) |
| Fatigue | Rested (+1 AP regen) | Normal | Tired (−1 Agi) | Exhausted (AP halved) |

### Radiation

| Parameter | Value |
|-----------|-------|
| Radiation range | 0 - 1000 |
| Rad poisoning threshold | 200 (−1 End) |
| Severe rad poisoning | 500 (−2 End, −1 Str) |
| Critical radiation | 800 (−3 End, −2 Str, HP drain) |
| Lethal radiation | 1000 (death) |
| Natural decay | −1 rad/min (outside irradiated zones) |
| Rad-Away effect | −100 rads instantly |

---

## 7. World & Time

### Day/Night Cycle

| Parameter | Value |
|-----------|-------|
| Full day cycle | 24 in-game hours |
| Real seconds per in-game hour | 120 (configurable) |
| Full day real time | 48 minutes |
| Dawn start | 06:00 |
| Day start | 07:00 |
| Dusk start | 20:00 |
| Night start | 21:00 |

### World Dimensions

| Parameter | Value |
|-----------|-------|
| Tile size | 1.0 world units |
| Chunk size | 16 × 16 tiles |
| Active chunk grid | 3 × 3 (9 chunks loaded) |
| Visible area | 48 × 48 tiles |
| World size (default) | 16 × 12 chunks (256 × 192 tiles) |
| Fast travel time cost | $distance \div 10$ in-game hours |

### Chunk Streaming

| Parameter | Value |
|-----------|-------|
| Load radius trigger | Player enters new chunk → recalculate 3×3 |
| Async load budget | 1 chunk per frame max |
| Unload delay | 2 frames after leaving active grid |
| Entity pool size | 100 (recycled across chunk loads) |

---

## 8. AI

### Detection

| Parameter | Default (Combat) | Default (Civilian) |
|-----------|-------------------|---------------------|
| Sight range | 20 tiles | 10 tiles |
| Sight cone angle | 120° | 180° |
| Hearing radius | 12 tiles | 8 tiles |
| Aggro range | 15 tiles | N/A |
| Leash range | 25 tiles | N/A |

### AI Timing

| Parameter | Value |
|-----------|-------|
| Utility re-evaluation | Every 0.5 seconds |
| Pathfinding recalc | Every 1.0 seconds or on target move > 3 tiles |
| Alert decay | 10 seconds after losing sight |
| Investigation duration | 15 seconds at last known position |
| Group coordination tick | Every 1.0 seconds |

### Utility Weights (Combat AI)

| Action | Consideration | Weight |
|--------|---------------|--------|
| Attack | `weapon_available` | 0.3 |
| | `target_in_range` | 0.4 |
| | `health_ok (> 30%)` | 0.3 |
| Flee | `low_health (< 25%)` | 0.5 |
| | `outnumbered` | 0.3 |
| | `no_ammo` | 0.2 |
| Take Cover | `under_fire` | 0.4 |
| | `cover_nearby` | 0.4 |
| | `health_low (< 50%)` | 0.2 |
| Investigate | `heard_noise` | 0.5 |
| | `lost_target` | 0.5 |
| Patrol | `no_threats` | 0.6 |
| | `at_waypoint` | 0.4 |

### Group Tactics

| Parameter | Value |
|-----------|-------|
| Flank angle threshold | 45° from target forward |
| Focus fire threshold | 3+ allies targeting same enemy |
| Retreat threshold | 50% of group dead/fled |

---

## 9. Economy

### Currency

| Unit | Name |
|------|------|
| Base currency | Caps |
| Starting caps | 50 |

### Trading

| Parameter | Value |
|-----------|-------|
| Base buy price | Item `value` × 1.5 |
| Base sell price | Item `value` × 0.5 |
| Friendly faction discount | −15% buy, +10% sell |
| Allied faction discount | −25% buy, +20% sell |
| Speech skill bonus | −1% buy per 5 Speech above 20 |

### Vendor Restock

| Parameter | Value |
|-----------|-------|
| Restock interval | 72 in-game hours (3 days) |
| Vendor cap refresh | 200-500 caps (per vendor tier) |

---

## 10. UI & Camera

### Camera

| Parameter | Default |
|-----------|---------|
| Pitch | 30° |
| Yaw | 45° |
| Projection | Orthographic |
| Distance | 20 units |
| Zoom min | 10 |
| Zoom max | 40 |
| Zoom speed | 2 units/scroll tick |
| Follow smoothing | 5 (lerp factor) |
| Deadzone | 0.5 units |
| Rotation snap | 90° |

### HUD

| Element | Position |
|---------|----------|
| Health bar | Top-left |
| AP bar | Below health |
| Minimap | Top-right |
| Quest tracker | Right edge, below minimap |
| Quick slot bar | Bottom-center |
| Compass | Top-center (optional) |

### Floating Text

| Parameter | Value |
|-----------|-------|
| Damage number rise speed | 2 units/sec |
| Damage number fade time | 1.0 sec |
| Crit damage colour | Gold |
| Heal number colour | Green |
| XP gain colour | Cyan |
