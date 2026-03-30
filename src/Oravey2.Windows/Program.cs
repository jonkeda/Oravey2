using Brinell.Automation;
using Microsoft.Extensions.Logging;
using Oravey2.Core.Camera;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Combat;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.Logging;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Loot;
using Oravey2.Core.Player;
using Oravey2.Core.UI;
using Oravey2.Core.UI.Stride;
using Oravey2.Core.World;
using Oravey2.Windows;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Compositing;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Extensions;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

using var game = new Game();

game.Run(start: Start);

void Start(Scene rootScene)
{
    // --- Bootstrap services ---
    var services = ServiceLocator.Instance;

    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder
            .SetMinimumLevel(LogLevel.Debug)
            .AddSimpleConsole(options =>
            {
                options.TimestampFormat = "HH:mm:ss.fff ";
                options.SingleLine = true;
            });
    });
    var gameLoggerFactory = new GameLoggerFactory(loggerFactory);
    services.Register(gameLoggerFactory);

    var logger = gameLoggerFactory.CreateLogger("Oravey2");
    logger.LogInformation("Game starting...");

    var eventBus = new EventBus();
    var inputProvider = new KeyboardMouseInputProvider();
    var gameStateManager = new GameStateManager(eventBus, gameLoggerFactory.CreateLogger<GameStateManager>());

    services.Register<IEventBus>(eventBus);
    services.Register<IInputProvider>(inputProvider);
    services.Register(gameStateManager);

    logger.LogInformation("Services registered");

    // --- Setup base scene ---
    game.AddGraphicsCompositor().AddCleanUIStage();
    game.AddDirectionalLight();
    game.AddSkybox();

    // --- Input update entity ---
    var inputEntity = new Entity("InputManager");
    inputEntity.Add(new InputUpdateScript());
    rootScene.Entities.Add(inputEntity);

    // --- Player entity ---
    var playerEntity = new Entity("Player");
    playerEntity.Transform.Position = new Vector3(0, 0.5f, 0);

    // Visual: a small capsule as the player placeholder
    var playerVisual = new Entity("PlayerVisual");
    var capsuleMesh = GeometricPrimitive.Capsule.New(game.GraphicsDevice, 0.3f, 0.8f).ToMeshDraw();
    var playerModel = new Model();
    playerModel.Meshes.Add(new Mesh { Draw = capsuleMesh });
    var playerMaterial = game.CreateMaterial(new Color(0.2f, 0.7f, 0.3f));
    playerModel.Materials.Add(playerMaterial);
    playerVisual.Add(new ModelComponent(playerModel));
    playerEntity.AddChild(playerVisual);

    var playerMovement = new PlayerMovementScript
    {
        MoveSpeed = 5f,
    };
    playerEntity.Add(playerMovement);
    rootScene.Entities.Add(playerEntity);

    // --- Player combat data ---
    var playerStats = new StatsComponent();
    var playerLevel = new LevelComponent(playerStats);
    var playerHealth = new HealthComponent(playerStats, playerLevel, eventBus);
    var playerCombat = new CombatComponent { InCombat = false };

    // --- Player inventory (Phase B) ---
    var playerInventory = new InventoryComponent(playerStats);
    var playerEquipment = new EquipmentComponent();
    var inventoryProcessor = new InventoryProcessor(playerInventory, playerEquipment, eventBus);

    // Starting equipment
    var startingWeapon = new ItemInstance(M0Items.PipeWrench());
    inventoryProcessor.TryPickup(startingWeapon);
    inventoryProcessor.TryEquip(startingWeapon, EquipmentSlot.PrimaryWeapon);

    // Starting consumable
    inventoryProcessor.TryPickup(new ItemInstance(M0Items.Medkit(), 2));

    // --- Tile Map ---
    var mapData = TileMapData.CreateDefault(32, 32);
    var tileMapEntity = new Entity("TileMap");
    var tileMapRenderer = new TileMapRendererScript { MapData = mapData };
    tileMapEntity.Add(tileMapRenderer);
    rootScene.Entities.Add(tileMapEntity);

    // Wire tile map to player for collision
    playerMovement.MapData = mapData;
    playerMovement.TileSize = tileMapRenderer.TileSize;

    // --- Enemy entities ---
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

        rootScene.Entities.Add(enemyEntity);

        var enemyStats = new StatsComponent(new Dictionary<Stat, int>
        {
            { Stat.Strength, 5 }, { Stat.Perception, 4 }, { Stat.Endurance, 4 },
            { Stat.Charisma, 3 }, { Stat.Intelligence, 3 }, { Stat.Agility, 5 },
            { Stat.Luck, 4 }
        });
        var enemyLevel = new LevelComponent(enemyStats);
        var enemyHealth = new HealthComponent(enemyStats, enemyLevel, eventBus);
        var enemyCombat = new CombatComponent { InCombat = false };

        enemies.Add(new EnemyInfo
        {
            Entity = enemyEntity,
            Id = id,
            Health = enemyHealth,
            Combat = enemyCombat,
        });
    }

    // --- Combat Manager ---
    var damageResolver = new DamageResolver();
    var combatEngine = new CombatEngine(damageResolver, eventBus);
    var actionQueue = new ActionQueue();
    var combatStateManager = new CombatStateManager(eventBus, gameStateManager);

    var combatManagerEntity = new Entity("CombatManager");

    var combatScript = new CombatSyncScript
    {
        Player = playerEntity,
        PlayerHealth = playerHealth,
        PlayerCombat = playerCombat,
        Engine = combatEngine,
        Queue = actionQueue,
        CombatState = combatStateManager,
        StateManager = gameStateManager,
    };
    combatScript.Enemies = enemies;

    // --- Loot system (Phase B) ---
    var lootTable = new LootTable();
    lootTable.Add(M0Items.ScrapMetal(), 0.7f);
    lootTable.Add(M0Items.Medkit(), 0.3f);
    lootTable.Add(M0Items.PipeWrench(), 0.15f);
    lootTable.Add(M0Items.LeatherJacket(), 0.1f);

    var lootDropScript = new LootDropScript { LootTable = lootTable };
    combatManagerEntity.Add(lootDropScript);

    // Wire loot drop to combat
    combatScript.LootDrop = lootDropScript;

    // Loot pickup on player entity
    var lootPickup = new LootPickupScript
    {
        Processor = inventoryProcessor,
        EventBus = eventBus,
        PickupRadius = 1.5f,
    };
    playerEntity.Add(lootPickup);

    var encounterTrigger = new EncounterTriggerScript
    {
        Player = playerEntity,
        StateManager = gameStateManager,
        CombatState = combatStateManager,
        TriggerRadius = 5f,
    };
    encounterTrigger.Enemies = enemies;

    combatManagerEntity.Add(combatScript);
    combatManagerEntity.Add(encounterTrigger);
    rootScene.Entities.Add(combatManagerEntity);

    // --- Isometric Camera ---
    var cameraEntity = game.Add3DCamera(
        cameraName: "IsometricCamera",
        initialPosition: new Vector3(20, 10, 20),
        initialRotation: new Vector3(45, -30, 0),
        projectionMode: CameraProjectionMode.Orthographic);
    var cameraScript = new IsometricCameraScript
    {
        Target = playerEntity,
        Pitch = 30f,
        Yaw = 45f,
        Distance = 50f,
        CurrentFov = 25f,
        FovMin = 10f,
        FovMax = 50f,
        RotationSpeed = 120f,
        ZoomSpeed = 15f
    };
    cameraEntity.Add(cameraScript);

    // Wire camera script to player movement for yaw-relative direction
    playerMovement.CameraScript = cameraScript;

    // --- HUD (Phase B) ---
    var hudEntity = new Entity("HUD");
    var hudScript = new HudSyncScript
    {
        Health = playerHealth,
        Combat = playerCombat,
        Level = playerLevel,
        StateManager = gameStateManager,
    };
    hudEntity.Add(hudScript);
    rootScene.Entities.Add(hudEntity);

    // --- Inventory overlay (Phase B) ---
    var inventoryOverlayEntity = new Entity("InventoryOverlay");
    var inventoryOverlay = new InventoryOverlayScript
    {
        Inventory = playerInventory,
        StateManager = gameStateManager,
    };
    inventoryOverlayEntity.Add(inventoryOverlay);
    rootScene.Entities.Add(inventoryOverlayEntity);

    // --- Notification feed (Phase C) ---
    var notificationService = new NotificationService();
    eventBus.Subscribe<NotificationEvent>(e => notificationService.Add(e.Message, e.DurationSeconds));

    var notificationEntity = new Entity("NotificationFeed");
    var notificationFeed = new NotificationFeedScript
    {
        Notifications = notificationService,
    };
    notificationEntity.Add(notificationFeed);
    rootScene.Entities.Add(notificationEntity);

    // --- Enemy HP bars (Phase C) ---
    var enemyHpEntity = new Entity("EnemyHpBars");
    var enemyHpBars = new EnemyHpBarScript
    {
        StateManager = gameStateManager,
        CameraEntity = cameraEntity,
    };
    enemyHpBars.Enemies = enemies;
    enemyHpEntity.Add(enemyHpBars);
    rootScene.Entities.Add(enemyHpEntity);

    // --- Game over / victory overlay (Phase C) ---
    var gameOverEntity = new Entity("GameOverOverlay");
    var gameOverOverlay = new GameOverOverlayScript
    {
        StateManager = gameStateManager,
    };
    gameOverEntity.Add(gameOverOverlay);
    rootScene.Entities.Add(gameOverEntity);

    // --- Floating damage (Phase C) ---
    var floatingDamageEntity = new Entity("FloatingDamage");
    var floatingDamage = new FloatingDamageScript
    {
        CameraEntity = cameraEntity,
        EventBus = eventBus,
        CombatScript = combatScript,
    };
    floatingDamageEntity.Add(floatingDamage);
    rootScene.Entities.Add(floatingDamageEntity);

    // --- Wire player movement freeze on GameOver (Phase C) ---
    playerMovement.StateManager = gameStateManager;

    // --- Automation server (for Brinell.Stride UI tests) ---
    if (StrideAutomationExtensions.IsAutomationEnabled())
    {
        logger.LogInformation("Automation mode enabled");
        var strideHandler = new StrideUIHandler(
            rootProvider: () => null,  // No Stride UI yet
            isReadyProvider: () => true,
            isBusyProvider: () => false);
        var oraveyHandler = new OraveyAutomationHandler(strideHandler, rootScene, game);
        oraveyHandler.SetPhaseB(
            playerInventory, playerEquipment, playerHealth,
            playerCombat, playerLevel, gameStateManager);
        oraveyHandler.SetPhaseC(notificationService, gameOverOverlay);
        game.UseAutomation(oraveyHandler,
            options: new AutomationServerOptions { VerboseLogging = true });
    }

    // --- Transition to Exploring state ---
    gameStateManager.TransitionTo(GameState.Exploring);
    logger.LogInformation("Game ready");
}
