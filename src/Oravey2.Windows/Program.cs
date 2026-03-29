using Brinell.Automation;
using Microsoft.Extensions.Logging;
using Oravey2.Core.Camera;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.Logging;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Oravey2.Core.Player;
using Oravey2.Core.World;
using Stride.CommunityToolkit.Engine;
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

    // --- Setup base scene (lighting, skybox) ---
    game.SetupBase3D();
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
        CameraYaw = 45f
    };
    playerEntity.Add(playerMovement);
    rootScene.Entities.Add(playerEntity);

    // --- Tile Map ---
    var mapData = TileMapData.CreateDefault(16, 16);
    var tileMapEntity = new Entity("TileMap");
    var tileMapRenderer = new TileMapRendererScript { MapData = mapData };
    tileMapEntity.Add(tileMapRenderer);
    rootScene.Entities.Add(tileMapEntity);

    // --- Isometric Camera ---
    var cameraEntity = new Entity("IsometricCamera");
    cameraEntity.Add(new CameraComponent());
    var cameraScript = new IsometricCameraScript
    {
        Target = playerEntity,
        Pitch = 30f,
        Yaw = 45f,
        Distance = 20f,
        CurrentZoom = 20f,
        ZoomMin = 10f,
        ZoomMax = 40f
    };
    cameraEntity.Add(cameraScript);
    rootScene.Entities.Add(cameraEntity);

    // --- Automation server (for Brinell.Stride UI tests) ---
    if (StrideAutomationExtensions.IsAutomationEnabled())
    {
        logger.LogInformation("Automation mode enabled");
        game.UseAutomation(
            uiRootProvider: () => null,  // No Stride UI yet
            options: new AutomationServerOptions { VerboseLogging = true });
    }

    // --- Transition to Exploring state ---
    gameStateManager.TransitionTo(GameState.Exploring);
    logger.LogInformation("Game ready");
}
