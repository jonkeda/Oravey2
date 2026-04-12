using Microsoft.Extensions.Logging;
using Oravey2.Core.Camera;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Combat;
using Oravey2.Core.Data;
using Oravey2.Core.Dialogue;
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
using Oravey2.Core.World.Terrain;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

namespace Oravey2.Core.Bootstrap;

/// <summary>
/// Data-driven region loader. Loads any region from the world database(s)
/// using MapDataProvider + ChunkStreamingProcessor, then spawns entities
/// via EntitySpawnerDispatcher. Replaces hardcoded ScenarioLoader paths.
/// </summary>
public sealed class RegionLoader
{
    private readonly List<WorldMapStore> _stores;
    private readonly SaveStateStore? _saveStore;
    private readonly EntitySpawnerDispatcher _spawnerDispatcher;

    public Scene? WorldScene { get; private set; }
    public SpriteFont? Font { get; set; }

    // Exposed game refs (mirrors ScenarioLoader pattern)
    public Entity? PlayerEntity { get; private set; }
    public InventoryComponent? PlayerInventory { get; private set; }
    public EquipmentComponent? PlayerEquipment { get; private set; }
    public HealthComponent? PlayerHealth { get; private set; }
    public CombatComponent? PlayerCombat { get; private set; }
    public LevelComponent? PlayerLevel { get; private set; }
    public StatsComponent? PlayerStats { get; private set; }
    public NotificationService? NotificationService { get; private set; }
    public GameOverOverlayScript? GameOverOverlay { get; private set; }

    public bool IsLoaded => WorldScene != null;
    public string? CurrentRegionName { get; private set; }

    public RegionLoader(
        IReadOnlyList<WorldMapStore> stores,
        SaveStateStore? saveStore,
        EntitySpawnerDispatcher spawnerDispatcher)
    {
        _stores = new List<WorldMapStore>(stores);
        _saveStore = saveStore;
        _spawnerDispatcher = spawnerDispatcher;
    }

    /// <summary>
    /// Adds a world store at runtime (e.g. after importing a content pack mid-session).
    /// </summary>
    public void AddStore(WorldMapStore store) => _stores.Insert(0, store);

    /// <summary>
    /// Searches all stores to find a region by name. Returns the store and region record.
    /// </summary>
    public (WorldMapStore Store, RegionRecord Region) FindRegion(string regionName)
    {
        foreach (var store in _stores)
        {
            var region = store.GetRegionByName(regionName);
            if (region != null)
                return (store, region);
        }

        throw new InvalidOperationException($"Region '{regionName}' not found in any world database.");
    }

    /// <summary>
    /// Loads a region by name: creates player, terrain, HUD, entities, and gameplay systems.
    /// </summary>
    public void LoadRegion(string regionName, Scene rootScene, Game game,
        Entity cameraEntity, GameStateManager gameStateManager,
        IEventBus eventBus, IInputProvider inputProvider, ILogger logger,
        Vector3? spawnOverride = null)
    {
        if (IsLoaded)
            UnloadCurrentRegion(rootScene);

        var (store, region) = FindRegion(regionName);
        CurrentRegionName = regionName;

        // Create child scene
        WorldScene = new Scene();
        rootScene.Children.Add(WorldScene);

        // ---- Player ----
        var (playerEntity, playerMovement, stats, level, health, combat,
             inventory, equipment, processor)
            = CreatePlayer(WorldScene, game, cameraEntity, gameStateManager, eventBus);

        if (spawnOverride.HasValue)
            playerEntity.Transform.Position = spawnOverride.Value;

        // ---- Terrain: load ALL chunks for the region ----
        var provider = new MapDataProvider(store, _saveStore);
        var chunkRecords = store.GetChunksForRegion(region.Id);

        // Determine grid extents
        int maxCx = 0, maxCy = 0;
        foreach (var cr in chunkRecords)
        {
            if (cr.GridX > maxCx) maxCx = cr.GridX;
            if (cr.GridY > maxCy) maxCy = cr.GridY;
        }
        int chunksW = maxCx + 1;
        int chunksH = maxCy + 1;

        // Build WorldMapData and a combined TileMapData
        var worldMap = new WorldMapData(chunksW, chunksH);
        int totalW = chunksW * ChunkData.Size;
        int totalH = chunksH * ChunkData.Size;
        var mapData = new TileMapData(totalW, totalH);

        foreach (var cr in chunkRecords)
        {
            var chunkData = provider.GetChunkData(region.Id, cr.GridX, cr.GridY);
            if (chunkData == null) continue;
            worldMap.SetChunk(cr.GridX, cr.GridY, chunkData);

            // Copy tiles into the combined grid
            for (int lx = 0; lx < ChunkData.Size; lx++)
            for (int ly = 0; ly < ChunkData.Size; ly++)
            {
                int gx = cr.GridX * ChunkData.Size + lx;
                int gy = cr.GridY * ChunkData.Size + ly;
                mapData.SetTileData(gx, gy, chunkData.Tiles.GetTileData(lx, ly));
            }
        }

        var tileMapEntity = new Entity("TileMap");
        var tileMapRenderer = new HeightmapTerrainScript { MapData = mapData, WorldMap = worldMap };
        tileMapEntity.Add(tileMapRenderer);
        AddEntity(tileMapEntity, WorldScene);

        playerMovement.MapData = mapData;
        playerMovement.TileSize = tileMapRenderer.TileSize;
        WireTerrainHeight(playerMovement, playerEntity, mapData, tileMapRenderer.TileSize);

        // ---- Entity spawns from DB ----
        float tileSize = tileMapRenderer.TileSize;
        float halfWorldX = totalW * tileSize / 2f;
        float halfWorldZ = totalH * tileSize / 2f;
        var worldOffset = new Vector3(-halfWorldX, 0f, -halfWorldZ);

        var dbSpawns = store.GetEntitySpawnsForRegion(region.Id);
        var spawnTuples = dbSpawns.Select(s => (s.ChunkX, s.ChunkY, s.Spawn));
        _spawnerDispatcher.SpawnAll(WorldScene, spawnTuples, tileSize, worldOffset);

        // ---- Notification feed ----
        var notificationService = new NotificationService();
        eventBus.Subscribe<NotificationEvent>(e => notificationService.Add(e.Message, e.DurationSeconds));
        NotificationService = notificationService;

        var notificationEntity = new Entity("NotificationFeed");
        notificationEntity.Add(new NotificationFeedScript { Notifications = notificationService, Font = Font });
        AddEntity(notificationEntity, WorldScene);

        // ---- HUD ----
        var hudEntity = new Entity("HUD");
        hudEntity.Add(new HudSyncScript
        {
            Health = health, Combat = combat,
            Level = level, Inventory = inventory,
            StateManager = gameStateManager,
            Font = Font,
        });
        AddEntity(hudEntity, WorldScene);

        // ---- Inventory overlay ----
        var inventoryOverlayEntity = new Entity("InventoryOverlay");
        inventoryOverlayEntity.Add(new InventoryOverlayScript
        {
            Inventory = inventory, StateManager = gameStateManager,
            InputProvider = inputProvider,
            Font = Font,
        });
        AddEntity(inventoryOverlayEntity, WorldScene);

        // ---- Game over overlay ----
        var gameOverEntity = new Entity("GameOverOverlay");
        var gameOverOverlay = new GameOverOverlayScript { StateManager = gameStateManager, Font = Font };
        gameOverEntity.Add(gameOverOverlay);
        AddEntity(gameOverEntity, WorldScene);
        GameOverOverlay = gameOverOverlay;

        logger.LogInformation("Region '{Region}' loaded with {Count} entities",
            regionName, WorldScene.Entities.Count);
    }

    public void UnloadCurrentRegion(Scene rootScene)
    {
        if (WorldScene != null)
        {
            rootScene.Children.Remove(WorldScene);
            WorldScene = null;
        }

        PlayerEntity = null;
        PlayerInventory = null;
        PlayerEquipment = null;
        PlayerHealth = null;
        PlayerCombat = null;
        PlayerLevel = null;
        PlayerStats = null;
        NotificationService = null;
        GameOverOverlay = null;
        CurrentRegionName = null;
    }

    // ---- Helpers (copied from ScenarioLoader patterns) ----

    private Entity AddEntity(Entity entity, Scene scene)
    {
        scene.Entities.Add(entity);
        return entity;
    }

    private static void WireTerrainHeight(PlayerMovementScript movement, Entity playerEntity,
        TileMapData mapData, float tileSize)
    {
        var heightQuery = new TerrainHeightQuery(mapData, tileSize);
        movement.HeightQuery = heightQuery;

        var pos = playerEntity.Transform.Position;
        pos.Y = heightQuery.GetEffectiveHeight(pos.X, pos.Z) + TerrainHeightQuery.PlayerHeightOffset;
        playerEntity.Transform.Position = pos;
    }

    private (Entity player, PlayerMovementScript movement, StatsComponent stats,
        LevelComponent level, HealthComponent health, CombatComponent combat,
        InventoryComponent inventory, EquipmentComponent equipment, InventoryProcessor processor)
        CreatePlayer(Scene scene, Game game, Entity cameraEntity,
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

        var cameraScript = cameraEntity.Get<TacticalCameraScript>();
        if (cameraScript != null)
            cameraScript.Target = playerEntity;
        playerMovement.CameraScript = cameraScript;

        AddEntity(playerEntity, scene);

        var stats = new StatsComponent();
        var level = new LevelComponent(stats);
        var health = new HealthComponent(stats, level, eventBus);
        var combat = new CombatComponent { InCombat = false };
        var inventory = new InventoryComponent(stats);
        var equipment = new EquipmentComponent();
        var processor = new InventoryProcessor(inventory, equipment, eventBus);

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
}
