using Microsoft.Extensions.Logging;
using Oravey2.Core.Camera;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
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
using Oravey2.Core.UI;
using Oravey2.Core.UI.Stride;
using Oravey2.Core.World;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

namespace Oravey2.Core.Bootstrap;

/// <summary>
/// Loads/unloads game world scenarios. Keeps the menu-only scene clean.
/// </summary>
public sealed class ScenarioLoader
{
    private readonly List<Entity> _loadedEntities = [];

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

    public bool IsLoaded => _loadedEntities.Count > 0;
    public string? CurrentScenarioId { get; private set; }

    public void Load(string scenarioId, Scene rootScene, Game game,
        Entity cameraEntity, GameStateManager gameStateManager,
        IEventBus eventBus, IInputProvider inputProvider, ILogger logger)
    {
        if (IsLoaded)
            Unload(rootScene);

        CurrentScenarioId = scenarioId;

        switch (scenarioId)
        {
            case "m0_combat":
                LoadM0Combat(rootScene, game, cameraEntity, gameStateManager, eventBus, inputProvider, logger);
                break;
            case "empty":
                LoadEmpty(rootScene, game, cameraEntity, gameStateManager, eventBus, inputProvider, logger);
                break;
            case "town":
                LoadTown(rootScene, game, cameraEntity, gameStateManager, eventBus, inputProvider, logger);
                break;
            default:
                logger.LogWarning("Unknown scenario: {Id}, falling back to m0_combat", scenarioId);
                LoadM0Combat(rootScene, game, cameraEntity, gameStateManager, eventBus, inputProvider, logger);
                break;
        }

        logger.LogInformation("Scenario '{Id}' loaded with {Count} entities", scenarioId, _loadedEntities.Count);
    }

    public void Unload(Scene rootScene)
    {
        foreach (var entity in _loadedEntities)
            rootScene.Entities.Remove(entity);
        _loadedEntities.Clear();

        PlayerInventory = null;
        PlayerEquipment = null;
        PlayerHealth = null;
        PlayerCombat = null;
        PlayerLevel = null;
        PlayerStats = null;
        NotificationService = null;
        GameOverOverlay = null;
        PlayerEntity = null;
        CurrentScenarioId = null;
    }

    private Entity AddEntity(Entity entity, Scene rootScene)
    {
        rootScene.Entities.Add(entity);
        _loadedEntities.Add(entity);
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
        var tileMapRenderer = new TileMapRendererScript { MapData = mapData };
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
            StateManager = gameStateManager,
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

        // HUD
        var hudEntity = new Entity("HUD");
        hudEntity.Add(new HudSyncScript
        {
            Health = playerHealth, Combat = playerCombat,
            Level = playerLevel, StateManager = gameStateManager,
            Font = Font,
        });
        AddEntity(hudEntity, rootScene);

        // Inventory overlay
        var inventoryOverlayEntity = new Entity("InventoryOverlay");
        inventoryOverlayEntity.Add(new InventoryOverlayScript
        {
            Inventory = playerInventory, StateManager = gameStateManager,
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
        var tileMapRenderer = new TileMapRendererScript { MapData = mapData };
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
            Level = playerLevel, StateManager = gameStateManager,
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
            StateManager = gameStateManager,
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
        var tileMapRenderer = new TileMapRendererScript { MapData = mapData };
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
            Level = playerLevel, StateManager = gameStateManager,
            Font = Font,
        });
        AddEntity(hudEntity, rootScene);

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

        // NPCs
        SpawnNpcs(rootScene, game);
    }

    private void SpawnNpcs(Scene rootScene, Game game)
    {
        var npcs = new (NpcDefinition def, Vector3 pos, Color color)[]
        {
            (new NpcDefinition("elder", "Elder Tomas", NpcRole.QuestGiver, "elder_dialogue"),
                new Vector3(-4f, 0.5f, -4.5f), new Color(0.9f, 0.8f, 0.2f)),
            (new NpcDefinition("merchant", "Mara", NpcRole.Merchant, "merchant_dialogue"),
                new Vector3(1f, 0.5f, -3.5f), new Color(0.2f, 0.3f, 0.9f)),
            (new NpcDefinition("civilian_1", "Settler", NpcRole.Civilian, "civilian_dialogue"),
                new Vector3(-4f, 0.5f, 3.5f), new Color(0.6f, 0.6f, 0.6f)),
            (new NpcDefinition("civilian_2", "Settler", NpcRole.Civilian, "civilian_dialogue"),
                new Vector3(13f, 0.5f, 3.5f), new Color(0.6f, 0.6f, 0.6f)),
        };

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

            AddEntity(npcEntity, rootScene);
        }
    }
}
