# State Machine Diagrams

Every finite state machine in Oravey2, with states, transitions, guards, and side-effects.

---

## Table of Contents

1. [GameState (Master)](#1-gamestate-master)
2. [AI Combat State](#2-ai-combat-state)
3. [AI Civilian State](#3-ai-civilian-state)
4. [Quest Status](#4-quest-status)
5. [Combat Action State (per entity)](#5-combat-action-state-per-entity)
6. [Day Phase Cycle](#6-day-phase-cycle)
7. [Weather Cycle](#7-weather-cycle)
8. [Dialogue State](#8-dialogue-state)
9. [UI Screen Stack](#9-ui-screen-stack)
10. [Player Status Effects](#10-player-status-effects)

---

## 1. GameState (Master)

Controls the top-level mode of the game. Only one state active at a time.

```
                    ┌──────────┐
                    │ Loading  │
                    └────┬─────┘
                         │ assets loaded
                         ▼
              ┌──────────────────────┐
         ┌───►│     Exploring        │◄───┐
         │    └──┬──────┬──────┬─────┘    │
         │       │      │      │          │
         │  enemy│ talk │  ESC │    close │
         │  aggro│ NPC  │      │    menu  │
         │       ▼      ▼      ▼          │
         │  ┌────────┐┌─────────┐┌──────┐ │
         │  │InCombat││InDialogue││InMenu│─┘
         │  └──┬─────┘└────┬────┘└──────┘
         │     │           │
         │  all│       dialogue
         │  dead        ends
         │     │           │
         └─────┘           │
         └─────────────────┘

         Any state ──→ Paused (pause key)
         Paused ──→ previous state (unpause)
```

### Transition Table

| From | To | Guard / Trigger | Side Effect |
|------|----|----------------|-------------|
| Loading | Exploring | All assets loaded | Spawn player, start systems |
| Exploring | InCombat | Enemy enters aggro range | Publish CombatStartedEvent |
| Exploring | InDialogue | Player interact + NPC has DialogueComponent | Publish DialogueStartedEvent |
| Exploring | InMenu | ESC / Pause / Inventory key | Push menu screen |
| InCombat | Exploring | All enemies dead or fled | Publish CombatEndedEvent |
| InDialogue | Exploring | Dialogue tree ends | Publish DialogueEndedEvent |
| InMenu | Exploring | Menu closed | Pop screen |
| Any | Paused | Pause key (not in Loading) | Freeze all processors |
| Paused | Previous | Unpause key | Resume all processors |

### Invalid Transitions

- InCombat → InDialogue (cannot talk mid-combat)
- InCombat → InMenu (must pause first)
- InDialogue → InCombat (dialogue has implicit safety)
- Loading → anything except Exploring

---

## 2. AI Combat State

Per-entity FSM for combat-type AI (`AIBehaviorComponent.BehaviorType == Combat`).

```
         ┌───────┐
         │ Idle  │
         └──┬────┘
            │ no threats for 30s
            ▼
         ┌───────┐     heard noise     ┌─────────┐
         │Patrol │ ──────────────────► │  Alert  │
         └──┬────┘                     └──┬───┬──┘
            │                             │   │
            │ see enemy                   │   │ 10s no contact
            ▼                             │   ▼
         ┌───────┐◄────────────────────┘ ┌──────┐
         │Engage │                        │Patrol│
         └──┬────┘                        └──────┘
            │
            │ HP < 25% OR outnumbered
            ▼
         ┌───────┐
         │ Flee  │
         └──┬────┘
            │ reached safe distance OR HP recovered
            ▼
         ┌───────┐
         │ Idle  │
         └───────┘
```

### Transition Table

| From | To | Guard | Utility Score |
|------|----|-------|--------------|
| Idle | Patrol | No threats, has waypoints | patrol > 0.5 |
| Patrol | Alert | Noise heard (HearingSensor) | investigate > current |
| Patrol | Engage | Enemy within sight (SightSensor) | attack > 0.5 |
| Alert | Engage | Target confirmed (SightSensor) | attack > investigate |
| Alert | Patrol | 10s timeout, no target found | patrol > investigate |
| Engage | Flee | HP < 25% OR allies < 50% | flee > attack |
| Engage | Alert | Target lost (behind wall, > leash) | — |
| Flee | Idle | Beyond leash range + safe | — |
| Any | Idle | Group coordinator calls retreat | — |

---

## 3. AI Civilian State

Per-entity FSM for civilian NPCs (`AIBehaviorType.Civilian`). Schedule-driven.

```
         ┌───────┐
         │ Sleep │ (Night: 21:00 - 06:00)
         └──┬────┘
            │ 06:00 (Dawn)
            ▼
         ┌───────┐
         │ Work  │ (Day: 07:00 - 12:00, 13:00 - 19:00)
         └──┬──┬─┘
            │  │
    12:00   │  │ 19:00
            ▼  │
         ┌────┐│
         │Eat ││
         └──┬─┘│
    13:00   │  │
            ▼  ▼
         ┌───────┐
         │Wander │ (Dusk: 19:00 - 21:00, Lunch break)
         └──┬────┘
            │ 21:00
            ▼
         ┌───────┐
         │ Sleep │
         └───────┘

   Interrupt: threat detected ──►  ┌──────┐
                                   │ Flee │ (run to safe point)
                                   └──┬───┘
                                      │ threat gone
                                      ▼
                                   (resume previous)
```

### Schedule Table

| Phase | Time Range | State | Waypoint |
|-------|-----------|-------|----------|
| Dawn | 06:00 - 07:00 | Wander | Home area |
| Morning | 07:00 - 12:00 | Work | Assigned work pos |
| Lunch | 12:00 - 13:00 | Eat | Eating area |
| Afternoon | 13:00 - 19:00 | Work | Assigned work pos |
| Evening | 19:00 - 21:00 | Wander | Settlement area |
| Night | 21:00 - 06:00 | Sleep | Home interior |

---

## 4. Quest Status

Per-quest lifecycle FSM.

```
         ┌────────────┐
         │ NotStarted │
         └──────┬─────┘
                │ start_quest action (dialogue, event, auto)
                ▼
         ┌────────────┐
         │   Active   │◄──┐
         └──┬─────┬───┘   │
            │     │        │ next stage
            │     │        │ (more stages remain)
            │     │        │
    fail    │     │ all stages   
  condition │     │ complete
            │     │        
            ▼     ▼        
     ┌──────┐  ┌──────────┐
     │Failed│  │Completed │
     └──────┘  └──────────┘
         (terminal)  (terminal)
```

### Stage Evaluation (within Active)

```
Active Quest
│
├─→ Check failConditions (any true → Failed)
│
└─→ Check current stage conditions (all true → stage complete)
    ├─→ Execute onComplete actions
    ├─→ If nextStageId exists → advance to next stage (stay Active)
    └─→ If nextStageId is null → Completed
```

---

## 5. Combat Action State (per entity)

Tracks what each entity is currently doing in combat.

```
         ┌───────┐
         │  Idle │ (has AP, waiting for command)
         └──┬────┘
            │ action queued
            ▼
         ┌────────────┐
         │ Executing  │ (playing animation, projectile in flight)
         └──┬─────────┘
            │ action resolved
            ▼
         ┌──────────┐
         │ Cooldown │ (fire rate delay between shots)
         └──┬───────┘
            │ cooldown elapsed
            ▼
         ┌───────┐
         │  Idle │ (check AP, queue next or wait for regen)
         └──┬────┘
            │ AP = 0
            ▼
         ┌──────────────┐
         │ Regenerating │ (waiting for AP regen)
         └──┬───────────┘
            │ AP > action cost
            ▼
         ┌───────┐
         │  Idle │
         └───────┘
```

---

## 6. Day Phase Cycle

Deterministic cycle driven by `DayNightCycleProcessor`.

```
         ┌──────┐
    ┌───►│ Dawn │ (06:00 - 07:00)
    │    └──┬───┘
    │       │ 07:00
    │       ▼
    │    ┌──────┐
    │    │ Day  │ (07:00 - 20:00)
    │    └──┬───┘
    │       │ 20:00
    │       ▼
    │    ┌──────┐
    │    │ Dusk │ (20:00 - 21:00)
    │    └──┬───┘
    │       │ 21:00
    │       ▼
    │    ┌───────┐
    └────│ Night │ (21:00 - 06:00)
         └───────┘
```

### Phase Effects

| Phase | Light | Enemy Spawn | Visibility | NPC Schedule |
|-------|-------|-------------|-----------|-------------|
| Dawn | Warm orange, 30% intensity | Normal | 80% | Wake up |
| Day | Neutral white, 100% intensity | Normal | 100% | Work |
| Dusk | Amber tint, 50% intensity | +25% | 70% | Head home |
| Night | Cool blue, 15% intensity | +50% | 40% | Sleep |

---

## 7. Weather Cycle

Stochastic cycle driven by `WeatherProcessor`.

```
         ┌───────┐          roll every 6 in-game hours
    ┌───►│ Clear │ ────────────────────────────────┐
    │    └───────┘                                  │
    │         │ 30% chance                          │
    │         ▼                                     │
    │    ┌───────┐          20% chance              │
    │    │ Foggy │ ◄────────────────────────────────┤
    │    └───────┘                                  │
    │         │ 15% chance                          │
    │         ▼                     10% chance      │
    │    ┌───────────┐ ◄────────────────────────────┤
    │    │ DustStorm │                              │
    │    └───────────┘                              │
    │         │ 10% chance                          │
    │         ▼                     5% chance       │
    │    ┌──────────┐ ◄─────────────────────────────┘
    │    │ AcidRain │
    │    └──────────┘
    │         │ always transitions to Clear next
    └─────────┘
```

### Weather Effects

| State | Visibility | Combat | Survival | VFX |
|-------|-----------|--------|----------|-----|
| Clear | 100% | Normal | Normal | None |
| Foggy | 50% | −15% sight range | Normal | Fog volume |
| DustStorm | 30% | −30% accuracy, −50% sight | +50% fatigue rate | Particle storm |
| AcidRain | 70% | −10% accuracy | +2 rad/sec outdoors | Rain particles, green tint |

---

## 8. Dialogue State

Internal FSM within `DialogueProcessor`.

```
         ┌──────────┐
         │ Inactive │ (no dialogue running)
         └──┬───────┘
            │ StartDialogue(npc)
            ▼
         ┌───────────────┐
         │ DisplayingNode│ (show speaker text, build choice list)
         └──┬────────────┘
            │ player clicks choice
            ▼
         ┌──────────────────┐
         │EvaluatingChoice  │ (check conditions, run consequences)
         └──┬───────────────┘
            │
            ├─→ nextNodeId exists → DisplayingNode
            │
            └─→ nextNodeId is null OR empty choices
                │
                ▼
         ┌──────────┐
         │ Inactive │
         └──────────┘
```

---

## 9. UI Screen Stack

`ScreenManager` operates as a stack. Top screen receives input.

```
         ┌─────────────────────┐
         │     HudScreen       │ ← always at bottom
         ├─────────────────────┤
         │  (game world input) │ ← when HUD is top
         └─────────────────────┘

         Push(InventoryScreen):
         ┌─────────────────────┐
         │  InventoryScreen    │ ← receives input (modal)
         ├─────────────────────┤
         │     HudScreen       │ ← rendered but no input
         └─────────────────────┘

         Push(Tooltip):
         ┌─────────────────────┐
         │     Tooltip         │ ← non-modal overlay
         ├─────────────────────┤
         │  InventoryScreen    │ ← still receives some input
         ├─────────────────────┤
         │     HudScreen       │
         └─────────────────────┘

         Pop() → remove top
         Replace(screen) → pop + push
```

### Screen Modality

| Screen | Modal | Pauses Game |
|--------|-------|-------------|
| HudScreen | No | No |
| InventoryScreen | Yes | Yes (Exploring only) |
| CharacterScreen | Yes | Yes |
| QuestLogScreen | Yes | Yes |
| CraftingScreen | Yes | Yes |
| DialogueScreen | Yes | Yes (via InDialogue state) |
| MapScreen | Yes | Yes |
| PauseMenuScreen | Yes | Yes (via Paused state) |
| SettingsScreen | Yes | Yes |
| Tooltip | No | No |
| FloatingText | No | No |

---

## 10. Player Status Effects

Status effects are active-duration FSMs that apply/remove modifiers.

```
         ┌──────────┐
         │ Inactive │
         └──┬───────┘
            │ ApplyEffect(type, duration, intensity)
            ▼
         ┌──────────┐
         │  Active  │ ── tick duration ──→ duration ≤ 0 → remove
         └──┬───────┘                              │
            │ player uses cure item                 │
            ▼                                       ▼
         ┌──────────┐                        ┌──────────┐
         │ Inactive │                        │ Inactive │
         └──────────┘                        └──────────┘
```

### Effect Details

| Type | Tick Rate | Effect Per Tick | Cured By |
|------|----------|----------------|----------|
| Poisoned | 3 sec | −3 HP | Antidote |
| Bleeding | 2 sec | −2 HP | Bandage / Stimpak |
| Irradiated | 5 sec | +10 Radiation | Rad-Away |
| Stunned | instant | Cannot act for duration | Wears off |
| Crippled | permanent | −2 to limb stat (Str or Agi) | Doctor NPC / Surgery Kit |
