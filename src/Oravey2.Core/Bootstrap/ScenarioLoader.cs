using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Combat;
using Oravey2.Core.Dialogue;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Quests;
using Oravey2.Core.UI;
using Oravey2.Core.UI.Stride;
using Oravey2.Core.World;
using Stride.Engine;
using Stride.Graphics;

namespace Oravey2.Core.Bootstrap;

/// <summary>
/// Holds persistent game state (quest/dialogue/world state) across zone transitions.
/// All scenario loading is handled by <see cref="RegionLoader"/>.
/// </summary>
public sealed class ScenarioLoader
{
    /// <summary>
    /// The child scene containing all world entities for the current zone.
    /// Synced from RegionLoader after each load.
    /// </summary>
    public Scene? WorldScene { get; private set; }

    public SpriteFont? Font { get; set; }

    // Exposed game refs for automation handler wiring
    public InventoryComponent? PlayerInventory { get; private set; }
    public EquipmentComponent? PlayerEquipment { get; private set; }
    public HealthComponent? PlayerHealth { get; private set; }
    public CombatComponent? PlayerCombat { get; private set; }
    public LevelComponent? PlayerLevel { get; private set; }
    public StatsComponent? PlayerStats { get; private set; }
    public NotificationService? NotificationService { get; private set; }
    public GameOverOverlayScript? GameOverOverlay { get; private set; }
    public Entity? PlayerEntity { get; private set; }
    public DialogueProcessor? DialogueProcessor { get; set; }
    public DialogueContext? DialogueContext { get; set; }
    public ZoneExitTriggerScript? ZoneExitTrigger { get; set; }
    public CombatSyncScript? CombatScript { get; set; }
    public QuestTrackerScript? QuestTracker { get; set; }
    public QuestJournalScript? QuestJournal { get; set; }
    public DeathRespawnScript? DeathRespawn { get; set; }
    public VictoryCheckScript? VictoryCheck { get; set; }

    // Persistent across zone transitions
    public WorldStateService WorldState { get; } = new();
    public QuestLogComponent QuestLog { get; } = new();
    public QuestProcessor? QuestProcessor { get; private set; }
    public KillTracker? KillTracker { get; set; }

    public bool IsLoaded => WorldScene != null;
    public string? CurrentScenarioId { get; private set; }

    private bool _questSystemInitialized;

    /// <summary>
    /// Initializes the quest system (subscriptions). Call once from GameBootstrapper.
    /// </summary>
    public void InitializeQuestSystem(IEventBus eventBus)
    {
        if (_questSystemInitialized) return;
        _questSystemInitialized = true;

        QuestProcessor = new QuestProcessor(eventBus);

        eventBus.Subscribe<QuestStartRequestedEvent>(e =>
        {
            var quest = QuestChainDefinitions.GetQuest(e.QuestId);
            if (quest != null)
            {
                QuestProcessor.StartQuest(QuestLog, quest);
                WorldState.SetFlag($"{e.QuestId}_active", true);
            }
        });

        eventBus.Subscribe<QuestUpdatedEvent>(e =>
        {
            if (e.NewStatus == QuestStatus.Completed)
                WorldState.SetFlag($"{e.QuestId}_active", false);
        });
    }

    /// <summary>
    /// Syncs player and scene refs from RegionLoader after a region is loaded.
    /// </summary>
    public void SyncFromRegion(RegionLoader loader)
    {
        WorldScene = loader.WorldScene;
        PlayerEntity = loader.PlayerEntity;
        PlayerInventory = loader.PlayerInventory;
        PlayerEquipment = loader.PlayerEquipment;
        PlayerHealth = loader.PlayerHealth;
        PlayerCombat = loader.PlayerCombat;
        PlayerLevel = loader.PlayerLevel;
        PlayerStats = loader.PlayerStats;
        NotificationService = loader.NotificationService;
        GameOverOverlay = loader.GameOverOverlay;
        QuestJournal = loader.QuestJournal;
        CurrentScenarioId = loader.CurrentRegionName;
    }

    public void Unload(Scene rootScene)
    {
        if (WorldScene != null)
        {
            rootScene.Children.Remove(WorldScene);
            WorldScene = null;
        }

        PlayerInventory = null;
        PlayerEquipment = null;
        PlayerHealth = null;
        PlayerCombat = null;
        PlayerLevel = null;
        PlayerStats = null;
        NotificationService = null;
        GameOverOverlay = null;
        PlayerEntity = null;
        DialogueProcessor = null;
        DialogueContext = null;
        ZoneExitTrigger = null;
        CombatScript = null;
        QuestTracker = null;
        QuestJournal = null;
        KillTracker = null;
        DeathRespawn = null;
        VictoryCheck = null;
        CurrentScenarioId = null;
        // Note: WorldState, QuestLog, QuestProcessor persist across zone transitions
    }
}
