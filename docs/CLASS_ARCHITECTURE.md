# Oravey2 — Class Architecture

This document defines every interface, class, enum, and data model across all systems. Each implementation step codes against these contracts.

---

## Table of Contents

1. [Namespace Map](#1-namespace-map)
2. [Framework Layer](#2-framework-layer)
3. [Input Layer](#3-input-layer)
4. [Camera](#4-camera)
5. [Player / Character](#5-player--character)
6. [World / Map](#6-world--map)
7. [Combat](#7-combat)
8. [AI](#8-ai)
9. [Dialogue & Quests](#9-dialogue--quests)
10. [Crafting & Survival](#10-crafting--survival)
11. [UI](#11-ui)
12. [Audio](#12-audio)
13. [Save / Platform](#13-save--platform)
14. [Data Models (JSON)](#14-data-models-json)
15. [Dependency Graph](#15-dependency-graph)

---

## 1. Namespace Map

```
Oravey2.Core
├── Framework
│   ├── Events          IGameEvent, IEventBus, EventBus, GameEvents
│   ├── Services        IServiceLocator, ServiceLocator
│   └── State           GameState, GameStateManager
├── Input               GameAction, IInputProvider, KeyboardMouseInputProvider,
│                       TouchInputProvider, GamepadInputProvider, InputUpdateScript
├── Camera              IsometricCameraScript
├── Player              PlayerMovementScript
├── Character
│   ├── Stats           StatsComponent, Stat, StatModifier
│   ├── Skills          SkillsComponent, Skill, SkillType
│   ├── Health          HealthComponent, StatusEffect, StatusEffectType
│   ├── Level           LevelComponent, LevelFormulas
│   └── Perks           PerkTreeComponent, PerkDefinition, PerkCondition
├── Inventory
│   ├── Core            InventoryComponent, InventoryProcessor
│   ├── Items           ItemDefinition, ItemInstance, ItemSlot, EquipmentSlot
│   └── Equipment       EquipmentComponent
├── Combat
│   ├── Core            CombatComponent, CombatProcessor, CombatStateManager
│   ├── Weapons         WeaponComponent, WeaponDefinition, ProjectileScript
│   ├── Armor           ArmorComponent, ArmorDefinition
│   ├── Actions         CombatAction, ActionQueue, DamageResolver
│   └── Cover           CoverComponent
├── AI
│   ├── Core            AIBehaviorComponent, AIState
│   ├── Sensors         ISensor, SightSensor, HearingSensor
│   ├── Decisions       UtilityScorer, AIConsideration, AIAction
│   ├── Blackboard      AIBlackboard
│   ├── Processors      AICombatProcessor, AICivilianProcessor
│   ├── Pathfinding     IPathfinder, TileGridPathfinder, PathResult
│   └── Groups          AIGroupCoordinator
├── Dialogue
│   ├── Data            DialogueTree, DialogueNode, DialogueChoice
│   ├── Conditions      IDialogueCondition, SkillCheckCondition, FlagCondition,
│   │                   ItemCondition, FactionCondition
│   ├── Actions         IConsequenceAction, GiveItemAction, ModifyFactionAction,
│   │                   SetFlagAction, StartQuestAction
│   └── Runtime         DialogueComponent, DialogueProcessor
├── Quests
│   ├── Data            QuestDefinition, QuestStage, QuestStatus
│   ├── Conditions      IQuestCondition, HasItemCondition, FlagCondition,
│   │                   FactionRepCondition, LevelCondition
│   ├── Actions         IQuestAction, GiveItemAction, SpawnEntityAction,
│   │                   UpdateJournalAction, TriggerEventAction
│   └── Runtime         QuestLogComponent, QuestProcessor, WorldStateService
├── Crafting
│   ├── Data            RecipeDefinition, StationType
│   └── Runtime         CraftingStationComponent, CraftingProcessor
├── Survival
│   ├── Components      SurvivalComponent, RadiationComponent, DurabilityComponent
│   └── Processors      SurvivalProcessor, RadiationProcessor
├── World
│   ├── Tiles           TileType, TileMapData, TileMapRendererScript
│   ├── Chunks          ChunkData, WorldMapData, ChunkStreamingProcessor
│   ├── Zones           ZoneDefinition, ZoneTriggerComponent
│   ├── Time            DayNightCycleComponent, DayNightCycleProcessor
│   ├── Weather         WeatherState, WeatherProcessor
│   └── Travel          FastTravelService, DiscoveredLocation
├── Factions            FactionComponent, FactionDefinition, FactionRelation
├── UI
│   ├── Framework       IScreen, ScreenManager, UIInputRouter
│   ├── HUD             HudScreen, HealthBar, APBar, MiniMap, QuestTracker, QuickSlotBar
│   ├── Screens         InventoryScreen, CharacterScreen, QuestLogScreen,
│   │                   CraftingScreen, DialogueScreen, MapScreen,
│   │                   PauseMenuScreen, SettingsScreen
│   └── Widgets         ProgressBar, Tooltip, FloatingText, RadialMenu
├── Audio
│   ├── Core            IAudioService, AudioService
│   ├── Music           MusicStateProcessor, MusicLayer
│   └── SFX             SFXPool, AmbientAudioProcessor, FootstepProcessor
└── Save
    ├── Core            ISaveService, SaveService, SaveData, SaveHeader
    ├── Migration       ISaveMigration, SaveMigrationChain
    └── Platform        IPlatformServices, WindowsPlatformServices,
                        iOSPlatformServices, AndroidPlatformServices
```

---

## 2. Framework Layer

### 2.1 Events

```csharp
// ── Marker ──
public interface IGameEvent { }

// ── Bus ──
public interface IEventBus
{
    void Subscribe<T>(Action<T> handler) where T : IGameEvent;
    void Unsubscribe<T>(Action<T> handler) where T : IGameEvent;
    void Publish<T>(T gameEvent) where T : IGameEvent;
}

public sealed class EventBus : IEventBus { /* Dictionary<Type, List<Delegate>> */ }

// ── Events (record structs for zero-alloc) ──
public readonly record struct GameStateChangedEvent(GameState OldState, GameState NewState) : IGameEvent;
public readonly record struct PlayerMovedEvent(Vector3 OldPosition, Vector3 NewPosition) : IGameEvent;
public readonly record struct CameraZoomChangedEvent(float OldZoom, float NewZoom) : IGameEvent;
public readonly record struct CameraRotatedEvent(float OldYaw, float NewYaw) : IGameEvent;
public readonly record struct HealthChangedEvent(Entity Target, int OldHP, int NewHP) : IGameEvent;
public readonly record struct EntityDiedEvent(Entity Target, Entity? Killer) : IGameEvent;
public readonly record struct ItemPickedUpEvent(Entity Who, ItemInstance Item) : IGameEvent;
public readonly record struct ItemDroppedEvent(Entity Who, ItemInstance Item) : IGameEvent;
public readonly record struct ItemEquippedEvent(Entity Who, ItemInstance Item, EquipmentSlot Slot) : IGameEvent;
public readonly record struct LevelUpEvent(Entity Who, int OldLevel, int NewLevel) : IGameEvent;
public readonly record struct XPGainedEvent(Entity Who, int Amount) : IGameEvent;
public readonly record struct CombatStartedEvent(Entity[] Participants) : IGameEvent;
public readonly record struct CombatEndedEvent() : IGameEvent;
public readonly record struct QuestUpdatedEvent(string QuestId, QuestStatus Status) : IGameEvent;
public readonly record struct QuestStageCompletedEvent(string QuestId, string StageId) : IGameEvent;
public readonly record struct DialogueStartedEvent(Entity NPC, string TreeId) : IGameEvent;
public readonly record struct DialogueEndedEvent(Entity NPC) : IGameEvent;
public readonly record struct FactionRepChangedEvent(string FactionId, int OldRep, int NewRep) : IGameEvent;
public readonly record struct DayPhaseChangedEvent(DayPhase OldPhase, DayPhase NewPhase) : IGameEvent;
public readonly record struct ChunkLoadedEvent(int ChunkX, int ChunkY) : IGameEvent;
public readonly record struct ChunkUnloadedEvent(int ChunkX, int ChunkY) : IGameEvent;
public readonly record struct WeatherChangedEvent(WeatherState OldWeather, WeatherState NewWeather) : IGameEvent;
public readonly record struct ZoneEnteredEvent(string ZoneId) : IGameEvent;
public readonly record struct SaveCompletedEvent(string SlotName) : IGameEvent;
public readonly record struct LoadCompletedEvent(string SlotName) : IGameEvent;
```

### 2.2 Services

```csharp
public interface IServiceLocator
{
    void Register<T>(T service) where T : class;
    T Get<T>() where T : class;
    bool TryGet<T>(out T? service) where T : class;
}

public sealed class ServiceLocator : IServiceLocator
{
    public static ServiceLocator Instance { get; }
    public static void Reset();   // testing only
}
```

### 2.3 Game State

```csharp
public enum GameState
{
    Loading, Exploring, InCombat, InDialogue, InMenu, Paused
}

public sealed class GameStateManager
{
    public GameState CurrentState { get; }
    public GameStateManager(IEventBus eventBus);
    public bool TransitionTo(GameState newState);  // validates + publishes event
}
```

---

## 3. Input Layer

```csharp
public enum GameAction
{
    MoveUp, MoveDown, MoveLeft, MoveRight,
    Interact, Attack, Pause, Inventory,
    RotateCameraLeft, RotateCameraRight,
    ZoomIn, ZoomOut,
    // Future
    CombatPause, QuickSlot1, QuickSlot2, QuickSlot3,
    QuickSlot4, QuickSlot5, QuickSlot6
}

public interface IInputProvider
{
    Vector2 MovementAxis { get; }
    bool IsActionPressed(GameAction action);
    bool IsActionHeld(GameAction action);
    bool IsActionReleased(GameAction action);
    Vector2 PointerScreenPosition { get; }
    float ScrollDelta { get; }
    void Update(InputManager input);
}

public sealed class KeyboardMouseInputProvider : IInputProvider { }  // Step 1
public sealed class TouchInputProvider : IInputProvider { }          // Step 8
public sealed class GamepadInputProvider : IInputProvider { }        // Step 8

// Stride script that pumps IInputProvider each frame
public class InputUpdateScript : SyncScript { }
```

---

## 4. Camera

```csharp
public class IsometricCameraScript : SyncScript
{
    public Entity? Target { get; set; }
    public float Pitch { get; set; }           // 30°
    public float Yaw { get; set; }             // 45°
    public float Distance { get; set; }        // 20
    public float FollowSmoothing { get; set; } // 5
    public float Deadzone { get; set; }        // 0.5
    public float ZoomMin { get; set; }         // 10
    public float ZoomMax { get; set; }         // 40
    public float CurrentZoom { get; set; }     // 20
    public float ZoomSpeed { get; set; }       // 2
    public float RotationSnap { get; set; }    // 90°
}
```

---

## 5. Player / Character

### 5.1 Player Movement (Step 1)

```csharp
public class PlayerMovementScript : SyncScript
{
    public float MoveSpeed { get; set; }   // 5
    public float CameraYaw { get; set; }   // synced from camera rotation events
}
```

### 5.2 Stats (Step 2)

```csharp
public enum Stat { Strength, Perception, Endurance, Charisma, Intelligence, Agility, Luck }

public class StatsComponent : EntityComponent
{
    public Dictionary<Stat, int> BaseStats { get; }            // 1-10
    public IReadOnlyList<StatModifier> Modifiers { get; }
    public int GetEffective(Stat stat);                         // base + modifiers
    public void AddModifier(StatModifier mod);
    public void RemoveModifier(StatModifier mod);
}

public sealed record StatModifier(Stat Stat, int Amount, string Source);
```

### 5.3 Skills (Step 2)

```csharp
public enum SkillType { Firearms, Melee, Survival, Science, Speech, Stealth, Mechanics }

public class SkillsComponent : EntityComponent
{
    public Dictionary<SkillType, int> BaseSkills { get; }      // 0-100
    public int GetEffective(SkillType skill);                   // base + stat bonus + modifiers
    public void AddXP(SkillType skill, int amount);             // use-based improvement
    public void AllocatePoints(SkillType skill, int points);
}
```

### 5.4 Health (Step 2)

```csharp
public enum StatusEffectType { Poisoned, Bleeding, Irradiated, Stunned, Crippled }

public sealed record StatusEffect(StatusEffectType Type, float Duration, float Intensity);

public class HealthComponent : EntityComponent
{
    public int CurrentHP { get; set; }
    public int MaxHP { get; }                                   // derived: 50 + End*10 + Level*5
    public int RadiationLevel { get; set; }                     // 0-1000
    public List<StatusEffect> ActiveEffects { get; }
    public bool IsAlive => CurrentHP > 0;

    public void TakeDamage(int amount);                         // clamp, publish HealthChanged
    public void Heal(int amount);
    public void ApplyEffect(StatusEffect effect);
    public void RemoveEffect(StatusEffectType type);
}
```

### 5.5 Level (Step 2)

```csharp
public class LevelComponent : EntityComponent
{
    public int Level { get; }                    // starts at 1
    public int CurrentXP { get; }
    public int XPToNextLevel { get; }            // 100 * Level²

    public void GainXP(int amount);              // may trigger level-up → event
    public int StatPointsAvailable { get; }
    public int SkillPointsAvailable { get; }     // 5 + Int/2 per level
}

public static class LevelFormulas
{
    public static int XPRequired(int level);            // 100 * level²
    public static int SkillPointsPerLevel(int intel);   // 5 + intel/2
    public static int MaxHP(int endurance, int level);  // 50 + end*10 + level*5
    public static float CarryWeight(int strength);      // 50 + str*10
}
```

### 5.6 Perks (Step 2)

```csharp
public sealed record PerkCondition(int RequiredLevel, Stat? RequiredStat, int? StatThreshold);

public sealed record PerkDefinition(
    string Id, string Name, string Description,
    PerkCondition Condition, string[] Effects      // e.g. "stat:Strength:+1", "skill:Firearms:+10"
);

public class PerkTreeComponent : EntityComponent
{
    public IReadOnlyList<PerkDefinition> AllPerks { get; }     // loaded from JSON
    public HashSet<string> UnlockedPerks { get; }
    public bool CanUnlock(string perkId);
    public void Unlock(string perkId);                          // applies effects
}
```

### 5.7 Factions (Step 2)

```csharp
public sealed record FactionDefinition(string Id, string Name, string Description);

public class FactionComponent : EntityComponent
{
    public Dictionary<string, int> Reputation { get; }          // -100 to +100
    public void ModifyRep(string factionId, int delta);         // clamp, publish event
    public FactionRelation GetRelation(string factionId);       // Hostile/Neutral/Friendly/Allied
}

public enum FactionRelation { Hostile, Unfriendly, Neutral, Friendly, Allied }
```

---

## 6. World / Map

### 6.1 Tiles (Step 1)

```csharp
public enum TileType : byte { Empty, Ground, Road, Rubble, Water, Wall }

public sealed class TileMapData
{
    public int Width { get; }
    public int Height { get; }
    public TileType[,] Tiles { get; }
    public TileType GetTile(int x, int y);
    public void SetTile(int x, int y, TileType type);
    public static TileMapData CreateDefault(int w = 16, int h = 16);
}

public class TileMapRendererScript : SyncScript
{
    public TileMapData? MapData { get; set; }
    public float TileSize { get; set; }          // 1.0
    public float TileHeight { get; set; }        // 0.1
    public float WallHeight { get; set; }        // 1.0
}
```

### 6.2 Chunks & Streaming (Step 7)

```csharp
public sealed class ChunkData
{
    public int ChunkX { get; }
    public int ChunkY { get; }
    public TileMapData Tiles { get; }                           // 16×16
    public List<EntitySpawnInfo> Entities { get; }              // what spawns in this chunk
    public Dictionary<string, bool> ModifiedState { get; }     // destroyed/looted flags
}

public sealed class WorldMapData
{
    public int ChunksWide { get; }
    public int ChunksHigh { get; }
    public ChunkData?[,] Chunks { get; }
    public ChunkData? GetChunk(int cx, int cy);
}

public class ChunkStreamingProcessor : SyncScript
{
    public WorldMapData? WorldMap { get; set; }
    public int ActiveGridSize { get; set; }      // 3 (3×3 = 9 chunks)
    // Loads/unloads based on player chunk position
}
```

### 6.3 Zones (Step 7)

```csharp
public sealed record ZoneDefinition(
    string Id, string Name, string BiomeType,
    float RadiationLevel, int EnemyDifficultyTier
);

public class ZoneTriggerComponent : EntityComponent
{
    public string ZoneId { get; set; }
}
```

### 6.4 Day/Night (Step 7)

```csharp
public enum DayPhase { Dawn, Day, Dusk, Night }

public class DayNightCycleComponent : EntityComponent
{
    public float InGameHour { get; set; }                       // 0.0 - 24.0
    public float RealSecondsPerInGameHour { get; set; }         // 120 (48 min = full day)
    public DayPhase CurrentPhase { get; }
}

public class DayNightCycleProcessor : SyncScript
{
    // Advances time, adjusts lighting, publishes DayPhaseChangedEvent
}
```

### 6.5 Weather (Step 9)

```csharp
public enum WeatherState { Clear, Foggy, DustStorm, AcidRain }

public class WeatherProcessor : SyncScript
{
    public WeatherState Current { get; }
    public float TransitionDuration { get; set; }
    // Random weather events, publishes WeatherChangedEvent
}
```

### 6.6 Fast Travel (Step 7)

```csharp
public sealed record DiscoveredLocation(string Id, string Name, int ChunkX, int ChunkY);

public sealed class FastTravelService
{
    public IReadOnlyList<DiscoveredLocation> Locations { get; }
    public void Discover(DiscoveredLocation loc);
    public bool CanTravel(string fromId, string toId);
    public float GetTravelTime(string fromId, string toId);     // in-game hours
    public void Travel(string destinationId);                   // teleport + advance time
}
```

---

## 7. Combat

### 7.1 Components (Step 3)

```csharp
public class CombatComponent : EntityComponent
{
    public int MaxAP { get; set; }               // 10
    public float CurrentAP { get; set; }
    public float APRegenPerSecond { get; set; }  // 2
    public bool InCombat { get; set; }
}

public class WeaponComponent : EntityComponent
{
    public WeaponDefinition? Definition { get; set; }
    public int CurrentAmmo { get; set; }
}

public sealed record WeaponDefinition(
    string Id, string Name, int Damage, float Range,
    int APCost, float Accuracy, string AmmoType, float FireRate
);

public class ArmorComponent : EntityComponent
{
    public ArmorDefinition? Definition { get; set; }
}

public sealed record ArmorDefinition(
    string Id, string Name, int DamageReduction,
    Dictionary<string, float> CoverageZones      // "head": 0.2, "torso": 0.5, etc.
);

public class CoverComponent : EntityComponent
{
    public CoverLevel Level { get; set; }         // Half, Full
}

public enum CoverLevel { None, Half, Full }
```

### 7.2 Combat Logic (Step 3)

```csharp
public sealed record CombatAction(
    Entity Actor, CombatActionType Type,
    Entity? Target, Vector3? TargetPosition
);

public enum CombatActionType { MeleeAttack, RangedAttack, Reload, UseItem, Move, TakeCover }

public sealed class ActionQueue
{
    public IReadOnlyList<CombatAction> PendingActions { get; }
    public void Enqueue(CombatAction action);
    public CombatAction? Dequeue();
    public void Clear();
}

public sealed class DamageResolver
{
    public DamageResult Resolve(Entity attacker, Entity target, WeaponDefinition weapon);
}

public sealed record DamageResult(bool Hit, int Damage, string HitLocation, bool Critical);

public class CombatProcessor : SyncScript
{
    // Runs RTwP loop: regen AP, process actions, resolve attacks
    // Pauses when player toggles CombatPause
}

public sealed class CombatStateManager
{
    public CombatStateManager(IEventBus bus, GameStateManager gameState);
    public void EnterCombat(Entity[] enemies);
    public void ExitCombat();
}
```

### 7.3 Projectiles (Step 3)

```csharp
public class ProjectileScript : SyncScript
{
    public Entity? Source { get; set; }
    public Entity? Target { get; set; }
    public float Speed { get; set; }
    public int Damage { get; set; }
    // Moves toward target, on hit → DamageResolver
}
```

---

## 8. AI

### 8.1 Core (Step 4)

```csharp
public enum AIState { Idle, Patrol, Alert, Engage, Flee }
public enum AIBehaviorType { Combat, Civilian }

public class AIBehaviorComponent : EntityComponent
{
    public AIBehaviorType BehaviorType { get; set; }
    public AIState CurrentState { get; set; }
    public AIBlackboard Blackboard { get; }
    public float AggroRange { get; set; }           // detection trigger
    public float LeashRange { get; set; }           // max chase distance
}
```

### 8.2 Sensors (Step 4)

```csharp
public interface ISensor
{
    bool CanDetect(Entity self, Entity target);
    float GetDetectionScore(Entity self, Entity target);
}

public sealed class SightSensor : ISensor
{
    public float Range { get; set; }          // units
    public float ConeAngle { get; set; }      // degrees
}

public sealed class HearingSensor : ISensor
{
    public float Radius { get; set; }         // units
}
```

### 8.3 Blackboard (Step 4)

```csharp
public sealed class AIBlackboard
{
    public Vector3? LastKnownTargetPosition { get; set; }
    public Entity? CurrentTarget { get; set; }
    public float ThreatLevel { get; set; }                      // 0-1
    public float TimeSinceLastSeen { get; set; }
    public Dictionary<string, object> CustomData { get; }
}
```

### 8.4 Utility AI (Step 4)

```csharp
public sealed record AIConsideration(string Name, Func<Entity, AIBlackboard, float> Evaluate, float Weight);

public sealed record AIActionDefinition(string Name, AIConsideration[] Considerations);

public sealed class UtilityScorer
{
    public (string ActionName, float Score) Score(Entity entity, AIBlackboard blackboard, AIActionDefinition[] actions);
}
```

### 8.5 Pathfinding (Step 4)

```csharp
public sealed record PathResult(bool Found, List<Vector2Int> Path);

public interface IPathfinder
{
    PathResult FindPath(Vector2Int start, Vector2Int goal, TileMapData map);
}

public sealed class TileGridPathfinder : IPathfinder { }       // A* on tile grid
```

### 8.6 AI Processors (Step 4)

```csharp
public class AICombatProcessor : SyncScript
{
    // For entities with AIBehaviorType.Combat
    // Runs utility scoring each frame, executes highest action
}

public class AICivilianProcessor : SyncScript
{
    // For entities with AIBehaviorType.Civilian
    // Schedule-driven waypoint routines (time-of-day based)
}

public sealed class AIGroupCoordinator
{
    // Coordinates group tactics: flanking, focus fire, retreat threshold
    public void UpdateGroup(IReadOnlyList<Entity> group, Entity target);
}
```

---

## 9. Dialogue & Quests

### 9.1 Dialogue Data (Step 5)

```csharp
public sealed record DialogueTree(string Id, Dictionary<string, DialogueNode> Nodes, string StartNodeId);

public sealed record DialogueNode(
    string Id, string Speaker, string Text,
    List<DialogueChoice> Choices
);

public sealed record DialogueChoice(
    string Text, string? NextNodeId,
    IDialogueCondition? Condition,
    List<IConsequenceAction>? Consequences
);
```

### 9.2 Dialogue Conditions (Step 5)

```csharp
public interface IDialogueCondition
{
    bool Evaluate(Entity player);
    string DisplayHint { get; }              // e.g. "[Speech 40]"
}

public sealed class SkillCheckCondition : IDialogueCondition
{
    public SkillType Skill { get; }
    public int Threshold { get; }
    public bool Hidden { get; }              // don't show requirement
}

public sealed class FlagCondition : IDialogueCondition
{
    public string FlagName { get; }
    public bool ExpectedValue { get; }
}

public sealed class ItemCondition : IDialogueCondition
{
    public string ItemId { get; }
    public int RequiredCount { get; }
}

public sealed class FactionCondition : IDialogueCondition
{
    public string FactionId { get; }
    public FactionRelation MinRelation { get; }
}
```

### 9.3 Dialogue Consequences (Step 5)

```csharp
public interface IConsequenceAction
{
    void Execute(Entity player);
}

public sealed class GiveItemAction : IConsequenceAction { public string ItemId; public int Count; }
public sealed class RemoveItemAction : IConsequenceAction { public string ItemId; public int Count; }
public sealed class ModifyFactionAction : IConsequenceAction { public string FactionId; public int Delta; }
public sealed class SetFlagAction : IConsequenceAction { public string FlagName; public bool Value; }
public sealed class StartQuestAction : IConsequenceAction { public string QuestId; }
public sealed class ModifyStatAction : IConsequenceAction { public Stat Stat; public int Delta; }
```

### 9.4 Dialogue Runtime (Step 5)

```csharp
public class DialogueComponent : EntityComponent
{
    public string DialogueTreeId { get; set; }
}

public class DialogueProcessor : SyncScript
{
    public DialogueTree? ActiveTree { get; }
    public DialogueNode? CurrentNode { get; }
    public void StartDialogue(Entity npc);
    public void SelectChoice(int choiceIndex);
    public void EndDialogue();
}
```

### 9.5 Quest Data (Step 5)

```csharp
public enum QuestStatus { NotStarted, Active, Completed, Failed }

public sealed record QuestStage(
    string Id, string Description,
    List<IQuestCondition> Conditions,
    List<IQuestAction> OnCompleteActions,
    string? NextStageId
);

public sealed record QuestDefinition(
    string Id, string Title, string Description,
    string FirstStageId,
    Dictionary<string, QuestStage> Stages
);
```

### 9.6 Quest Conditions & Actions (Step 5)

```csharp
public interface IQuestCondition { bool Evaluate(Entity player); }

public sealed class HasItemCondition : IQuestCondition { public string ItemId; public int Count; }
public sealed class QuestFlagCondition : IQuestCondition { public string Flag; public bool Value; }
public sealed class FactionRepCondition : IQuestCondition { public string FactionId; public int MinRep; }
public sealed class LevelCondition : IQuestCondition { public int MinLevel; }

public interface IQuestAction { void Execute(Entity player); }

public sealed class QuestGiveItemAction : IQuestAction { public string ItemId; public int Count; }
public sealed class SpawnEntityAction : IQuestAction { public string PrefabId; public Vector3 Position; }
public sealed class UpdateJournalAction : IQuestAction { public string Text; }
public sealed class TriggerEventAction : IQuestAction { public string EventId; }
```

### 9.7 Quest Runtime (Step 5)

```csharp
public class QuestLogComponent : EntityComponent
{
    public Dictionary<string, QuestStatus> Quests { get; }
    public Dictionary<string, string> CurrentStages { get; }   // questId → stageId
}

public class QuestProcessor : SyncScript
{
    // Each frame: evaluate active quest stage conditions → advance
}

public sealed class WorldStateService
{
    public Dictionary<string, bool> Flags { get; }
    public void SetFlag(string name, bool value);
    public bool GetFlag(string name);
}
```

---

## 10. Crafting & Survival

### 10.1 Crafting (Step 6)

```csharp
public enum StationType { Workbench, ChemLab, CookingFire }

public sealed record RecipeDefinition(
    string Id, string OutputItemId, int OutputCount,
    Dictionary<string, int> Ingredients,            // itemId → count
    StationType RequiredStation,
    SkillType? RequiredSkill, int SkillThreshold
);

public class CraftingStationComponent : EntityComponent
{
    public StationType Type { get; set; }
    public List<string> AvailableRecipeIds { get; set; }
}

public class CraftingProcessor : SyncScript
{
    public bool CanCraft(Entity player, RecipeDefinition recipe);
    public void Craft(Entity player, RecipeDefinition recipe);
    // Validates inventory, skill, consumes ingredients, produces output
}
```

### 10.2 Durability (Step 6)

```csharp
public class DurabilityComponent : EntityComponent
{
    public int CurrentDurability { get; set; }
    public int MaxDurability { get; set; }
    public float DegradePerUse { get; set; }

    public void Degrade();                    // called on weapon fire / armor hit
    public void Repair(int amount);
    public bool IsBroken => CurrentDurability <= 0;
}
```

### 10.3 Survival (Step 6)

```csharp
public class SurvivalComponent : EntityComponent
{
    public float Hunger { get; set; }            // 0-100 (0 = full, 100 = starving)
    public float Thirst { get; set; }
    public float Fatigue { get; set; }
    public bool Enabled { get; set; }            // toggle in settings
}

public class SurvivalProcessor : SyncScript
{
    // Ticks needs over time, applies debuffs at thresholds (25/50/75/100)
}

public class RadiationComponent : EntityComponent
{
    public int Level { get; set; }               // 0-1000
}

public class RadiationProcessor : SyncScript
{
    // Checks player position against zone radiation, increments/decrements
}
```

---

## 11. UI

### 11.1 Framework (Step 8)

```csharp
public interface IScreen
{
    string Name { get; }
    bool IsModal { get; }
    void OnEnter();
    void OnExit();
    void Update(GameTime time);
    void Draw();
}

public sealed class ScreenManager
{
    public IScreen? ActiveScreen { get; }
    public void Push(IScreen screen);
    public void Pop();
    public void Replace(IScreen screen);
}

public sealed class UIInputRouter
{
    // Routes input to topmost modal or game world
}
```

### 11.2 Screens (Step 8)

```csharp
public class HudScreen : IScreen { /* HP, AP, minimap, quest tracker, quick slots */ }
public class InventoryScreen : IScreen { /* grid, drag-equip, weight, tooltips */ }
public class CharacterScreen : IScreen { /* stats, skills, perks, level, XP */ }
public class QuestLogScreen : IScreen { /* tabs: active/completed/failed */ }
public class CraftingScreen : IScreen { /* recipe list, ingredients, craft button */ }
public class DialogueScreen : IScreen { /* portrait, text, choices, skill checks */ }
public class MapScreen : IScreen { /* fog-of-war, fast travel, zone labels */ }
public class PauseMenuScreen : IScreen { /* resume, settings, save, load, quit */ }
public class SettingsScreen : IScreen { /* volumes, quality, survival toggle, controls */ }
```

---

## 12. Audio

```csharp
public interface IAudioService
{
    void PlaySFX(string sfxId, Vector3? position = null);
    void PlayMusic(string trackId);
    void CrossfadeMusic(string trackId, float duration);
    void StopMusic(float fadeOut = 0f);
    void SetVolume(AudioCategory category, float volume);     // 0-1
}

public enum AudioCategory { Master, Music, SFX, Ambient, Voice }

public sealed class AudioService : IAudioService { }

public class MusicStateProcessor : SyncScript
{
    // Monitors GameState → crossfades layers (base, exploration, tension, combat, eerie)
}

public class AmbientAudioProcessor : SyncScript
{
    // Zone-specific ambient loops, crossfade on zone transition
}

public class FootstepProcessor : SyncScript
{
    // Detects tile under player → plays surface-specific SFX
}
```

---

## 13. Save / Platform

### 13.1 Save Service (Step 10)

```csharp
public sealed record SaveHeader(
    int FormatVersion, string GameVersion,
    DateTime Timestamp, string PlayerName,
    int PlayerLevel, TimeSpan PlayTime
);

public sealed class SaveData
{
    public SaveHeader Header { get; set; }
    // Player
    public Dictionary<Stat, int> Stats { get; set; }
    public Dictionary<SkillType, int> Skills { get; set; }
    public int HP { get; set; }
    public int Level { get; set; }
    public int XP { get; set; }
    public List<string> UnlockedPerks { get; set; }
    public List<SerializedItem> Inventory { get; set; }
    public Dictionary<EquipmentSlot, string?> Equipment { get; set; }
    public Dictionary<string, int> FactionRep { get; set; }
    // World
    public float InGameHour { get; set; }
    public int PlayerChunkX { get; set; }
    public int PlayerChunkY { get; set; }
    public Vector3 PlayerPosition { get; set; }
    public Dictionary<string, Dictionary<string, bool>> ChunkModifications { get; set; }
    // Quests
    public Dictionary<string, QuestStatus> QuestStates { get; set; }
    public Dictionary<string, string> QuestStages { get; set; }
    public Dictionary<string, bool> WorldFlags { get; set; }
    // Survival
    public float Hunger { get; set; }
    public float Thirst { get; set; }
    public float Fatigue { get; set; }
    public int Radiation { get; set; }
}

public interface ISaveService
{
    Task SaveAsync(string slotName, SaveData data);
    Task<SaveData?> LoadAsync(string slotName);
    Task DeleteAsync(string slotName);
    Task<List<SaveHeader>> ListSavesAsync();
    void TriggerAutoSave();
}

public sealed class SaveService : ISaveService { /* MessagePack/JSON serialization */ }
```

### 13.2 Save Migration (Step 10)

```csharp
public interface ISaveMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    SaveData Migrate(SaveData data);
}

public sealed class SaveMigrationChain
{
    public SaveData MigrateToLatest(SaveData data);
}
```

### 13.3 Platform Services (Step 10)

```csharp
public interface IPlatformServices
{
    string SaveDirectory { get; }
    Task SyncToCloudAsync();
    Task SyncFromCloudAsync();
    void Vibrate(float duration);
    void ScheduleNotification(string title, string body, TimeSpan delay);
}

public sealed class WindowsPlatformServices : IPlatformServices { }
public sealed class iOSPlatformServices : IPlatformServices { }
public sealed class AndroidPlatformServices : IPlatformServices { }
```

---

## 14. Data Models (JSON)

### 14.1 Items (`Assets/Data/Items/items.json`)

```csharp
public enum EquipmentSlot { None, Head, Torso, Legs, Feet, PrimaryWeapon, SecondaryWeapon, Accessory1, Accessory2 }

public sealed record ItemDefinition(
    string Id, string Name, string Description,
    float Weight, bool Stackable, int MaxStack,
    EquipmentSlot Slot,
    Dictionary<string, string>? Effects           // e.g. "heal": "20", "radReduction": "50"
);

public sealed class ItemInstance
{
    public ItemDefinition Definition { get; }
    public int StackCount { get; set; }
    public DurabilityComponent? Durability { get; }        // null for non-degradable items
}
```

### 14.2 Inventory (Step 2)

```csharp
public class InventoryComponent : EntityComponent
{
    public List<ItemInstance> Items { get; }
    public Dictionary<EquipmentSlot, ItemInstance?> Equipped { get; }
    public float MaxCarryWeight { get; }                     // derived from Strength
    public float CurrentWeight { get; }

    public bool CanAdd(ItemDefinition item, int count = 1);
    public void Add(ItemInstance item);
    public void Remove(ItemInstance item, int count = 1);
    public void Equip(ItemInstance item, EquipmentSlot slot);
    public void Unequip(EquipmentSlot slot);
}

public class InventoryProcessor : SyncScript
{
    // Enforces weight limits, handles equip/unequip, publishes events
}
```

---

## 15. Dependency Graph

Shows which classes depend on which services and components.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          ServiceLocator (global)                            │
│                                                                             │
│  Registered at startup:                                                     │
│  ├── IEventBus ──────────────── EventBus                                    │
│  ├── IInputProvider ─────────── KeyboardMouseInput / Touch / Gamepad        │
│  ├── GameStateManager                                                       │
│  ├── WorldStateService                                                      │
│  ├── FastTravelService                                                      │
│  ├── IAudioService ──────────── AudioService                                │
│  ├── ISaveService ───────────── SaveService                                 │
│  ├── IPlatformServices ──────── Windows/iOS/AndroidPlatformServices         │
│  └── ScreenManager                                                          │
└─────────────────────────────────────────────────────────────────────────────┘

Entity composition (typical Player entity):
  Entity "Player"
  ├── PlayerMovementScript     → reads IInputProvider, publishes PlayerMovedEvent
  ├── StatsComponent           → pure data
  ├── SkillsComponent          → reads StatsComponent for bonuses
  ├── HealthComponent          → reads StatsComponent + LevelComponent for MaxHP
  ├── LevelComponent           → publishes LevelUpEvent
  ├── PerkTreeComponent        → reads LevelComponent + StatsComponent for unlock checks
  ├── InventoryComponent       → reads StatsComponent for carry weight
  ├── FactionComponent         → pure data + events
  ├── CombatComponent          → AP pool
  ├── QuestLogComponent        → pure data
  ├── SurvivalComponent        → optional, reads settings
  └── RadiationComponent       → pure data

Entity composition (typical Enemy entity):
  Entity "Raider"
  ├── AIBehaviorComponent      → blackboard, state
  ├── StatsComponent
  ├── SkillsComponent
  ├── HealthComponent
  ├── CombatComponent
  ├── WeaponComponent
  ├── ArmorComponent
  ├── InventoryComponent       → loot on death
  └── FactionComponent

Entity composition (typical NPC entity):
  Entity "Merchant"
  ├── AIBehaviorComponent      → Civilian type, schedule
  ├── DialogueComponent        → references dialogue tree
  ├── InventoryComponent       → trade stock
  └── FactionComponent

Script → Service dependencies:
  IsometricCameraScript ──→ IInputProvider, IEventBus
  PlayerMovementScript ───→ IInputProvider, IEventBus
  CombatProcessor ────────→ IEventBus, GameStateManager
  AICombatProcessor ──────→ IEventBus, IPathfinder
  AICivilianProcessor ────→ DayNightCycleComponent
  DialogueProcessor ──────→ IEventBus, GameStateManager, WorldStateService
  QuestProcessor ─────────→ IEventBus, WorldStateService
  CraftingProcessor ──────→ IEventBus
  SurvivalProcessor ──────→ IEventBus
  RadiationProcessor ─────→ IEventBus, ZoneDefinition
  ChunkStreamingProcessor → IEventBus, WorldMapData
  DayNightCycleProcessor ─→ IEventBus
  MusicStateProcessor ────→ IEventBus, IAudioService
  InventoryProcessor ─────→ IEventBus
  ScreenManager ──────────→ IInputProvider
```

---

## Conventions

1. **Components are data.** They hold state. No game logic in components.
2. **Scripts/Processors are behavior.** They read components + services and mutate state.
3. **Events decouple systems.** Combat doesn't import Dialogue — they communicate through events.
4. **Data files define content.** Items, perks, quests, dialogues, recipes, factions are JSON. Code is generic.
5. **Interfaces at boundaries.** All services are registered and consumed by interface (`IEventBus`, `IInputProvider`, `ISaveService`, etc.).
6. **Record types for immutable data.** Definitions, events, and results are `record` or `record struct`.
7. **Naming:** `*Component` = ECS data, `*Script`/`*Processor` = ECS behavior, `*Service` = global singleton, `*Definition` = data-loaded spec, `*Event` = fired through EventBus.
