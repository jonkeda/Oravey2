# 03 — Unified Region Loader

## Goal

Replace the switch-case in `ScenarioLoader` with a single data-driven
`RegionLoader` that reads chunks, entities, POIs, zone exits, and
gameplay systems from `world.db`. This is the runtime counterpart to
the softcoding (01) and pipeline export (02) refactors.

## Prerequisites

- `01-softcode-scenarios.md` — all built-in scenarios exist as DB rows
- `02-pipeline-db-export.md` — pipeline regions are in DB (optional;
  the loader works with either seeded or exported data)

## Current loading flow

```
GameBootstrapper.StartScenario(id)
  → ScenarioLoader.Load(id)
    → switch(id)
      "m0_combat" → LoadM0Combat()  ← hardcoded
      "town"      → LoadTown()      ← hardcoded
      "wasteland" → LoadWasteland() ← hardcoded
      "generated" → LoadGeneratedWorld() ← reads world.db
      default     → LoadFromCompiledMap(id)
```

Each hardcoded method independently creates: player entity, tile
renderer, HUD, camera, skybox, terrain, NPCs, zone exits, and
gameplay systems (combat, quests, dialogue). This is fragile and
duplicative.

## Target loading flow

```
GameBootstrapper.StartScenario(id)
  → RegionLoader.LoadRegion(id)
    1. Open world.db
    2. region = store.GetRegionByName(id)
    3. Load metadata (biome, baseHeight, description)
    4. Bootstrap player + camera + HUD + skybox       ← always
    5. Bootstrap terrain renderer with MapDataProvider ← always
    6. spawn entities from entity_spawn via prefab dispatch
    7. start gameplay systems based on region metadata
    8. activate ChunkStreamingProcessor around player
```

## RegionLoader design

```csharp
namespace Oravey2.Core.Bootstrap;

public sealed class RegionLoader
{
    private readonly Scene _scene;
    private readonly SceneSystem _sceneSystem;
    private readonly WorldMapStore _worldStore;
    private readonly SaveStateStore _saveStore;
    private readonly ContentPackLoader _contentPackLoader;
    private readonly IReadOnlyList<IEntitySpawnerFactory> _spawners;

    public void LoadRegion(string regionName)
    {
        var region = _worldStore.GetRegionByName(regionName)
            ?? throw new InvalidOperationException(
                $"Region '{regionName}' not found in world.db");

        // Always-on systems
        var player = BootstrapPlayer(region);
        BootstrapCamera(player);
        BootstrapHud();
        BootstrapSkybox(region.Biome);

        // Map data provider for chunk streaming
        var mapProvider = new MapDataProvider(_worldStore, _saveStore);
        var chunkProcessor = new ChunkStreamingProcessor(
            _scene, mapProvider, player.Transform);
        _scene.Entities.Add(new Entity("ChunkProcessor")
            { new ScriptComponent { Scripts = { chunkProcessor } } });

        // Entity spawning
        var entitySpawns = _worldStore.GetEntitySpawnsForRegion(region.Id);
        foreach (var spawn in entitySpawns)
        {
            var factory = _spawners.FirstOrDefault(
                s => s.CanHandle(spawn.PrefabId));
            factory?.Spawn(_scene, spawn, region, _contentPackLoader);
        }

        // Gameplay systems based on region metadata
        BootstrapGameplaySystems(region);
    }
}
```

## Entity spawner factories

Implement `IEntitySpawnerFactory` for each entity category:

```csharp
public interface IEntitySpawnerFactory
{
    bool CanHandle(string prefabId);
    Entity Spawn(Scene scene, EntitySpawnInfo spawn,
        RegionInfo region, ContentPackLoader contentPack);
}
```

### NpcSpawnerFactory

```csharp
public sealed class NpcSpawnerFactory : IEntitySpawnerFactory
{
    public bool CanHandle(string prefabId) =>
        prefabId.StartsWith("npc:", StringComparison.Ordinal);

    public Entity Spawn(Scene scene, EntitySpawnInfo spawn,
        RegionInfo region, ContentPackLoader contentPack)
    {
        var npcId = spawn.PrefabId["npc:".Length..];
        var npcDef = contentPack.GetNpcDefinition(npcId);

        var entity = new Entity(npcDef.DisplayName);
        entity.Transform.Position = spawn.WorldPosition();
        entity.Add(new NpcComponent
        {
            NpcId = npcId,
            DisplayName = npcDef.DisplayName,
            Role = npcDef.Role,
        });
        entity.Add(new InteractionTrigger
        {
            DialogueTreeId = npcDef.DialogueTreeId,
        });
        // Capsule model for NPC
        entity.Add(CreateNpcModel(npcDef.Color));

        scene.Entities.Add(entity);
        return entity;
    }
}
```

### EnemySpawnerFactory

```csharp
public sealed class EnemySpawnerFactory : IEntitySpawnerFactory
{
    public bool CanHandle(string prefabId) =>
        prefabId.StartsWith("enemy:", StringComparison.Ordinal);

    public Entity Spawn(Scene scene, EntitySpawnInfo spawn,
        RegionInfo region, ContentPackLoader contentPack)
    {
        var enemyTag = spawn.PrefabId["enemy:".Length..];

        // Check condition flag
        if (spawn.ConditionFlag != null &&
            !WorldState.GetFlag(spawn.ConditionFlag))
            return null;

        var enemyDef = contentPack.GetEnemyDefinition(enemyTag)
            ?? EnemyDefaults.Get(enemyTag);

        var entity = new Entity(enemyDef.DisplayName);
        entity.Transform.Position = spawn.WorldPosition();
        entity.Add(new EnemyComponent
        {
            Tag = enemyTag,
            Level = spawn.Level ?? 1,
            MaxHp = enemyDef.BaseHp,
        });
        entity.Add(new CapsuleCollider());

        scene.Entities.Add(entity);
        return entity;
    }
}
```

### ZoneExitSpawnerFactory

```csharp
public sealed class ZoneExitSpawnerFactory : IEntitySpawnerFactory
{
    public bool CanHandle(string prefabId) =>
        prefabId.StartsWith("zone_exit:", StringComparison.Ordinal);

    public Entity Spawn(Scene scene, EntitySpawnInfo spawn,
        RegionInfo region, ContentPackLoader contentPack)
    {
        var targetZone = spawn.PrefabId["zone_exit:".Length..];

        var entity = new Entity($"ZoneExit_{targetZone}");
        entity.Transform.Position = spawn.WorldPosition();
        entity.Add(new ZoneExitTriggerScript
        {
            TargetZoneName = targetZone,
        });

        scene.Entities.Add(entity);
        return entity;
    }
}
```

### BuildingSpawnerFactory / PropSpawnerFactory

These read the `building:{meshId}` or `prop:{meshId}` prefix and
instantiate the 3D mesh via the content pack's mesh catalog:

```csharp
public sealed class BuildingSpawnerFactory : IEntitySpawnerFactory
{
    public bool CanHandle(string prefabId) =>
        prefabId.StartsWith("building:", StringComparison.Ordinal);

    public Entity Spawn(Scene scene, EntitySpawnInfo spawn,
        RegionInfo region, ContentPackLoader contentPack)
    {
        var meshId = spawn.PrefabId["building:".Length..];
        var meshPath = contentPack.ResolveMeshPath(meshId);

        var entity = new Entity($"Building_{meshId}");
        entity.Transform.Position = spawn.WorldPosition();
        // Load GLB at runtime (Stride content pipeline or custom loader)
        entity.Add(MeshLoader.LoadGlb(meshPath));

        scene.Entities.Add(entity);
        return entity;
    }
}
```

## Gameplay system bootstrap

Each region has metadata (stored in `world_meta` or on the `region`
row) that tells the loader which gameplay systems to activate:

```csharp
private void BootstrapGameplaySystems(RegionInfo region)
{
    // Combat engine — always active but only engages when enemies exist
    _scene.Entities.Add(new Entity("CombatEngine")
        { new ScriptComponent { Scripts = { new CombatEngineScript() } } });

    // Dialogue system — always active
    _scene.Entities.Add(new Entity("DialogueSystem")
        { new ScriptComponent { Scripts = { new DialogueSystem() } } });

    // Quest tracker — loads active quests from save state
    var questTracker = new QuestTrackerScript();
    questTracker.LoadFrom(_saveStore);
    _scene.Entities.Add(new Entity("QuestTracker")
        { new ScriptComponent { Scripts = { questTracker } } });

    // Kill tracker — reads kill objectives from entity_spawn conditions
    var killTracker = new KillTrackerScript();
    _scene.Entities.Add(new Entity("KillTracker")
        { new ScriptComponent { Scripts = { killTracker } } });
}
```

## ZoneManager refactor

Currently `ZoneManager` has hardcoded zone names:

```csharp
// Before
case "wasteland": LoadWasteland(); break;
case "town": LoadTown(); break;
```

After this refactor, zone transitions become region transitions:

```csharp
public void TransitionTo(string targetRegionName)
{
    _regionLoader.UnloadCurrentRegion();
    _regionLoader.LoadRegion(targetRegionName);
}
```

The `ZoneExitTriggerScript` calls `ZoneManager.TransitionTo(targetZone)`
where `targetZone` is a region name in `world.db`.

## Player position and save state

When loading a region:

1. Check save state for last known position in this region
2. If none, use the region's spawn point from `world_meta`
3. If no spawn point, use chunk (0,0), position (8, 0, 8) (center)

```csharp
private Entity BootstrapPlayer(RegionInfo region)
{
    var savedPos = _saveStore?.GetPlayerPosition(region.Name);
    var startPos = savedPos
        ?? GetRegionSpawnPoint(region)
        ?? new Vector3(8f, 0f, 8f);

    var player = PlayerFactory.Create(startPos);
    _scene.Entities.Add(player);
    return player;
}
```

## Migration: GameBootstrapper changes

```csharp
// Before
public void StartScenario(string id)
{
    _scenarioLoader = new ScenarioLoader(_scene, _sceneSystem);
    _scenarioLoader.Load(id);
}

// After
public void StartScenario(string id)
{
    var worldStore = new WorldMapStore(_worldDbPath);
    var saveStore = new SaveStateStore(_saveDbPath);
    var spawners = new IEntitySpawnerFactory[]
    {
        new NpcSpawnerFactory(),
        new EnemySpawnerFactory(),
        new ZoneExitSpawnerFactory(),
        new BuildingSpawnerFactory(),
        new PropSpawnerFactory(),
        new LootContainerSpawnerFactory(),
    };
    _regionLoader = new RegionLoader(
        _scene, _sceneSystem, worldStore, saveStore,
        _contentPackLoader, spawners);
    _regionLoader.LoadRegion(id);
}
```

## New WorldMapStore methods needed

```csharp
// Get a region by its display name
public RegionInfo? GetRegionByName(string name);

// Get all entity spawns across all chunks in a region
public IReadOnlyList<EntitySpawnInfo> GetEntitySpawnsForRegion(long regionId);

// Get all regions (for scenario selector)
public IReadOnlyList<RegionInfo> GetAllRegions();
```

These are straightforward SQL queries against the existing schema.

## Testing strategy

1. **Load seeded town**: Seed town via `WorldDbSeeder`, load via
   `RegionLoader`, assert 4 NPC entities in scene
2. **Load empty region**: Seed empty, load, assert scene contains
   player + camera + terrain renderer, zero NPCs
3. **Zone transition**: Load town, trigger zone exit, assert
   wasteland region loads with enemies
4. **Unknown region**: `LoadRegion("nonexistent")` →
   `InvalidOperationException`
5. **Conditional spawn**: Set flag → enemy appears; clear flag →
   enemy skipped
6. **Save/restore position**: Save player position in town, load
   town again, assert position restored

## Files changed

| File | Action |
|------|--------|
| `RegionLoader.cs` | **New** in `Oravey2.Core.Bootstrap` |
| `IEntitySpawnerFactory.cs` | **New** interface |
| `NpcSpawnerFactory.cs` | **New** |
| `EnemySpawnerFactory.cs` | **New** |
| `ZoneExitSpawnerFactory.cs` | **New** |
| `BuildingSpawnerFactory.cs` | **New** |
| `PropSpawnerFactory.cs` | **New** |
| `GameBootstrapper.cs` | **Major refactor** |
| `ScenarioLoader.cs` | **Delete** (replaced by RegionLoader) |
| `ZoneManager.cs` | **Refactor** — delegate to RegionLoader |
| `WorldMapStore.cs` | **Extend** — new query methods |
| `ScenarioSelectorScript.cs` | **Refactor** — discover from DB |
