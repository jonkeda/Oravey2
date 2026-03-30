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
using Oravey2.Core.Player;
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

    // --- Automation server (for Brinell.Stride UI tests) ---
    if (StrideAutomationExtensions.IsAutomationEnabled())
    {
        logger.LogInformation("Automation mode enabled");
        var strideHandler = new StrideUIHandler(
            rootProvider: () => null,  // No Stride UI yet
            isReadyProvider: () => true,
            isBusyProvider: () => false);
        var oraveyHandler = new OraveyAutomationHandler(strideHandler, rootScene, game);
        game.UseAutomation(oraveyHandler,
            options: new AutomationServerOptions { VerboseLogging = true });
    }

    // --- Transition to Exploring state ---
    gameStateManager.TransitionTo(GameState.Exploring);
    logger.LogInformation("Game ready");
}
