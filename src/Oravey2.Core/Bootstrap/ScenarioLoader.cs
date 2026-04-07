using Microsoft.Extensions.Logging;
using Oravey2.Core.Camera;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Content;
using Oravey2.Core.Dialogue;
using Oravey2.Core.NPC;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Combat;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Loot;
using Oravey2.Core.Player;
using Oravey2.Core.Quests;
using Oravey2.Core.UI;
using Oravey2.Core.UI.Stride;
using Oravey2.Core.World;
using Oravey2.Core.World.Serialization;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

namespace Oravey2.Core.Bootstrap;

/// <summary>
/// Loads/unloads game world scenarios into a child scene.
/// Each zone gets its own Scene, swapped atomically on transition.
/// </summary>
public sealed class ScenarioLoader
{
    /// <summary>
    /// The child scene containing all world entities for the current zone.
    /// Null when no scenario is loaded.
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
    public DialogueProcessor? DialogueProcessor { get; private set; }
    public DialogueContext? DialogueContext { get; private set; }
    public ZoneExitTriggerScript? ZoneExitTrigger { get; private set; }
    public CombatSyncScript? CombatScript { get; private set; }
    public QuestTrackerScript? QuestTracker { get; private set; }
    public QuestJournalScript? QuestJournal { get; private set; }
    public DeathRespawnScript? DeathRespawn { get; private set; }
    public VictoryCheckScript? VictoryCheck { get; private set; }

    // Persistent across zone transitions
    public WorldStateService WorldState { get; } = new();
    public QuestLogComponent QuestLog { get; } = new();
    public QuestProcessor? QuestProcessor { get; private set; }
    public KillTracker? KillTracker { get; private set; }

    public bool IsLoaded => WorldScene != null;
    public string? CurrentScenarioId { get; private set; }

    private bool _questSystemInitialized;

    // Content pack support: optional data-driven content
    private ContentPackLoader? _contentPack;
    private Dictionary<string, DialogueTree>? _loadedDialogues;
    private Dictionary<string, QuestDefinition>? _loadedQuests;

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
            var quest = ResolveQuest(e.QuestId);
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

    public void Load(string scenarioId, Scene rootScene, Game game,
        Entity cameraEntity, GameStateManager gameStateManager,
        IEventBus eventBus, IInputProvider inputProvider, ILogger logger)
    {
        if (IsLoaded)
            Unload(rootScene);

        CurrentScenarioId = scenarioId;

        // Try to discover an active content pack
        TryLoadContentPack(logger);

        // Create a fresh child scene for this zone
        WorldScene = new Scene();
        rootScene.Children.Add(WorldScene);

        switch (scenarioId)
        {
            case "m0_combat":
                LoadM0Combat(WorldScene, game, cameraEntity, gameStateManager, eventBus, inputProvider, logger);
                break;
            case "empty":
                LoadEmpty(WorldScene, game, cameraEntity, gameStateManager, eventBus, inputProvider, logger);
                break;
            case "town":
                LoadTown(WorldScene, game, cameraEntity, gameStateManager, eventBus, inputProvider, logger);
                break;
            case "wasteland":
                LoadWasteland(WorldScene, game, cameraEntity, gameStateManager, eventBus, inputProvider, logger);
                break;
            case "terrain_test":
                LoadTerrainTest(WorldScene, game, cameraEntity, gameStateManager, eventBus, inputProvider, logger);
                break;
            default:
                var customMapDir = Path.Combine(AppContext.BaseDirectory, "Maps", scenarioId);
                if (Directory.Exists(customMapDir))
                {
                    logger.LogInformation("Loading custom compiled map: {Id} from {Dir}", scenarioId, customMapDir);
                    LoadFromCompiledMap(scenarioId, customMapDir, WorldScene, game, cameraEntity, gameStateManager, eventBus, inputProvider, logger);
                }
                else
                {
                    logger.LogWarning("Unknown scenario: {Id}, falling back to m0_combat", scenarioId);
                    LoadM0Combat(WorldScene, game, cameraEntity, gameStateManager, eventBus, inputProvider, logger);
                }
                break;
        }

        logger.LogInformation("Scenario '{Id}' loaded with {Count} entities", scenarioId, WorldScene.Entities.Count);
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

    private Entity AddEntity(Entity entity, Scene worldScene)
    {
        worldScene.Entities.Add(entity);
        return entity;
    }

    // ---- Shared player creation used by all scenarios ----

    private (Entity player, PlayerMovementScript movement, StatsComponent stats,
        LevelComponent level, HealthComponent health, CombatComponent combat,
        InventoryComponent inventory, EquipmentComponent equipment, InventoryProcessor processor)
        CreatePlayer(Scene rootScene, Game game, Entity cameraEntity,
            GameStateManager gameStateManager, IEventBus eventBus)
    {
        var playerEntity = new Entity("Player");
        playerEntity.Transform.Position = new Vector3(0, 0.5f, 0);

        var playerVisual = new Entity("PlayerVisual");
        var capsuleMesh = GeometricPrimitive.Capsule.New(game.GraphicsDevice, 0.3f, 0.8f).ToMeshDraw();
        var playerModel = new Model();
        playerModel.Meshes.Add(new Mesh { Draw = capsuleMesh });
        var playerMaterial = game.CreateMaterial(new Color(0.2f, 0.7f, 0.3f));
        playerModel.Materials.Add(playerMaterial);
        playerVisual.Add(new ModelComponent(playerModel));
        playerEntity.AddChild(playerVisual);

        var playerMovement = new PlayerMovementScript { MoveSpeed = 5f };
        playerMovement.StateManager = gameStateManager;
        playerEntity.Add(playerMovement);

        // Wire camera target
        var cameraScript = cameraEntity.Get<TacticalCameraScript>();
        if (cameraScript != null)
            cameraScript.Target = playerEntity;
        playerMovement.CameraScript = cameraScript;

        AddEntity(playerEntity, rootScene);

        var stats = new StatsComponent();
        var level = new LevelComponent(stats);
        var health = new HealthComponent(stats, level, eventBus);
        var combat = new CombatComponent { InCombat = false };
        var inventory = new InventoryComponent(stats);
        var equipment = new EquipmentComponent();
        var processor = new InventoryProcessor(inventory, equipment, eventBus);

        // Starting equipment
        var startingWeapon = new ItemInstance(M0Items.PipeWrench());
        processor.TryPickup(startingWeapon);
        processor.TryEquip(startingWeapon, EquipmentSlot.PrimaryWeapon);
        processor.TryPickup(new ItemInstance(M0Items.Medkit(), 2));

        PlayerEntity = playerEntity;
        PlayerStats = stats;
        PlayerLevel = level;
        PlayerHealth = health;
        PlayerCombat = combat;
        PlayerInventory = inventory;
        PlayerEquipment = equipment;

        return (playerEntity, playerMovement, stats, level, health, combat, inventory, equipment, processor);
    }

    // ---- m0_combat: Full M0 world with enemies ----

    private void LoadM0Combat(Scene rootScene, Game game, Entity cameraEntity,
        GameStateManager gameStateManager, IEventBus eventBus, IInputProvider inputProvider, ILogger logger)
    {
        var (playerEntity, playerMovement, playerStats, playerLevel, playerHealth,
            playerCombat, playerInventory, playerEquipment, inventoryProcessor)
            = CreatePlayer(rootScene, game, cameraEntity, gameStateManager, eventBus);

        // Tile map
        var mapData = TileMapData.CreateDefault(32, 32);
        var tileMapEntity = new Entity("TileMap");
        var tileMapRenderer = new HeightmapTerrainScript { MapData = mapData };
        tileMapEntity.Add(tileMapRenderer);
        AddEntity(tileMapEntity, rootScene);

        playerMovement.MapData = mapData;
        playerMovement.TileSize = tileMapRenderer.TileSize;

        // Enemies
        var enemyPositions = new (string id, Vector3 pos)[]
        {
            ("enemy_1", new Vector3(8f, 0.5f, 8f)),
            ("enemy_2", new Vector3(-6f, 0.5f, 10f)),
            ("enemy_3", new Vector3(10f, 0.5f, -6f)),
        };
        var enemyMaterial = game.CreateMaterial(new Color(0.8f, 0.15f, 0.15f));
        var enemies = new List<EnemyInfo>();

        foreach (var (id, pos) in enemyPositions)
        {
            var enemyEntity = new Entity(id);
            enemyEntity.Transform.Position = pos;
            var enemyVisual = new Entity($"{id}_Visual");
            var enemyMesh = GeometricPrimitive.Capsule.New(game.GraphicsDevice, 0.3f, 0.8f).ToMeshDraw();
            var enemyModel = new Model();
            enemyModel.Meshes.Add(new Mesh { Draw = enemyMesh });
            enemyModel.Materials.Add(enemyMaterial);
            enemyVisual.Add(new ModelComponent(enemyModel));
            enemyEntity.AddChild(enemyVisual);
            AddEntity(enemyEntity, rootScene);

            var enemyStats = new StatsComponent(new Dictionary<Stat, int>
            {
                { Stat.Strength, 3 }, { Stat.Perception, 3 }, { Stat.Endurance, 1 },
                { Stat.Charisma, 2 }, { Stat.Intelligence, 2 }, { Stat.Agility, 4 },
                { Stat.Luck, 3 }
            });
            var enemyLevel = new LevelComponent(enemyStats);
            var enemyHealth = new HealthComponent(enemyStats, enemyLevel, eventBus);
            var enemyCombat = new CombatComponent { InCombat = false };

            enemies.Add(new EnemyInfo
            {
                Entity = enemyEntity, Id = id,
                Health = enemyHealth, Combat = enemyCombat,
                Stats = enemyStats, Weapon = M0Items.RustyShiv().Weapon,
            });
        }

        // Combat manager
        var damageResolver = new DamageResolver();
        var combatEngine = new CombatEngine(damageResolver, eventBus);
        var actionQueue = new ActionQueue();
        var combatStateManager = new CombatStateManager(eventBus, gameStateManager);

        var combatManagerEntity = new Entity("CombatManager");
        var combatScript = new CombatSyncScript
        {
            Player = playerEntity, PlayerHealth = playerHealth,
            PlayerCombat = playerCombat, PlayerEquipment = playerEquipment,
            PlayerStats = playerStats, Engine = combatEngine,
            Queue = actionQueue, CombatState = combatStateManager,
            StateManager = gameStateManager, EventBus = eventBus,
        };
        combatScript.Enemies = enemies;

        var lootTable = new LootTable();
        lootTable.Add(M0Items.ScrapMetal(), 0.7f);
        lootTable.Add(M0Items.Medkit(), 0.3f);
        lootTable.Add(M0Items.PipeWrench(), 0.15f);
        lootTable.Add(M0Items.LeatherJacket(), 0.1f);
        var lootDropScript = new LootDropScript { LootTable = lootTable };
        combatManagerEntity.Add(lootDropScript);
        combatScript.LootDrop = lootDropScript;

        var lootPickup = new LootPickupScript
        {
            Processor = inventoryProcessor, EventBus = eventBus, PickupRadius = 1.5f,
        };
        playerEntity.Add(lootPickup);

        var encounterTrigger = new EncounterTriggerScript
        {
            Player = playerEntity, StateManager = gameStateManager,
            CombatState = combatStateManager, TriggerRadius = 5f,
        };
        encounterTrigger.Enemies = enemies;

        combatManagerEntity.Add(combatScript);
        combatManagerEntity.Add(encounterTrigger);
        AddEntity(combatManagerEntity, rootScene);
        CombatScript = combatScript;

        // HUD
        var hudEntity = new Entity("HUD");
        hudEntity.Add(new HudSyncScript
        {
            Health = playerHealth, Combat = playerCombat,
            Level = playerLevel, Inventory = playerInventory,
            StateManager = gameStateManager,
            Font = Font,
        });
        AddEntity(hudEntity, rootScene);

        // Inventory overlay
        var inventoryOverlayEntity = new Entity("InventoryOverlay");
        inventoryOverlayEntity.Add(new InventoryOverlayScript
        {
            Inventory = playerInventory, StateManager = gameStateManager,
            InputProvider = inputProvider,
            Font = Font,
        });
        AddEntity(inventoryOverlayEntity, rootScene);

        // Notification feed
        var notificationService = new NotificationService();
        eventBus.Subscribe<NotificationEvent>(e => notificationService.Add(e.Message, e.DurationSeconds));
        NotificationService = notificationService;

        var notificationEntity = new Entity("NotificationFeed");
        notificationEntity.Add(new NotificationFeedScript { Notifications = notificationService, Font = Font });
        AddEntity(notificationEntity, rootScene);

        // Enemy HP bars
        var enemyHpEntity = new Entity("EnemyHpBars");
        var enemyHpBars = new EnemyHpBarScript
        {
            StateManager = gameStateManager, CameraEntity = cameraEntity,
            Font = Font,
        };
        enemyHpBars.Enemies = enemies;
        enemyHpEntity.Add(enemyHpBars);
        AddEntity(enemyHpEntity, rootScene);

        // Game over overlay
        var gameOverEntity = new Entity("GameOverOverlay");
        var gameOverOverlay = new GameOverOverlayScript { StateManager = gameStateManager, Font = Font };
        gameOverEntity.Add(gameOverOverlay);
        AddEntity(gameOverEntity, rootScene);
        GameOverOverlay = gameOverOverlay;

        // Floating damage
        var floatingDamageEntity = new Entity("FloatingDamage");
        floatingDamageEntity.Add(new FloatingDamageScript
        {
            CameraEntity = cameraEntity, EventBus = eventBus, CombatScript = combatScript,
            Font = Font,
        });
        AddEntity(floatingDamageEntity, rootScene);
    }

    // ---- terrain_test: Heightmap terrain test scene (3×3 chunk grid) ----

    private void LoadTerrainTest(Scene rootScene, Game game, Entity cameraEntity,
        GameStateManager gameStateManager, IEventBus eventBus, IInputProvider inputProvider, ILogger logger)
    {
        var (playerEntity, playerMovement, playerStats, playerLevel, playerHealth,
            playerCombat, playerInventory, playerEquipment, inventoryProcessor)
            = CreatePlayer(rootScene, game, cameraEntity, gameStateManager, eventBus);

        var mapData = TerrainTestData.CreateTestMap();
        var worldMap = TerrainTestData.CreateTestWorldMap(mapData);
        var tileMapEntity = new Entity("TileMap");
        var tileMapRenderer = new HeightmapTerrainScript { MapData = mapData, WorldMap = worldMap };
        tileMapEntity.Add(tileMapRenderer);
        AddEntity(tileMapEntity, rootScene);

        playerMovement.MapData = mapData;
        playerMovement.TileSize = tileMapRenderer.TileSize;

        var notificationService = new NotificationService();
        eventBus.Subscribe<NotificationEvent>(e => notificationService.Add(e.Message, e.DurationSeconds));
        NotificationService = notificationService;

        var notificationEntity = new Entity("NotificationFeed");
        notificationEntity.Add(new NotificationFeedScript { Notifications = notificationService, Font = Font });
        AddEntity(notificationEntity, rootScene);
    }

    // ---- empty: Minimal world for smoke tests ----

    private void LoadEmpty(Scene rootScene, Game game, Entity cameraEntity,
        GameStateManager gameStateManager, IEventBus eventBus, IInputProvider inputProvider, ILogger logger)
    {
        var (playerEntity, playerMovement, playerStats, playerLevel, playerHealth,
            playerCombat, playerInventory, playerEquipment, inventoryProcessor)
            = CreatePlayer(rootScene, game, cameraEntity, gameStateManager, eventBus);

        // Basic tile map
        var mapData = TileMapData.CreateDefault(32, 32);
        var tileMapEntity = new Entity("TileMap");
        var tileMapRenderer = new HeightmapTerrainScript { MapData = mapData };
        tileMapEntity.Add(tileMapRenderer);
        AddEntity(tileMapEntity, rootScene);

        playerMovement.MapData = mapData;
        playerMovement.TileSize = tileMapRenderer.TileSize;

        // Notification feed (needed for save notifications)
        var notificationService = new NotificationService();
        eventBus.Subscribe<NotificationEvent>(e => notificationService.Add(e.Message, e.DurationSeconds));
        NotificationService = notificationService;

        var notificationEntity = new Entity("NotificationFeed");
        notificationEntity.Add(new NotificationFeedScript { Notifications = notificationService, Font = Font });
        AddEntity(notificationEntity, rootScene);

        // Minimal HUD
        var hudEntity = new Entity("HUD");
        hudEntity.Add(new HudSyncScript
        {
            Health = playerHealth, Combat = playerCombat,
            Level = playerLevel, Inventory = playerInventory,
            StateManager = gameStateManager,
            Font = Font,
        });
        AddEntity(hudEntity, rootScene);

        // Empty combat manager (some tests query it)
        var combatManagerEntity = new Entity("CombatManager");
        var combatStateManager = new CombatStateManager(eventBus, gameStateManager);
        var damageResolver = new DamageResolver();
        var combatEngine = new CombatEngine(damageResolver, eventBus);
        var combatScript = new CombatSyncScript
        {
            Player = playerEntity, PlayerHealth = playerHealth,
            PlayerCombat = playerCombat, PlayerEquipment = playerEquipment,
            PlayerStats = playerStats, Engine = combatEngine,
            Queue = new ActionQueue(), CombatState = combatStateManager,
            StateManager = gameStateManager, EventBus = eventBus,
        };
        combatScript.Enemies = [];
        var encounterTrigger = new EncounterTriggerScript
        {
            Player = playerEntity, StateManager = gameStateManager,
            CombatState = combatStateManager, TriggerRadius = 5f,
        };
        encounterTrigger.Enemies = [];
        combatManagerEntity.Add(combatScript);
        combatManagerEntity.Add(encounterTrigger);
        AddEntity(combatManagerEntity, rootScene);
        CombatScript = combatScript;

        // Inventory overlay
        var inventoryOverlayEntity = new Entity("InventoryOverlay");
        inventoryOverlayEntity.Add(new InventoryOverlayScript
        {
            Inventory = playerInventory, StateManager = gameStateManager,
            Font = Font,
        });
        AddEntity(inventoryOverlayEntity, rootScene);

        // Game over overlay
        var gameOverEntity = new Entity("GameOverOverlay");
        var gameOverOverlay = new GameOverOverlayScript { StateManager = gameStateManager, Font = Font };
        gameOverEntity.Add(gameOverOverlay);
        AddEntity(gameOverEntity, rootScene);
        GameOverOverlay = gameOverOverlay;
    }

    // ---- town: Haven settlement (no enemies, NPCs added in later sub-phases) ----

    private void LoadTown(Scene rootScene, Game game, Entity cameraEntity,
        GameStateManager gameStateManager, IEventBus eventBus, IInputProvider inputProvider, ILogger logger)
    {
        var (playerEntity, playerMovement, playerStats, playerLevel, playerHealth,
            playerCombat, playerInventory, playerEquipment, inventoryProcessor)
            = CreatePlayer(rootScene, game, cameraEntity, gameStateManager, eventBus);

        // Town tile map
        var mapData = TownMapBuilder.CreateTownMap();
        var tileMapEntity = new Entity("TileMap");
        var tileMapRenderer = new HeightmapTerrainScript { MapData = mapData };
        tileMapEntity.Add(tileMapRenderer);
        AddEntity(tileMapEntity, rootScene);

        playerMovement.MapData = mapData;
        playerMovement.TileSize = tileMapRenderer.TileSize;

        // Notification feed
        var notificationService = new NotificationService();
        eventBus.Subscribe<NotificationEvent>(e => notificationService.Add(e.Message, e.DurationSeconds));
        NotificationService = notificationService;

        var notificationEntity = new Entity("NotificationFeed");
        notificationEntity.Add(new NotificationFeedScript { Notifications = notificationService, Font = Font });
        AddEntity(notificationEntity, rootScene);

        // HUD
        var hudEntity = new Entity("HUD");
        hudEntity.Add(new HudSyncScript
        {
            Health = playerHealth, Combat = playerCombat,
            Level = playerLevel, Inventory = playerInventory,
            StateManager = gameStateManager,
            Font = Font,
        });
        AddEntity(hudEntity, rootScene);

        // Inventory overlay
        var inventoryOverlayEntity = new Entity("InventoryOverlay");
        inventoryOverlayEntity.Add(new InventoryOverlayScript
        {
            Inventory = playerInventory, StateManager = gameStateManager,
            InputProvider = inputProvider,
            Font = Font,
        });
        AddEntity(inventoryOverlayEntity, rootScene);

        // Game over overlay
        var gameOverEntity = new Entity("GameOverOverlay");
        var gameOverOverlay = new GameOverOverlayScript { StateManager = gameStateManager, Font = Font };
        gameOverEntity.Add(gameOverOverlay);
        AddEntity(gameOverEntity, rootScene);
        GameOverOverlay = gameOverOverlay;

        // Death respawn script
        var deathRespawnEntity = new Entity("DeathRespawn");
        var deathRespawnScript = new DeathRespawnScript
        {
            PlayerHealth = playerHealth,
            PlayerInventory = playerInventory,
            GameStateManager = gameStateManager,
            DeathOverlay = gameOverOverlay,
            Notifications = NotificationService,
        };
        deathRespawnEntity.Add(deathRespawnScript);
        AddEntity(deathRespawnEntity, rootScene);
        DeathRespawn = deathRespawnScript;

        // Dialogue system
        var dialogueProcessor = new DialogueProcessor(eventBus);
        var skills = new SkillsComponent(playerStats);
        var dialogueContext = new DialogueContext(skills, playerInventory, WorldState, playerLevel, eventBus);
        DialogueProcessor = dialogueProcessor;
        DialogueContext = dialogueContext;

        // NPC interaction → start dialogue
        eventBus.Subscribe<NpcInteractionEvent>(e =>
        {
            if (dialogueProcessor.IsActive) return;
            var tree = ResolveDialogue(e.DialogueTreeId);
            if (tree != null)
            {
                dialogueProcessor.StartDialogue(tree);
                gameStateManager.TransitionTo(GameState.InDialogue);
            }
        });

        // Dialogue overlay
        var dialogueOverlayEntity = new Entity("DialogueOverlay");
        dialogueOverlayEntity.Add(new DialogueOverlayScript
        {
            Processor = dialogueProcessor,
            Context = dialogueContext,
            StateManager = gameStateManager,
            InputProvider = inputProvider,
            Font = Font,
            PlayerEntity = playerEntity,
        });
        AddEntity(dialogueOverlayEntity, rootScene);

        // NPCs
        SpawnNpcs(rootScene, game, playerEntity, inputProvider, eventBus, gameStateManager);

        // Quest evaluation script (evaluates active quests each frame)
        if (QuestProcessor != null)
        {
            var questContext = new QuestContext(playerInventory, WorldState, playerLevel, QuestLog, eventBus);
            var questEvalEntity = new Entity("QuestEval");
            questEvalEntity.Add(new QuestEvalScript
            {
                Processor = QuestProcessor,
                QuestLog = QuestLog,
                Context = questContext,
                StateManager = gameStateManager,
            });
            AddEntity(questEvalEntity, rootScene);
        }

        // Quest HUD tracker (top-right, shows active objective)
        var questTrackerEntity = new Entity("QuestTracker");
        var questTrackerScript = new QuestTrackerScript
        {
            QuestLog = QuestLog,
            WorldState = WorldState,
            StateManager = gameStateManager,
            Font = Font,
        };
        questTrackerEntity.Add(questTrackerScript);
        AddEntity(questTrackerEntity, rootScene);
        QuestTracker = questTrackerScript;

        // Quest journal overlay (J key)
        var questJournalEntity = new Entity("QuestJournal");
        var questJournalScript = new QuestJournalScript
        {
            QuestLog = QuestLog,
            WorldState = WorldState,
            StateManager = gameStateManager,
            InputProvider = inputProvider,
            Font = Font,
        };
        questJournalEntity.Add(questJournalScript);
        AddEntity(questJournalEntity, rootScene);
        QuestJournal = questJournalScript;

        // Victory check script (detects m1_complete flag)
        var victoryCheckEntity = new Entity("VictoryCheck");
        var victoryCheckScript = new VictoryCheckScript
        {
            Overlay = gameOverOverlay,
            WorldState = WorldState,
            StateManager = gameStateManager,
        };
        victoryCheckEntity.Add(victoryCheckScript);
        AddEntity(victoryCheckEntity, rootScene);
        VictoryCheck = victoryCheckScript;

        // Zone exit trigger at gate (tile 30,17 / 30,18 → world ~14.5, 0.5, 2.0)
        var zoneExitEntity = new Entity("ZoneExitTrigger");
        zoneExitEntity.Transform.Position = new Vector3(14.5f, 0.5f, 2.0f);
        var zoneExitScript = new ZoneExitTriggerScript
        {
            Player = playerEntity,
            TargetZoneId = "wasteland",
            TargetSpawnPosition = new Vector3(0f, 0.5f, 0f),
            TriggerRadius = 1.5f,
            StateManager = gameStateManager,
        };
        zoneExitEntity.Add(zoneExitScript);
        AddEntity(zoneExitEntity, rootScene);
        ZoneExitTrigger = zoneExitScript;
    }

    // ---- wasteland: Scorched Outskirts with enemies ----

    private void LoadWasteland(Scene rootScene, Game game, Entity cameraEntity,
        GameStateManager gameStateManager, IEventBus eventBus, IInputProvider inputProvider, ILogger logger)
    {
        var (playerEntity, playerMovement, playerStats, playerLevel, playerHealth,
            playerCombat, playerInventory, playerEquipment, inventoryProcessor)
            = CreatePlayer(rootScene, game, cameraEntity, gameStateManager, eventBus);

        // Wasteland tile map
        var mapData = WastelandMapBuilder.CreateWastelandMap();
        var tileMapEntity = new Entity("TileMap");
        var tileMapRenderer = new HeightmapTerrainScript { MapData = mapData };
        tileMapEntity.Add(tileMapRenderer);
        AddEntity(tileMapEntity, rootScene);

        playerMovement.MapData = mapData;
        playerMovement.TileSize = tileMapRenderer.TileSize;

        // Notification feed
        var notificationService = new NotificationService();
        eventBus.Subscribe<NotificationEvent>(e => notificationService.Add(e.Message, e.DurationSeconds));
        NotificationService = notificationService;

        var notificationEntity = new Entity("NotificationFeed");
        notificationEntity.Add(new NotificationFeedScript { Notifications = notificationService, Font = Font });
        AddEntity(notificationEntity, rootScene);

        // Enemies — spawn from content pack or hardcoded defaults
        var spawnPoints = ResolveEnemySpawnPoints();

        var enemySpawner = new EnemySpawner(game, eventBus);

        // KillTracker — wire enemy death → counter/flag updates
        var killTracker = new KillTracker(WorldState, eventBus);
        killTracker.RegisterCounter("radrat", "rats_killed");
        killTracker.RegisterFlag("scar", "scar_killed");
        KillTracker = killTracker;

        // Combat manager
        var damageResolver = new DamageResolver();
        var combatEngine = new CombatEngine(damageResolver, eventBus);
        var combatStateManager = new CombatStateManager(eventBus, gameStateManager);

        var combatManagerEntity = new Entity("CombatManager");
        var combatScript = new CombatSyncScript
        {
            Player = playerEntity, PlayerHealth = playerHealth,
            PlayerCombat = playerCombat, PlayerEquipment = playerEquipment,
            PlayerStats = playerStats, Engine = combatEngine,
            Queue = new ActionQueue(), CombatState = combatStateManager,
            StateManager = gameStateManager, EventBus = eventBus,
        };
        combatScript.Enemies = [];

        var encounterTrigger = new EncounterTriggerScript
        {
            Player = playerEntity, StateManager = gameStateManager,
            CombatState = combatStateManager, TriggerRadius = 5f,
        };
        encounterTrigger.Enemies = combatScript.Enemies;

        // Spawn enemies into combat system
        enemySpawner.SpawnFromPoints(rootScene, spawnPoints, combatScript, encounterTrigger);

        var lootTable = new LootTable();
        lootTable.Add(M0Items.ScrapMetal(), 0.7f);
        lootTable.Add(M0Items.Medkit(), 0.3f);
        var lootDropScript = new LootDropScript { LootTable = lootTable };
        combatManagerEntity.Add(lootDropScript);
        combatScript.LootDrop = lootDropScript;

        var lootPickup = new LootPickupScript
        {
            Processor = inventoryProcessor, EventBus = eventBus, PickupRadius = 1.5f,
        };
        playerEntity.Add(lootPickup);

        combatManagerEntity.Add(combatScript);
        combatManagerEntity.Add(encounterTrigger);
        AddEntity(combatManagerEntity, rootScene);
        CombatScript = combatScript;

        // HUD
        var hudEntity = new Entity("HUD");
        hudEntity.Add(new HudSyncScript
        {
            Health = playerHealth, Combat = playerCombat,
            Level = playerLevel, Inventory = playerInventory,
            StateManager = gameStateManager,
            Font = Font,
        });
        AddEntity(hudEntity, rootScene);

        // Inventory overlay
        var inventoryOverlayEntity = new Entity("InventoryOverlay");
        inventoryOverlayEntity.Add(new InventoryOverlayScript
        {
            Inventory = playerInventory, StateManager = gameStateManager,
            InputProvider = inputProvider,
            Font = Font,
        });
        AddEntity(inventoryOverlayEntity, rootScene);

        // Enemy HP bars
        var enemyHpEntity = new Entity("EnemyHpBars");
        var enemyHpBars = new EnemyHpBarScript
        {
            StateManager = gameStateManager, CameraEntity = cameraEntity,
            Font = Font,
        };
        enemyHpBars.Enemies = combatScript.Enemies;
        enemyHpEntity.Add(enemyHpBars);
        AddEntity(enemyHpEntity, rootScene);

        // Game over overlay
        var gameOverEntity = new Entity("GameOverOverlay");
        var gameOverOverlay = new GameOverOverlayScript { StateManager = gameStateManager, Font = Font };
        gameOverEntity.Add(gameOverOverlay);
        AddEntity(gameOverEntity, rootScene);
        GameOverOverlay = gameOverOverlay;

        // Death respawn script
        var deathRespawnEntity = new Entity("DeathRespawn");
        var deathRespawnScript = new DeathRespawnScript
        {
            PlayerHealth = playerHealth,
            PlayerInventory = playerInventory,
            GameStateManager = gameStateManager,
            DeathOverlay = gameOverOverlay,
            Notifications = NotificationService,
        };
        deathRespawnEntity.Add(deathRespawnScript);
        AddEntity(deathRespawnEntity, rootScene);
        DeathRespawn = deathRespawnScript;

        // Victory check script (detects m1_complete flag)
        var victoryCheckEntity = new Entity("VictoryCheck");
        var victoryCheckScript = new VictoryCheckScript
        {
            Overlay = gameOverOverlay,
            WorldState = WorldState,
            StateManager = gameStateManager,
        };
        victoryCheckEntity.Add(victoryCheckScript);
        AddEntity(victoryCheckEntity, rootScene);
        VictoryCheck = victoryCheckScript;

        // Floating damage
        var floatingDamageEntity = new Entity("FloatingDamage");
        floatingDamageEntity.Add(new FloatingDamageScript
        {
            CameraEntity = cameraEntity, EventBus = eventBus, CombatScript = combatScript,
            Font = Font,
        });
        AddEntity(floatingDamageEntity, rootScene);

        // Quest evaluation script (evaluates active quests each frame)
        if (QuestProcessor != null)
        {
            var questContext = new QuestContext(playerInventory, WorldState, playerLevel, QuestLog, eventBus);
            var questEvalEntity = new Entity("QuestEval");
            questEvalEntity.Add(new QuestEvalScript
            {
                Processor = QuestProcessor,
                QuestLog = QuestLog,
                Context = questContext,
                StateManager = gameStateManager,
            });
            AddEntity(questEvalEntity, rootScene);
        }

        // Quest HUD tracker (top-right, shows active objective)
        var questTrackerEntity = new Entity("QuestTracker");
        var questTrackerScript = new QuestTrackerScript
        {
            QuestLog = QuestLog,
            WorldState = WorldState,
            StateManager = gameStateManager,
            Font = Font,
        };
        questTrackerEntity.Add(questTrackerScript);
        AddEntity(questTrackerEntity, rootScene);
        QuestTracker = questTrackerScript;

        // Quest journal overlay (J key)
        var questJournalEntity = new Entity("QuestJournal");
        var questJournalScript = new QuestJournalScript
        {
            QuestLog = QuestLog,
            WorldState = WorldState,
            StateManager = gameStateManager,
            InputProvider = inputProvider,
            Font = Font,
        };
        questJournalEntity.Add(questJournalScript);
        AddEntity(questJournalEntity, rootScene);
        QuestJournal = questJournalScript;

        // Zone exit trigger at west gate (tile 0,17 → world -15.5, 0.5, 1.5)
        var zoneExitEntity = new Entity("ZoneExitTrigger");
        zoneExitEntity.Transform.Position = new Vector3(-15.5f, 0.5f, 1.5f);
        var zoneExitScript = new ZoneExitTriggerScript
        {
            Player = playerEntity,
            TargetZoneId = "town",
            TargetSpawnPosition = new Vector3(0f, 0.5f, 0f),
            TriggerRadius = 1.5f,
            StateManager = gameStateManager,
        };
        zoneExitEntity.Add(zoneExitScript);
        AddEntity(zoneExitEntity, rootScene);
        ZoneExitTrigger = zoneExitScript;
    }

    private void LoadFromCompiledMap(string scenarioId, string mapDir, Scene rootScene, Game game, Entity cameraEntity,
        GameStateManager gameStateManager, IEventBus eventBus, IInputProvider inputProvider, ILogger logger)
    {
        var (playerEntity, playerMovement, playerStats, playerLevel, playerHealth,
            playerCombat, playerInventory, playerEquipment, inventoryProcessor)
            = CreatePlayer(rootScene, game, cameraEntity, gameStateManager, eventBus);

        // TODO: Step 10 will wire this to MapDataProvider/SQLite
        var mapData = new TileMapData(ChunkData.Size, ChunkData.Size);

        // Load buildings
        var buildingJsons = BuildingSerializer.LoadBuildings(mapDir);
        var buildings = buildingJsons.Select(BuildingSerializer.FromBuildingJson).ToArray();
        var buildingRegistry = new BuildingRegistry();
        foreach (var bj in buildingJsons)
        {
            var building = BuildingSerializer.FromBuildingJson(bj);
            buildingRegistry.RegisterForChunk(building, bj.Placement.ChunkX, bj.Placement.ChunkY);
        }

        // Load props
        var propJsons = BuildingSerializer.LoadProps(mapDir);
        var props = propJsons.Select(BuildingSerializer.FromPropJson).ToArray();

        // Apply footprints to walkability
        foreach (var b in buildings)
            BuildingPlacer.ApplyFootprint(mapData, b);
        foreach (var p in props)
            BuildingPlacer.ApplyPropFootprint(mapData, p);

        // Tile map renderer with all features
        var tileMapEntity = new Entity("TileMap");
        var tileMapRenderer = new HeightmapTerrainScript
        {
            MapData = mapData,
            Buildings = buildingRegistry,
            Props = props,
        };
        tileMapEntity.Add(tileMapRenderer);
        AddEntity(tileMapEntity, rootScene);

        playerMovement.MapData = mapData;
        playerMovement.TileSize = tileMapRenderer.TileSize;

        // Notification feed
        var notificationService = new NotificationService();
        eventBus.Subscribe<NotificationEvent>(e => notificationService.Add(e.Message, e.DurationSeconds));
        NotificationService = notificationService;

        var notificationEntity = new Entity("NotificationFeed");
        notificationEntity.Add(new NotificationFeedScript { Notifications = notificationService, Font = Font });
        AddEntity(notificationEntity, rootScene);

        // HUD
        var hudEntity = new Entity("HUD");
        hudEntity.Add(new HudSyncScript
        {
            Health = playerHealth, Combat = playerCombat,
            Level = playerLevel, Inventory = playerInventory,
            StateManager = gameStateManager,
            Font = Font,
        });
        AddEntity(hudEntity, rootScene);

        // Inventory overlay
        var inventoryOverlayEntity = new Entity("InventoryOverlay");
        inventoryOverlayEntity.Add(new InventoryOverlayScript
        {
            Inventory = playerInventory, StateManager = gameStateManager,
            InputProvider = inputProvider,
            Font = Font,
        });
        AddEntity(inventoryOverlayEntity, rootScene);

        // Game over overlay
        var gameOverEntity = new Entity("GameOverOverlay");
        var gameOverOverlay = new GameOverOverlayScript { StateManager = gameStateManager, Font = Font };
        gameOverEntity.Add(gameOverOverlay);
        AddEntity(gameOverEntity, rootScene);
        GameOverOverlay = gameOverOverlay;
    }

    private void SpawnNpcs(Scene rootScene, Game game,
        Entity playerEntity, IInputProvider inputProvider, IEventBus eventBus, GameStateManager gameStateManager)
    {
        var npcs = ResolveNpcs();

        foreach (var (def, pos, color) in npcs)
        {
            var npcEntity = new Entity($"npc_{def.Id}");
            npcEntity.Transform.Position = pos;

            // Capsule visual
            var visual = new Entity($"npc_{def.Id}_Visual");
            var mesh = GeometricPrimitive.Capsule.New(game.GraphicsDevice, 0.3f, 0.8f).ToMeshDraw();
            var model = new Model();
            model.Meshes.Add(new Mesh { Draw = mesh });
            model.Materials.Add(game.CreateMaterial(color));
            visual.Add(new ModelComponent(model));
            npcEntity.AddChild(visual);

            // NPC component
            npcEntity.Add(new NpcComponent { Definition = def });

            // Floating name label (offset above capsule)
            var labelEntity = new Entity($"npc_{def.Id}_Label");
            labelEntity.Transform.Position = new Vector3(0, 1.2f, 0);
            labelEntity.Add(new NpcNameLabelScript
            {
                DisplayName = def.DisplayName,
                LabelColor = color,
                Font = Font,
            });
            npcEntity.AddChild(labelEntity);

            // Interaction trigger
            npcEntity.Add(new InteractionTriggerScript
            {
                Player = playerEntity,
                NpcDef = def,
                InputProvider = inputProvider,
                EventBus = eventBus,
                StateManager = gameStateManager,
            });

            AddEntity(npcEntity, rootScene);
        }
    }

    // ---- Content pack resolution helpers ----

    private void TryLoadContentPack(ILogger logger)
    {
        if (_contentPack != null) return; // already loaded

        var contentPacksDir = Path.Combine(AppContext.BaseDirectory, "ContentPacks");
        if (!Directory.Exists(contentPacksDir)) return;

        // Pick first content pack that has a manifest
        foreach (var dir in Directory.GetDirectories(contentPacksDir))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (File.Exists(manifestPath))
            {
                _contentPack = new ContentPackLoader(dir);
                logger.LogInformation("Loaded content pack from {Dir}", dir);

                // Cache dialogues and quests for lookup
                _loadedDialogues = _contentPack.LoadDialogues()
                    .ToDictionary(d => d.Id, d => d);
                _loadedQuests = _contentPack.LoadQuests()
                    .ToDictionary(q => q.Id, q => q);
                return;
            }
        }
    }

    private DialogueTree? ResolveDialogue(string treeId)
    {
        if (_loadedDialogues != null && _loadedDialogues.TryGetValue(treeId, out var loaded))
            return loaded;
        return TownDialogueTrees.GetTree(treeId);
    }

    private QuestDefinition? ResolveQuest(string questId)
    {
        if (_loadedQuests != null && _loadedQuests.TryGetValue(questId, out var loaded))
            return loaded;
        return QuestChainDefinitions.GetQuest(questId);
    }

    private (NpcDefinition def, Vector3 pos, Color color)[] ResolveNpcs()
    {
        if (_contentPack != null)
        {
            var loaded = _contentPack.LoadNpcs();
            if (loaded.Length > 0)
            {
                return loaded.Select(n => (
                    new NpcDefinition(n.Id, n.DisplayName,
                        Enum.Parse<NpcRole>(n.Role, ignoreCase: true),
                        n.DialogueTreeId),
                    new Vector3(n.Position?.X ?? 0f, n.Position?.Y ?? 0.5f, n.Position?.Z ?? 0f),
                    new Color(n.Color?.R ?? 0.6f, n.Color?.G ?? 0.6f, n.Color?.B ?? 0.6f)
                )).ToArray();
            }
        }

        // Hardcoded fallback
        return
        [
            (new NpcDefinition("elder", "Elder Tomas", NpcRole.QuestGiver, "elder_dialogue"),
                new Vector3(-4f, 0.5f, -4.5f), new Color(0.9f, 0.8f, 0.2f)),
            (new NpcDefinition("merchant", "Mara", NpcRole.Merchant, "merchant_dialogue"),
                new Vector3(1f, 0.5f, -3.5f), new Color(0.2f, 0.3f, 0.9f)),
            (new NpcDefinition("civilian_1", "Settler", NpcRole.Civilian, "civilian_dialogue"),
                new Vector3(-4f, 0.5f, 3.5f), new Color(0.6f, 0.6f, 0.6f)),
            (new NpcDefinition("civilian_2", "Settler", NpcRole.Civilian, "civilian_dialogue"),
                new Vector3(13f, 0.5f, 3.5f), new Color(0.6f, 0.6f, 0.6f)),
        ];
    }

    private List<EnemySpawnPoint> ResolveEnemySpawnPoints()
    {
        if (_contentPack != null)
        {
            var loaded = _contentPack.LoadEnemies();
            if (loaded.Length > 0)
            {
                var points = new List<EnemySpawnPoint>();
                foreach (var sp in loaded)
                {
                    // Filter conditionals: skip spawn points requiring a quest that isn't active
                    if (sp.RequiredQuestId != null &&
                        QuestLog.GetStatus(sp.RequiredQuestId) != QuestStatus.Active)
                        continue;
                    points.Add(sp);
                }
                return points;
            }
        }

        // Hardcoded fallback
        var spawnPoints = new List<EnemySpawnPoint>
        {
            new("radrat_south", -2f, -2f, Count: 1, Endurance: 1, Luck: 3, WeaponDamage: 4, WeaponAccuracy: 0.50f, Tag: "radrat"),
            new("radrat_east",   2f, -2f, Count: 1, Endurance: 1, Luck: 3, WeaponDamage: 4, WeaponAccuracy: 0.50f, Tag: "radrat"),
            new("radrat_road",  -2f,  0f, Count: 1, Endurance: 1, Luck: 3, WeaponDamage: 4, WeaponAccuracy: 0.50f, Tag: "radrat"),
        };

        if (QuestLog.GetStatus("q_raider_camp") == QuestStatus.Active)
        {
            spawnPoints.Add(new EnemySpawnPoint(
                "scar_boss", 10f, 0f, Count: 1,
                Endurance: 3, Luck: 5, WeaponDamage: 8, WeaponAccuracy: 0.65f,
                Tag: "scar"));
        }

        return spawnPoints;
    }
}
