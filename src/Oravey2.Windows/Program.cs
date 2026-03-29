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

    // --- Tile Map ---
    var mapData = TileMapData.CreateDefault(16, 16);
    var tileMapEntity = new Entity("TileMap");
    var tileMapRenderer = new TileMapRendererScript { MapData = mapData };
    tileMapEntity.Add(tileMapRenderer);
    rootScene.Entities.Add(tileMapEntity);

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
