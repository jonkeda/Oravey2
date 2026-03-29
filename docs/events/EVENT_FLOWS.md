# Event Flow Sequences

End-to-end event chains showing how systems communicate through the EventBus. Each sequence documents the trigger, every event published, and which systems react.

---

## Table of Contents

1. [Combat Encounter (Full Cycle)](#1-combat-encounter-full-cycle)
2. [Attack → Damage → Death → Loot](#2-attack--damage--death--loot)
3. [Level Up Chain](#3-level-up-chain)
4. [Dialogue → Quest Start → Completion](#4-dialogue--quest-start--completion)
5. [Zone Transition & Chunk Streaming](#5-zone-transition--chunk-streaming)
6. [Item Pickup → Inventory → Equip](#6-item-pickup--inventory--equip)
7. [Day/Night Cycle Tick](#7-daynight-cycle-tick)
8. [Save/Load Cycle](#8-saveload-cycle)
9. [Crafting Flow](#9-crafting-flow)
10. [Survival Threshold Breach](#10-survival-threshold-breach)

---

## 1. Combat Encounter (Full Cycle)

```
Player enters aggro range of enemy
│
├─→ SightSensor.CanDetect() returns true
│   └─→ AICombatProcessor sets AIState = Alert → Engage
│       └─→ CombatStateManager.EnterCombat([enemies])
│           ├─→ GameStateManager.TransitionTo(InCombat)
│           │   └─→ PUBLISH: GameStateChangedEvent(Exploring, InCombat)
│           │       ├─→ MusicStateProcessor: crossfade to combat layer
│           │       ├─→ HudScreen: show AP bar, combat indicators
│           │       └─→ UIInputRouter: enable combat controls
│           └─→ PUBLISH: CombatStartedEvent([player, enemy1, enemy2])
│               └─→ CombatProcessor: begin RTwP loop
│
│  ── COMBAT LOOP (repeats) ──
│
├─→ CombatProcessor: regen AP each frame
├─→ Player or AI spends AP → ActionQueue.Enqueue(CombatAction)
├─→ DamageResolver.Resolve() → see Sequence #2
│
│  ── ALL ENEMIES DEAD ──
│
└─→ CombatStateManager.ExitCombat()
    ├─→ GameStateManager.TransitionTo(Exploring)
    │   └─→ PUBLISH: GameStateChangedEvent(InCombat, Exploring)
    │       ├─→ MusicStateProcessor: crossfade back to exploration
    │       ├─→ HudScreen: hide combat indicators
    │       └─→ UIInputRouter: restore exploration controls
    └─→ PUBLISH: CombatEndedEvent()
        └─→ QuestProcessor: check kill-based quest conditions
```

---

## 2. Attack → Damage → Death → Loot

```
Attacker queues CombatAction(RangedAttack, target)
│
├─→ CombatProcessor dequeues action
│   ├─→ Deduct AP from attacker.CombatComponent
│   ├─→ Degrade weapon: DurabilityComponent.Degrade()
│   └─→ DamageResolver.Resolve(attacker, target, weapon)
│       ├─→ Calculate hitChance (accuracy × skill × cover × range)
│       ├─→ Roll hit/miss
│       ├─→ If hit:
│       │   ├─→ Roll hit location (torso 40%, head 10%, arms 25%, legs 25%)
│       │   ├─→ Calculate damage (weapon.damage × skillBonus × crit − armor DR)
│       │   ├─→ target.HealthComponent.TakeDamage(amount)
│       │   │   └─→ PUBLISH: HealthChangedEvent(target, oldHP, newHP)
│       │   │       ├─→ HudScreen: update target health bar
│       │   │       └─→ FloatingText: show damage number
│       │   └─→ If target.HP ≤ 0:
│       │       └─→ PUBLISH: EntityDiedEvent(target, attacker)
│       │           ├─→ InventoryProcessor: drop loot from target inventory
│       │           │   └─→ PUBLISH: ItemDroppedEvent(target, lootItem)
│       │           ├─→ QuestProcessor: check entity_dead conditions
│       │           │   └─→ May trigger QuestStageCompletedEvent
│       │           ├─→ FactionComponent: player rep change for killing faction member
│       │           │   └─→ PUBLISH: FactionRepChangedEvent(factionId, old, new)
│       │           ├─→ XPGainedEvent(player, 25 × tier)
│       │           │   └─→ See Sequence #3 if level-up
│       │           └─→ AICombatProcessor: remove from active group
│       └─→ If miss:
│           └─→ SFX: play miss/ricochet
│               └─→ FloatingText: "MISS"
│
└─→ ProjectileScript spawned (if ranged)
    └─→ Travels toward target → triggers damage on arrival
```

---

## 3. Level Up Chain

```
XP gained (from kill, quest, discovery, etc.)
│
├─→ LevelComponent.GainXP(amount)
│   └─→ PUBLISH: XPGainedEvent(player, amount)
│       └─→ HudScreen: flash XP indicator
│
├─→ If currentXP >= XPToNextLevel:
│   ├─→ LevelComponent: Level++, recalculate XPToNextLevel
│   ├─→ PUBLISH: LevelUpEvent(player, oldLevel, newLevel)
│   │   ├─→ HealthComponent: recalculate MaxHP → heal to new max
│   │   │   └─→ PUBLISH: HealthChangedEvent(player, oldHP, newHP)
│   │   ├─→ LevelComponent: grant stat points (1)
│   │   ├─→ LevelComponent: grant skill points (5 + Int/2)
│   │   ├─→ PerkTreeComponent: if newLevel is even → perk point available
│   │   ├─→ HudScreen: show level-up notification
│   │   ├─→ AudioService: play level-up jingle
│   │   └─→ QuestProcessor: check level-based conditions
│   │       └─→ May trigger QuestStageCompletedEvent
│   └─→ Check for additional level-ups (if XP overflow)
```

---

## 4. Dialogue → Quest Start → Completion

```
Player interacts with NPC (Interact action near DialogueComponent entity)
│
├─→ DialogueProcessor.StartDialogue(npc)
│   ├─→ Load DialogueTree from data files
│   ├─→ GameStateManager.TransitionTo(InDialogue)
│   │   └─→ PUBLISH: GameStateChangedEvent(Exploring, InDialogue)
│   │       ├─→ PlayerMovementScript: disable movement
│   │       ├─→ CombatProcessor: disabled
│   │       └─→ MusicStateProcessor: lower music volume
│   ├─→ PUBLISH: DialogueStartedEvent(npc, treeId)
│   │   └─→ DialogueScreen: push onto ScreenManager
│   └─→ Display first node (CurrentNode = startNode)
│
├─→ Player selects choice (e.g., "I'll do it" → starts quest)
│   ├─→ DialogueProcessor.SelectChoice(index)
│   │   ├─→ Evaluate condition (if any) — skip if not met
│   │   ├─→ Execute consequences in order:
│   │   │   ├─→ StartQuestAction.Execute(player)
│   │   │   │   ├─→ QuestLogComponent: add quest as Active
│   │   │   │   └─→ PUBLISH: QuestUpdatedEvent(questId, Active)
│   │   │   │       └─→ QuestTracker: display new quest
│   │   │   └─→ GiveItemAction.Execute(player)
│   │   │       ├─→ InventoryComponent.Add(item)
│   │   │       └─→ PUBLISH: ItemPickedUpEvent(player, item)
│   │   │           └─→ HudScreen: show item pickup notification
│   │   └─→ Navigate to nextNodeId
│
├─→ Dialogue ends (empty choices or null nextNodeId)
│   ├─→ DialogueProcessor.EndDialogue()
│   ├─→ PUBLISH: DialogueEndedEvent(npc)
│   │   └─→ DialogueScreen: pop from ScreenManager
│   └─→ GameStateManager.TransitionTo(Exploring)
│       └─→ PUBLISH: GameStateChangedEvent(InDialogue, Exploring)
│
│  ── LATER: Player completes quest conditions ──
│
├─→ QuestProcessor evaluates stage conditions each frame
│   ├─→ All conditions met for current stage
│   ├─→ Execute onComplete actions:
│   │   ├─→ RemoveItemAction, GiveXP, ModifyFaction, SetFlag, etc.
│   │   └─→ Each action may publish its own events
│   ├─→ PUBLISH: QuestStageCompletedEvent(questId, stageId)
│   │   └─→ QuestTracker: update progress
│   └─→ If nextStageId is null:
│       ├─→ Quest status → Completed
│       └─→ PUBLISH: QuestUpdatedEvent(questId, Completed)
│           ├─→ QuestTracker: show completion
│           ├─→ AudioService: play quest-complete jingle
│           └─→ HudScreen: show reward summary
```

---

## 5. Zone Transition & Chunk Streaming

```
Player crosses chunk boundary
│
├─→ ChunkStreamingProcessor detects new player chunk
│   ├─→ Calculate new 3×3 active grid
│   ├─→ Diff with current loaded chunks
│   │
│   ├─→ For each chunk to UNLOAD:
│   │   ├─→ Serialize modified state (destroyed, looted)
│   │   ├─→ Destroy spawned entities → return to pool
│   │   └─→ PUBLISH: ChunkUnloadedEvent(chunkX, chunkY)
│   │
│   └─→ For each chunk to LOAD:
│       ├─→ Deserialize chunk JSON (tiles + entities + containers)
│       ├─→ TileMapRendererScript: render tiles
│       ├─→ Spawn entities from EntitySpawnInfo (check conditionFlags)
│       ├─→ Apply saved modifications (already looted containers, dead NPCs)
│       └─→ PUBLISH: ChunkLoadedEvent(chunkX, chunkY)
│           └─→ RadiationProcessor: check zone radiation level
│
├─→ ZoneTriggerComponent detected on new chunk
│   └─→ PUBLISH: ZoneEnteredEvent(zoneId)
│       ├─→ AmbientAudioProcessor: crossfade to zone ambience
│       ├─→ MusicStateProcessor: switch music theme (if zone has one)
│       ├─→ MiniMap: update zone label
│       ├─→ QuestProcessor: check zone_visited conditions
│       ├─→ RadiationProcessor: update radiation rate
│       └─→ FastTravelService: auto-discover location (if discoverable)
│
└─→ ISaveService.TriggerAutoSave()
    └─→ PUBLISH: SaveCompletedEvent(slotName)
```

---

## 6. Item Pickup → Inventory → Equip

```
Player interacts with ground item or container
│
├─→ InventoryComponent.CanAdd(item) → check weight
│   ├─→ If OVER weight limit: show "Encumbered" warning, reject
│   └─→ If OK:
│       ├─→ InventoryComponent.Add(itemInstance)
│       └─→ PUBLISH: ItemPickedUpEvent(player, item)
│           ├─→ HudScreen: show pickup notification
│           ├─→ QuestProcessor: check has_item conditions
│           └─→ AudioService: play pickup SFX
│
├─→ Player opens inventory → equips item to slot
│   ├─→ InventoryComponent.Equip(item, slot)
│   │   ├─→ If slot occupied: unequip old item first
│   │   │   └─→ PUBLISH: ItemEquippedEvent(player, null, slot) [unequip]
│   │   ├─→ Apply stat modifiers from equipment effects
│   │   ├─→ Update visual model on entity
│   │   └─→ PUBLISH: ItemEquippedEvent(player, item, slot)
│   │       ├─→ StatsComponent: recalculate effective stats
│   │       ├─→ HealthComponent: recalculate MaxHP (if End changed)
│   │       └─→ CharacterScreen: update display
```

---

## 7. Day/Night Cycle Tick

```
DayNightCycleProcessor.Update() (every frame)
│
├─→ Advance InGameHour by delta time / RealSecondsPerInGameHour
│
├─→ If phase boundary crossed:
│   └─→ PUBLISH: DayPhaseChangedEvent(oldPhase, newPhase)
│       ├─→ DayNightCycleProcessor: adjust global light direction + colour
│       │   Dawn: warm orange tint, low angle
│       │   Day: neutral white, high angle
│       │   Dusk: amber tint, low angle
│       │   Night: cool blue, dim
│       ├─→ AICivilianProcessor: trigger schedule changes
│       │   Dawn: wake up, go to work
│       │   Dusk: head home
│       │   Night: sleep
│       ├─→ AICombatProcessor: adjust enemy spawn rates
│       │   Night: +50% enemy encounter chance
│       ├─→ MusicStateProcessor: subtle layer shifts
│       └─→ HudScreen: update time display
│
├─→ SurvivalProcessor: tick hunger/thirst/fatigue
│   └─→ May trigger threshold events (see Sequence #10)
│
└─→ RadiationProcessor: natural rad decay if outside irradiated zone
```

---

## 8. Save/Load Cycle

```
── SAVE ──
AutoSave trigger (zone transition / timer / manual)
│
├─→ SaveService.SaveAsync(slotName, saveData)
│   ├─→ Collect player state:
│   │   Stats, Skills, HP, Level, XP, Perks, Inventory, Equipment, Factions
│   ├─→ Collect world state:
│   │   InGameHour, PlayerChunkXY, PlayerPosition, ChunkModifications
│   ├─→ Collect quest state:
│   │   QuestStates, QuestStages, WorldFlags
│   ├─→ Collect survival state:
│   │   Hunger, Thirst, Fatigue, Radiation
│   ├─→ Build SaveHeader (version, timestamp, player name, level, playtime)
│   ├─→ Serialize (MessagePack or JSON)
│   └─→ Write to disk
│
└─→ PUBLISH: SaveCompletedEvent(slotName)
    └─→ HudScreen: flash save icon

── LOAD ──
Player selects save slot
│
├─→ SaveService.LoadAsync(slotName)
│   ├─→ Read file, deserialize
│   ├─→ Check FormatVersion → run SaveMigrationChain if needed
│   ├─→ Restore player entity components from SaveData
│   ├─→ Set WorldStateService flags
│   ├─→ Restore QuestLogComponent
│   ├─→ ChunkStreamingProcessor: load player's chunk + 3×3 grid
│   └─→ Set camera to player position
│
└─→ PUBLISH: LoadCompletedEvent(slotName)
    ├─→ GameStateManager: TransitionTo(Exploring)
    ├─→ HudScreen: refresh all displays
    └─→ MusicStateProcessor: set music to appropriate zone/state
```

---

## 9. Crafting Flow

```
Player interacts with CraftingStationComponent
│
├─→ ScreenManager.Push(CraftingScreen)
│   ├─→ Load available recipes for station type
│   ├─→ Filter by discovered recipes (check discoveredBy)
│   └─→ Display recipe list with ingredient availability
│
├─→ Player selects recipe
│   ├─→ CraftingProcessor.CanCraft(player, recipe)
│   │   ├─→ Check all ingredients in inventory
│   │   ├─→ Check skill threshold (if required)
│   │   └─→ Return true/false
│   │
│   └─→ If can craft → player clicks "Craft"
│       ├─→ CraftingProcessor.Craft(player, recipe)
│       │   ├─→ Remove ingredients from inventory
│       │   │   └─→ PUBLISH: ItemDroppedEvent per ingredient consumed
│       │   ├─→ Create output item
│       │   ├─→ Add to inventory
│       │   │   └─→ PUBLISH: ItemPickedUpEvent(player, craftedItem)
│       │   ├─→ Grant skill XP (1 × Mechanics or Science)
│       │   ├─→ Grant crafting XP (10)
│       │   │   └─→ PUBLISH: XPGainedEvent(player, 10)
│       │   └─→ AudioService: play crafting SFX
│       └─→ CraftingScreen: refresh ingredient counts
│
└─→ Player closes crafting screen
    └─→ ScreenManager.Pop()
```

---

## 10. Survival Threshold Breach

```
SurvivalProcessor.Update() (ticks per in-game hour)
│
├─→ Increment Hunger / Thirst / Fatigue by decay rate
│
├─→ Hunger crosses threshold (e.g., 50 → 51 = "Hungry"):
│   ├─→ Apply debuff: StatModifier(Strength, -1, "Hungry")
│   │   └─→ StatsComponent.AddModifier()
│   │       └─→ HealthComponent: recalculate MaxHP
│   └─→ HudScreen: show hunger warning icon
│
├─→ Hunger reaches 76+ ("Starving"):
│   ├─→ HP drain: HealthComponent.TakeDamage(2) per minute
│   │   └─→ PUBLISH: HealthChangedEvent
│   │       └─→ If HP ≤ 0: EntityDiedEvent (starvation death)
│   └─→ HudScreen: critical hunger warning
│
├─→ Player consumes food item:
│   ├─→ InventoryComponent.Remove(foodItem, 1)
│   ├─→ Apply effects.hungerRestore: Hunger -= value
│   ├─→ Remove "Hungry"/"Starving" modifiers if below threshold
│   └─→ AudioService: play eating SFX
│
└─→ RadiationProcessor (parallel):
    ├─→ Player in irradiated zone: Radiation += zone.radiationLevel × deltaTime
    ├─→ Crosses 200: apply mild debuff (−1 End)
    ├─→ Crosses 500: apply severe debuff (−2 End, −1 Str)
    ├─→ Crosses 800: critical (HP drain + debuffs)
    ├─→ Reaches 1000: instant death
    └─→ Player uses Rad-Away: Radiation -= 100
        └─→ Recalculate debuffs based on new level
```
