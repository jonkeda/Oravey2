using Brinell.Automation;
using Microsoft.Extensions.Logging;
using Oravey2.Core.Audio;
using Oravey2.Core.Automation;
using Oravey2.Core.Bootstrap.Spawners;
using Oravey2.Core.Camera;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Content;
using Oravey2.Core.Data;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.Logging;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Oravey2.Core.Save;
using Oravey2.Core.UI.Stride;
using Oravey2.Core.World;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Compositing;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Graphics;

namespace Oravey2.Core.Bootstrap;

/// <summary>
/// Orchestrates full game startup. Called by each platform's Program.cs.
/// Contains ALL game logic wiring — platforms only provide config.
/// </summary>
public sealed class GameBootstrapper
{
    public void Start(Scene rootScene, Game game, BootstrapConfig config)
    {
        // --- Bootstrap services ---
        var services = ServiceLocator.Instance;

        var gameLoggerFactory = new GameLoggerFactory(config.LoggerFactory);
        services.Register(gameLoggerFactory);

        var logger = gameLoggerFactory.CreateLogger("Oravey2");
        logger.LogInformation("Game starting...");

        var eventBus = new EventBus();
        var inputProvider = config.InputProvider;
        var gameStateManager = new GameStateManager(eventBus, gameLoggerFactory.CreateLogger<GameStateManager>());

        services.Register<IEventBus>(eventBus);
        services.Register<IInputProvider>(inputProvider);
        services.Register(gameStateManager);

        var volumeSettings = new VolumeSettings(eventBus);
        var saveService = new SaveService();
        var autoSaveTracker = new AutoSaveTracker(eventBus);

        services.Register(saveService);
        services.Register(autoSaveTracker);

        logger.LogInformation("Services registered");

        // --- Scene infrastructure (always present) ---
        game.AddGraphicsCompositor().AddCleanUIStage();
        game.AddDirectionalLight();
        game.AddSkybox();

        var font = game.Content.Load<SpriteFont>("StrideDefaultFont");

        var inputEntity = new Entity("InputManager");
        inputEntity.Add(new InputUpdateScript());
        rootScene.Entities.Add(inputEntity);

        // Camera (no target until scenario loads)
        var cameraEntity = game.Add3DCamera(
            cameraName: "IsometricCamera",
            initialPosition: new Vector3(20, 10, 20),
            initialRotation: new Vector3(45, -30, 0),
            projectionMode: CameraProjectionMode.Orthographic);
        var cameraScript = new TacticalCameraScript
        {
            Target = null,
            Pitch = 30f, Yaw = 45f, Distance = 40f,
            CurrentFov = 28f, FovMin = 10f, FovMax = 50f,
            RotationSpeed = 120f, ZoomSpeed = 15f, FollowSmoothing = 8f
        };
        cameraEntity.Add(cameraScript);

        // --- Scenario state holder ---
        var scenarioLoader = new ScenarioLoader { Font = font };
        scenarioLoader.InitializeQuestSystem(eventBus);

        // --- World DB stores + RegionLoader (sole loading path) ---
        WorldMapStore? worldStore = null;
        var worldDbPath = WorldDbPaths.GetUserWorldDbPath();
        var debugDbPath = Path.Combine(AppContext.BaseDirectory, "debug.db");
        logger.LogInformation("World DB path: {Path}", worldDbPath);
        if (File.Exists(worldDbPath))
            worldStore = new WorldMapStore(worldDbPath);

        // Auto-seed debug.db with built-in scenarios if it doesn't exist
        if (!File.Exists(debugDbPath))
        {
            using var seedStore = new WorldMapStore(debugDbPath);
            new WorldDbSeeder(seedStore).SeedAll();
            logger.LogInformation("Seeded debug.db with built-in scenarios");
        }
        var debugStore = new WorldMapStore(debugDbPath);

        var worldStores = new List<WorldMapStore>();
        if (worldStore != null) worldStores.Add(worldStore);
        worldStores.Add(debugStore);

        var spawnerFactories = new IEntitySpawnerFactory[]
        {
            new NpcSpawnerFactory(game),
            new EnemySpawnerFactory(game),
            new ZoneExitSpawnerFactory(game),
            new BuildingSpawnerFactory(game),
            new PropSpawnerFactory(game),
        };
        var spawnerDispatcher = new EntitySpawnerDispatcher(spawnerFactories);
        var saveDbPath = Path.Combine(AppContext.BaseDirectory, "save.db");
        var saveStateStore = new SaveStateStore(saveDbPath);
        var regionLoader = new RegionLoader(worldStores, saveStateStore, spawnerDispatcher)
        {
            Font = font,
            QuestLog = scenarioLoader.QuestLog,
            WorldState = scenarioLoader.WorldState,
        };

        var zoneManager = new ZoneManager(scenarioLoader);
        zoneManager.RegionLoader = regionLoader;
        zoneManager.SaveStateStore = saveStateStore;

        // --- Content pack discovery ---
        var contentPackService = new ContentPackService();
        contentPackService.DiscoverPacks();
        var contentPackImportService = new ContentPackImportService(contentPackService);
        if (contentPackService.Packs.Count > 0)
        {
            contentPackService.SetActivePack(contentPackService.Packs[0].Manifest.Id);
            logger.LogInformation("Content packs found: {Count}, active: {Pack}",
                contentPackService.Packs.Count, contentPackService.ActivePack?.Manifest.Name);
        }

        // Helper: build SaveData from current scenario state
        SaveData? BuildSaveData()
        {
            if (scenarioLoader.PlayerHealth == null || scenarioLoader.PlayerEntity == null) return null;
            return new SaveDataBuilder()
                .WithHeader("Survivor", scenarioLoader.PlayerLevel?.Level ?? 1, TimeSpan.Zero, "0.1.0")
                .WithStats(scenarioLoader.PlayerStats ?? new StatsComponent())
                .WithHealth(scenarioLoader.PlayerHealth)
                .WithLevel(scenarioLoader.PlayerLevel ?? new LevelComponent(new StatsComponent()))
                .WithInventory(scenarioLoader.PlayerInventory!, scenarioLoader.PlayerEquipment!)
                .WithWorldState(new DayNightCycleProcessor(eventBus), 0, 0,
                    scenarioLoader.PlayerEntity.Transform.Position.X,
                    scenarioLoader.PlayerEntity.Transform.Position.Y,
                    scenarioLoader.PlayerEntity.Transform.Position.Z)
                .Build();
        }

        // Helper: perform a save
        void PerformSave()
        {
            var data = BuildSaveData();
            if (data != null) saveService.SaveGame(data);
        }

        // Forward-declare so LoadAndWireScenario can capture it
        PauseMenuScript? pauseMenuScript = null;

        // Helper: load a scenario and wire automation refs
        void LoadAndWireScenario(string scenarioId)
        {
            regionLoader.LoadRegion(scenarioId, rootScene, game,
                cameraEntity, gameStateManager, eventBus, inputProvider, logger);
            scenarioLoader.SyncFromRegion(regionLoader);

            // Wire action bar pause button to the menu-scene PauseMenuScript
            if (regionLoader.ActionBar != null)
                regionLoader.ActionBar.PauseOverlay = pauseMenuScript;

            // Track the current zone
            var zoneId = scenarioId switch { "m0_combat" => "wasteland", _ => scenarioId };
            zoneManager.SetCurrentZone(zoneId);

            // Wire zone exit trigger if present (town has a gate exit)
            if (scenarioLoader.ZoneExitTrigger != null)
            {
                scenarioLoader.ZoneExitTrigger.OnZoneExit = (targetZoneId, spawnPos) =>
                {
                    PerformSave();
                    zoneManager.TransitionTo(targetZoneId, rootScene, game,
                        cameraEntity, gameStateManager, eventBus, inputProvider, logger, spawnPos);
                    WireDeathRespawn();
                    // Re-wire death penalty for the new scenario (fallback when no DeathRespawnScript)
                    eventBus.Subscribe<GameStateChangedEvent>(e =>
                    {
                        if (e.NewState == GameState.GameOver && scenarioLoader.DeathRespawn == null
                            && scenarioLoader.PlayerInventory != null)
                        {
                            var lost = scenarioLoader.PlayerInventory.ApplyDeathPenalty();
                            if (lost > 0)
                                scenarioLoader.NotificationService?.Add($"Lost {lost} Caps", 3f);
                        }
                    });
                };
            }

            // Wire death respawn script (timed death → respawn flow)
            WireDeathRespawn();

            // Death penalty fallback: lose 10% Caps on GameOver (only when no DeathRespawnScript)
            eventBus.Subscribe<GameStateChangedEvent>(e =>
            {
                if (e.NewState == GameState.GameOver && scenarioLoader.DeathRespawn == null
                    && scenarioLoader.PlayerInventory != null)
                {
                    var lost = scenarioLoader.PlayerInventory.ApplyDeathPenalty();
                    if (lost > 0)
                        scenarioLoader.NotificationService?.Add($"Lost {lost} Caps", 3f);
                }
            });

            gameStateManager.TransitionTo(GameState.Exploring);
        }

        // Helper: wire DeathRespawnScript OnRespawn callback
        void WireDeathRespawn()
        {
            if (scenarioLoader.DeathRespawn == null) return;
            scenarioLoader.DeathRespawn.AutoSaveTracker = autoSaveTracker;
            scenarioLoader.DeathRespawn.OnRespawn = (capsLost) =>
            {
                zoneManager.TransitionTo("town", rootScene, game,
                    cameraEntity, gameStateManager, eventBus, inputProvider, logger,
                    new Vector3(0, 0.5f, 0));
                WireDeathRespawn();
                // Heal the new player to max
                if (scenarioLoader.PlayerHealth != null)
                    scenarioLoader.PlayerHealth.HealToMax();
                // Apply caps penalty to the new inventory
                if (capsLost > 0 && scenarioLoader.PlayerInventory != null)
                    scenarioLoader.PlayerInventory.Caps -= capsLost;
                gameStateManager.ForceState(GameState.Exploring);
                var msg = capsLost > 0
                    ? $"You wake up in Haven. Lost {capsLost} caps."
                    : "You wake up in Haven.";
                scenarioLoader.NotificationService?.Add(msg, 5f);
            };
        }

        // Helper: apply a save-file on top of the current scenario
        void ApplyLoadedSave()
        {
            var restorer = saveService.LoadGame();
            if (restorer != null && scenarioLoader.PlayerEntity != null)
            {
                var (px, py, pz) = restorer.PlayerPosition;
                scenarioLoader.PlayerEntity.Transform.Position = new Vector3(px, py, pz);
                if (scenarioLoader.PlayerStats != null) restorer.RestoreStats(scenarioLoader.PlayerStats);
                if (scenarioLoader.PlayerHealth != null) restorer.RestoreHealth(scenarioLoader.PlayerHealth);
                if (scenarioLoader.PlayerLevel != null) restorer.RestoreLevel(scenarioLoader.PlayerLevel);
                if (scenarioLoader.PlayerInventory != null) scenarioLoader.PlayerInventory.Caps = restorer.Caps;
            }
        }

        // --- Menu entities (child scene — persists across zone transitions) ---
        var menuScene = new Scene();
        rootScene.Children.Add(menuScene);

        var startMenuEntity = new Entity("StartMenu");
        var startMenuScript = new StartMenuScript
        {
            StateManager = gameStateManager,
            SaveService = saveService,
            ContentPacks = contentPackService,
            Font = font,
        };

        var scenarioSelectorEntity = new Entity("ScenarioSelector");
        var scenarioSelectorScript = new ScenarioSelectorScript
        {
            Font = font,
            ContentPacks = contentPackService,
            ImportService = contentPackImportService,
            WorldStore = worldStore,
            DebugStore = debugStore,
            OnStoreAdded = store => regionLoader.AddStore(store),
        };
        scenarioSelectorScript.OnBack = () =>
        {
            scenarioSelectorScript.Hide();
            startMenuScript.Show();
        };
        scenarioSelectorScript.OnScenarioSelected = (scenarioId) =>
        {
            scenarioSelectorScript.Hide();
            LoadAndWireScenario(scenarioId);
            logger.LogInformation("New game started: scenario '{Id}'", scenarioId);
        };
        scenarioSelectorEntity.Add(scenarioSelectorScript);
        menuScene.Entities.Add(scenarioSelectorEntity);

        startMenuScript.OnNewScenario = () =>
        {
            startMenuScript.Hide();
            scenarioSelectorScript.Show();
        };
        startMenuScript.OnContinue = () =>
        {
            startMenuScript.Hide();

            // Check if save state tracks a current region for the data-driven path
            var savedRegion = saveStateStore.GetCurrentRegion();
            if (savedRegion != null)
            {
                // Restore saved position for the region
                var savedPos = saveStateStore.GetPlayerPosition(savedRegion);
                Vector3? spawnOverride = savedPos.HasValue
                    ? new Vector3(savedPos.Value.X, savedPos.Value.Y, savedPos.Value.Z)
                    : null;
                regionLoader.LoadRegion(savedRegion, rootScene, game,
                    cameraEntity, gameStateManager, eventBus, inputProvider, logger, spawnOverride);
                scenarioLoader.SyncFromRegion(regionLoader);
                gameStateManager.TransitionTo(GameState.Exploring);
            }
            else
            {
                LoadAndWireScenario("town");
                ApplyLoadedSave();
            }

            logger.LogInformation("Game loaded from save");
        };
        startMenuEntity.Add(startMenuScript);
        menuScene.Entities.Add(startMenuEntity);

        var pauseMenuEntity = new Entity("PauseMenu");
        pauseMenuScript = new PauseMenuScript
        {
            StateManager = gameStateManager,
            InputProvider = inputProvider,
            Font = font,
        };
        pauseMenuScript.OnSaveGame = () =>
        {
            PerformSave();
            scenarioLoader.NotificationService?.Add("Game Saved!", 2f);
            logger.LogInformation("Game saved");
        };
        pauseMenuScript.OnQuitToMenu = () =>
        {
            pauseMenuScript.Resume();
            scenarioLoader.Unload(rootScene);
            cameraScript.Target = null;
            autoSaveTracker.Acknowledge(); // Reset auto-save timer
            startMenuScript.Show();
            gameStateManager.ForceState(GameState.InMenu);
            logger.LogInformation("Returned to main menu");
        };
        pauseMenuEntity.Add(pauseMenuScript);
        menuScene.Entities.Add(pauseMenuEntity);

        var settingsMenuEntity = new Entity("SettingsMenu");
        var settingsMenuScript = new SettingsMenuScript
        {
            Volume = volumeSettings,
            AutoSave = autoSaveTracker,
            Font = font,
        };
        settingsMenuScript.OnBack = () => settingsMenuScript.Hide();
        startMenuScript.OnSettings = () => settingsMenuScript.Show();
        pauseMenuScript.OnSettings = () => settingsMenuScript.Show();
        settingsMenuEntity.Add(settingsMenuScript);
        menuScene.Entities.Add(settingsMenuEntity);

        // --- SaveLoad script (QuickSave F5, QuickLoad F9, auto-save tick) ---
        var saveLoadEntity = new Entity("SaveLoadManager");
        var saveLoadScript = new SaveLoadScript
        {
            InputProvider = inputProvider,
            StateManager = gameStateManager,
            AutoSaveTracker = autoSaveTracker,
            GetNotifications = () => scenarioLoader.NotificationService,
        };
        saveLoadScript.OnSave = PerformSave;
        saveLoadScript.OnLoad = () =>
        {
            // QuickLoad: reload current scenario + overlay saved state
            var currentScenario = scenarioLoader.CurrentScenarioId ?? "town";
            scenarioLoader.Unload(rootScene);
            LoadAndWireScenario(currentScenario);
            ApplyLoadedSave();
        };
        saveLoadScript.HasSave = () => saveService.HasSaveFile();
        saveLoadEntity.Add(saveLoadScript);
        menuScene.Entities.Add(saveLoadEntity);

        // --- Automation server ---
        if (config.AutomationEnabled)
        {
            logger.LogInformation("Automation mode enabled");
            var strideHandler = new StrideUIHandler(
                rootProvider: () => null,
                isReadyProvider: () => true,
                isBusyProvider: () => false);
            var oraveyHandler = new OraveyAutomationHandler(strideHandler, rootScene, game);
            oraveyHandler.SetScenarioLoader(scenarioLoader);
            oraveyHandler.SetZoneManager(zoneManager);
            oraveyHandler.SetM1(saveService, autoSaveTracker, startMenuScript, pauseMenuScript, settingsMenuScript);

            // Parse --pipe arg for unique pipe names (prevents orphan conflicts in test runs)
            var automationOptions = new AutomationServerOptions { VerboseLogging = true };
            var args = config.Args;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--pipe")
                {
                    automationOptions.PipeName = args[i + 1];
                    logger.LogInformation("Using custom pipe name: {Pipe}", automationOptions.PipeName);
                    break;
                }
            }

            game.UseAutomation(oraveyHandler, options: automationOptions);

            // Parse --scenario arg (default: m0_combat)
            var scenarioId = "m0_combat";
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--scenario")
                {
                    scenarioId = args[i + 1];
                    break;
                }
            }

            // Direct start: load scenario, skip menu
            startMenuScript.Hide();
            LoadAndWireScenario(scenarioId);

            // Wire automation refs after scenario is loaded
            oraveyHandler.SetPhaseB(
                scenarioLoader.PlayerInventory!, scenarioLoader.PlayerEquipment!,
                scenarioLoader.PlayerHealth!, scenarioLoader.PlayerCombat!,
                scenarioLoader.PlayerLevel!, gameStateManager);
            if (scenarioLoader.NotificationService != null && scenarioLoader.GameOverOverlay != null)
                oraveyHandler.SetPhaseC(scenarioLoader.NotificationService, scenarioLoader.GameOverOverlay);

            logger.LogInformation("Automation: loaded scenario '{Id}'", scenarioId);
        }
        else
        {
            // Normal mode: show start menu only, no world loaded
            gameStateManager.TransitionTo(GameState.InMenu);
        }

        logger.LogInformation("Game ready");
    }
}
