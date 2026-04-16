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
    public QuestLogComponent? QuestLog { get; set; }
    public WorldStateService? WorldState { get; set; }

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
    public QuestJournalScript? QuestJournal { get; private set; }
    public ActionBarScript? ActionBar { get; private set; }

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

        if (chunkRecords.Count == 0)
        {
            logger.LogWarning("Region '{Region}' has no chunks.", regionName);
            return;
        }

        // Cluster-pack: find contiguous groups of chunks and lay them out
        // adjacently to avoid sparse grids that waste memory.
        var chunkCoordMap = CompactChunkLayout(chunkRecords);
        int chunksW = chunkCoordMap.Values.Max(v => v.NewX) + 1;
        int chunksH = chunkCoordMap.Values.Max(v => v.NewY) + 1;

        // Build WorldMapData and a combined TileMapData (compacted)
        var worldMap = new WorldMapData(chunksW, chunksH);
        int totalW = chunksW * ChunkData.Size;
        int totalH = chunksH * ChunkData.Size;
        var mapData = new TileMapData(totalW, totalH);

        foreach (var cr in chunkRecords)
        {
            var chunkData = provider.GetChunkData(region.Id, cr.GridX, cr.GridY);
            if (chunkData == null) continue;

            var (newX, newY) = chunkCoordMap[(cr.GridX, cr.GridY)];
            worldMap.SetChunk(newX, newY, chunkData);

            // Copy tiles into the combined grid
            for (int lx = 0; lx < ChunkData.Size; lx++)
            for (int ly = 0; ly < ChunkData.Size; ly++)
            {
                int gx = newX * ChunkData.Size + lx;
                int gy = newY * ChunkData.Size + ly;
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

        // ---- Entity spawns from DB (remapped to compacted grid) ----
        float tileSize = tileMapRenderer.TileSize;
        float halfWorldX = totalW * tileSize / 2f;
        float halfWorldZ = totalH * tileSize / 2f;
        var worldOffset = new Vector3(-halfWorldX, 0f, -halfWorldZ);

        var dbSpawns = store.GetEntitySpawnsForRegion(region.Id);
        var spawnTuples = dbSpawns
            .Where(s => chunkCoordMap.ContainsKey((s.ChunkX, s.ChunkY)))
            .Select(s =>
            {
                var (nx, ny) = chunkCoordMap[(s.ChunkX, s.ChunkY)];
                return (nx, ny, s.Spawn);
            });
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
        var hudScript = new HudSyncScript
        {
            Health = health, Combat = combat,
            Level = level, Inventory = inventory,
            StateManager = gameStateManager,
            Font = Font,
        };
        hudEntity.Add(hudScript);
        AddEntity(hudEntity, WorldScene);

        // ---- Inventory overlay ----
        var inventoryOverlayEntity = new Entity("InventoryOverlay");
        var inventoryScript = new InventoryOverlayScript
        {
            Inventory = inventory, StateManager = gameStateManager,
            InputProvider = inputProvider,
            Font = Font,
        };
        inventoryOverlayEntity.Add(inventoryScript);
        AddEntity(inventoryOverlayEntity, WorldScene);

        // ---- Region map overlay ----
        var pois = store.GetPois(region.Id);
        // Normalise POI positions proportionally within their original geo-based
        // bounding box, then scale to compacted grid dimensions.  This preserves
        // geographic layout (north/south/east/west) that CompactChunkLayout's
        // left-to-right packing would otherwise destroy.
        var remappedPois = RemapPoisToCompactGrid(pois, chunksW, chunksH);
        var regionMapEntity = new Entity("RegionMapOverlay");
        var regionMapScript = new RegionMapOverlayScript
        {
            InputProvider = inputProvider,
            StateManager = gameStateManager,
            Font = Font,
            RegionName = regionName,
            WorldTilesWide = totalW,
            WorldTilesHigh = totalH,
            TileSize = tileSize,
            Pois = remappedPois,
            GetPlayerPosition = () => playerEntity.Transform.Position,
            GetHudRootElement = () => hudScript.RootElement,
        };
        regionMapEntity.Add(regionMapScript);
        AddEntity(regionMapEntity, WorldScene);

        // ---- Game over overlay ----
        var gameOverEntity = new Entity("GameOverOverlay");
        var gameOverOverlay = new GameOverOverlayScript { StateManager = gameStateManager, Font = Font };
        gameOverEntity.Add(gameOverOverlay);
        AddEntity(gameOverEntity, WorldScene);
        GameOverOverlay = gameOverOverlay;

        // ---- Quest journal overlay ----
        var journalEntity = new Entity("QuestJournal");
        var journalScript = new QuestJournalScript
        {
            QuestLog = QuestLog,
            WorldState = WorldState,
            StateManager = gameStateManager,
            InputProvider = inputProvider,
            Font = Font,
        };
        journalEntity.Add(journalScript);
        AddEntity(journalEntity, WorldScene);
        QuestJournal = journalScript;

        // ---- Action button bar ----
        var actionBarEntity = new Entity("ActionBar");
        var actionBarScript = new ActionBarScript
        {
            StateManager = gameStateManager,
            Font = Font,
            MapOverlay = regionMapScript,
            InventoryOverlay = inventoryScript,
            JournalOverlay = journalScript,
        };
        actionBarEntity.Add(actionBarScript);
        AddEntity(actionBarEntity, WorldScene);
        ActionBar = actionBarScript;

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
        QuestJournal = null;
        ActionBar = null;
        CurrentRegionName = null;
    }

    // ---- Helpers (copied from ScenarioLoader patterns) ----

    /// <summary>
    /// Maps POI positions from the sparse geo-based chunk grid to the compacted
    /// grid by normalising proportionally.  This preserves the geographic layout
    /// (a town to the north stays above a town to the south) regardless of how
    /// <see cref="CompactChunkLayout"/> packs the terrain clusters.
    /// </summary>
    internal static List<PoiRecord> RemapPoisToCompactGrid(
        List<PoiRecord> pois, int compactChunksW, int compactChunksH)
    {
        if (pois.Count == 0) return [];

        int origMinX = pois.Min(p => p.GridX);
        int origMaxX = pois.Max(p => p.GridX);
        int origMinY = pois.Min(p => p.GridY);
        int origMaxY = pois.Max(p => p.GridY);

        int origW = origMaxX - origMinX;
        int origH = origMaxY - origMinY;

        return pois.Select(p =>
        {
            int nx = origW > 0
                ? (int)((float)(p.GridX - origMinX) / origW * Math.Max(0, compactChunksW - 1))
                : compactChunksW / 2;
            // Invert Y so higher latitude (higher GridY) maps to lower screen Y (top of map)
            int ny = origH > 0
                ? Math.Max(0, compactChunksH - 1) - (int)((float)(p.GridY - origMinY) / origH * Math.Max(0, compactChunksH - 1))
                : compactChunksH / 2;
            return p with { GridX = nx, GridY = ny };
        }).ToList();
    }

    /// <summary>
    /// Clusters chunks by spatial adjacency and packs them into a compact grid.
    /// Returns a mapping from original (gridX, gridY) → compacted (newX, newY).
    /// </summary>
    internal static Dictionary<(int, int), (int NewX, int NewY)> CompactChunkLayout(
        IReadOnlyList<ChunkRecord> records)
    {
        var result = new Dictionary<(int, int), (int NewX, int NewY)>();
        if (records.Count == 0) return result;

        // Build set of occupied positions for fast neighbor lookup
        var occupied = new HashSet<(int, int)>();
        foreach (var cr in records)
            occupied.Add((cr.GridX, cr.GridY));

        // Find connected clusters via flood-fill
        var visited = new HashSet<(int, int)>();
        var clusters = new List<List<(int X, int Y)>>();

        foreach (var pos in occupied)
        {
            if (visited.Contains(pos)) continue;

            var cluster = new List<(int, int)>();
            var queue = new Queue<(int, int)>();
            queue.Enqueue(pos);
            visited.Add(pos);

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                cluster.Add((cx, cy));

                // Check 4-connected neighbors (chunks are adjacent within a town)
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var neighbor = (cx + dx, cy + dy);
                    if (occupied.Contains(neighbor) && visited.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            clusters.Add(cluster);
        }

        // Pack clusters left-to-right with a 1-chunk gap between them
        int cursorX = 0;
        foreach (var cluster in clusters)
        {
            int cMinX = cluster.Min(c => c.X);
            int cMinY = cluster.Min(c => c.Y);
            int cW = cluster.Max(c => c.X) - cMinX + 1;

            foreach (var (ox, oy) in cluster)
                result[(ox, oy)] = (cursorX + (ox - cMinX), oy - cMinY);

            cursorX += cW + 1; // 1-chunk gap
        }

        return result;
    }

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
