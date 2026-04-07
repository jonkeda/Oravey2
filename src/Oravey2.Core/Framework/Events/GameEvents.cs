using Oravey2.Core.AI;
using Oravey2.Core.Audio;
using Oravey2.Core.Combat;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Quests;
using Oravey2.Core.Survival;
using Stride.Core.Mathematics;

namespace Oravey2.Core.Framework.Events;

public readonly record struct GameStateChangedEvent(
    State.GameState OldState,
    State.GameState NewState
) : IGameEvent;

public readonly record struct PlayerMovedEvent(
    Vector3 OldPosition,
    Vector3 NewPosition
) : IGameEvent;

public readonly record struct CameraZoomChangedEvent(
    float OldZoom,
    float NewZoom
) : IGameEvent;

public readonly record struct CameraRotatedEvent(
    float OldYaw,
    float NewYaw
) : IGameEvent;

public readonly record struct ZoomLevelChangedEvent(
    Camera.ZoomLevel OldLevel,
    Camera.ZoomLevel NewLevel
) : IGameEvent;

public readonly record struct HealthChangedEvent(int OldHP, int NewHP) : IGameEvent;

public readonly record struct XPGainedEvent(int Amount) : IGameEvent;

public readonly record struct LevelUpEvent(int OldLevel, int NewLevel) : IGameEvent;

public readonly record struct ItemPickedUpEvent(string ItemId) : IGameEvent;

public readonly record struct ItemDroppedEvent(string ItemId, int Count) : IGameEvent;

public readonly record struct ItemEquippedEvent(string ItemId, EquipmentSlot Slot) : IGameEvent;

public readonly record struct ItemUnequippedEvent(EquipmentSlot Slot) : IGameEvent;

public readonly record struct CombatStartedEvent(string[] EnemyIds) : IGameEvent;
public readonly record struct CombatEndedEvent() : IGameEvent;
public readonly record struct AttackResolvedEvent(
    bool Hit, int Damage, HitLocation Location, bool Critical) : IGameEvent;
public readonly record struct EntityDiedEvent(string EntityId, string? Tag) : IGameEvent;

public readonly record struct AIStateChangedEvent(string EntityId, AIState OldState, AIState NewState) : IGameEvent;
public readonly record struct AIDetectedTargetEvent(string EntityId, string TargetId) : IGameEvent;

public readonly record struct DialogueStartedEvent(string TreeId) : IGameEvent;
public readonly record struct DialogueEndedEvent(string TreeId) : IGameEvent;
public readonly record struct QuestStartRequestedEvent(string QuestId) : IGameEvent;
public readonly record struct QuestUpdatedEvent(string QuestId, QuestStatus NewStatus) : IGameEvent;
public readonly record struct QuestStageCompletedEvent(string QuestId, string StageId) : IGameEvent;
public readonly record struct JournalUpdatedEvent(string QuestId, string Text) : IGameEvent;

public readonly record struct ItemCraftedEvent(string RecipeId, string OutputItemId, int Count) : IGameEvent;
public readonly record struct ItemRepairedEvent(string ItemId, int DurabilityRestored) : IGameEvent;
public readonly record struct SurvivalThresholdChangedEvent(
    string NeedType, SurvivalThreshold OldThreshold,
    SurvivalThreshold NewThreshold) : IGameEvent;
public readonly record struct RadiationChangedEvent(int OldLevel, int NewLevel) : IGameEvent;

public readonly record struct ChunkLoadedEvent(int ChunkX, int ChunkY) : IGameEvent;
public readonly record struct ChunkUnloadedEvent(int ChunkX, int ChunkY) : IGameEvent;
public readonly record struct ZoneEnteredEvent(string ZoneId, string ZoneName) : IGameEvent;
public readonly record struct DayPhaseChangedEvent(World.DayPhase OldPhase, World.DayPhase NewPhase) : IGameEvent;
public readonly record struct FastTravelEvent(string FromId, string ToId, float InGameHoursCost) : IGameEvent;
public readonly record struct LocationDiscoveredEvent(string LocationId, string LocationName) : IGameEvent;

public readonly record struct ScreenPushedEvent(UI.ScreenId Screen) : IGameEvent;
public readonly record struct ScreenPoppedEvent(UI.ScreenId Screen) : IGameEvent;
public readonly record struct NotificationEvent(string Message, float DurationSeconds) : IGameEvent;

public readonly record struct WeatherChangedEvent(
    WeatherState OldState, WeatherState NewState) : IGameEvent;
public readonly record struct MusicLayerActivatedEvent(
    MusicLayer Layer, float FadeDuration) : IGameEvent;
public readonly record struct MusicLayerDeactivatedEvent(
    MusicLayer Layer, float FadeDuration) : IGameEvent;
public readonly record struct VolumeChangedEvent(
    AudioCategory Category, float OldVolume, float NewVolume) : IGameEvent;

public readonly record struct SaveCompletedEvent(string SlotName) : IGameEvent;
public readonly record struct LoadCompletedEvent(string SlotName) : IGameEvent;
public readonly record struct AutoSaveTriggeredEvent() : IGameEvent;
public readonly record struct SettingChangedEvent(string Key, string OldValue, string NewValue) : IGameEvent;
