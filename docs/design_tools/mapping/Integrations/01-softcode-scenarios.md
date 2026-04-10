# 01 — Softcode All Scenarios into world.db

## Goal

Replace every hardcoded `LoadXxx()` method in `ScenarioLoader` with
database rows. After this refactor, the game has **zero** hardcoded
map builders — all terrain, entities, NPCs, quests, and zone
transitions come from `world.db`.

## Current state

`ScenarioLoader.Load()` dispatches on a string `scenarioId`:

| scenarioId | Method | Map source | Entities | Purpose |
|------------|--------|-----------|----------|---------|
| `m0_combat` | `LoadM0Combat` | Flat 32×32 | 3 radrats (hardcoded) | Combat test |
| `empty` | `LoadEmpty` | Flat 32×32 | None | Smoke test |
| `town` | `LoadTown` | `TownMapBuilder` | 4 NPCs, quests, dialogue | Main town |
| `wasteland` | `LoadWasteland` | `WastelandMapBuilder` | Radrats + scar boss | Combat zone |
| `terrain_test` | `LoadTerrainTest` | `TerrainTestData` | None | Terrain demo |
| `generated` | `LoadGeneratedWorld` | `world.db` via `MapDataProvider` | From DB | Pipeline output |
| *(default)* | `LoadFromCompiledMap` | `Maps/{id}/` JSON files | Buildings + props | Custom maps |

The `generated` path is the closest to the target state — it already
loads from SQLite. The others need to be migrated.

## Target state

**One loading path**: every scenario is a *region* in `world.db` with
chunks, entity spawns, linear features, POIs, and metadata. The
switch statement is deleted.

```
ScenarioLoader.Load(scenarioId) →
  1. Open world.db
  2. Look up region by name = scenarioId
  3. Load chunks via MapDataProvider
  4. Spawn entities from entity_spawn table
  5. Wire NPCs from poi table (type = "npc")
  6. Wire quests from world_meta or a new quest_def table
  7. Wire zone exits from poi table (type = "zone_exit")
```

## Migration plan

### Phase 1: Seed tool — `WorldDbSeeder`

Create a new class `WorldDbSeeder` in `Oravey2.Core.Data` (or a
shared tools assembly) that programmatically builds the same world
the hardcoded methods build, but writes to `world.db`.

```csharp
public sealed class WorldDbSeeder
{
    private readonly WorldMapStore _store;

    public WorldDbSeeder(WorldMapStore store) { _store = store; }

    /// Seed the "town" scenario as a region in the database.
    public long SeedTown()
    {
        var continentId = _store.InsertContinent("haven", null, 1, 1);
        var regionId = _store.InsertRegion(continentId, "town", 0, 0,
            biome: "urban", baseHeight: 0);

        // Build exactly the same TileMapData that TownMapBuilder creates
        var mapData = TownMapBuilder.CreateTownMap();
        SeedChunks(regionId, mapData);
        SeedTownNpcs(regionId);
        SeedTownQuests(regionId);
        SeedTownZoneExits(regionId);
        return regionId;
    }

    public long SeedWasteland() { … }
    public long SeedCombatArena() { … }
    public long SeedEmpty() { … }
    public long SeedTerrainTest() { … }
}
```

Each `SeedXxx()` method:
1. Creates a continent + region row
2. Converts the existing `TileMapData` to chunks (split 32×32 → four
   16×16 chunks, or keep as single chunk if ≤16×16)
3. Inserts entity spawns for enemies, NPCs, and structural entities
4. Inserts POIs for named locations, zone exits, and NPC positions
5. Inserts linear features for roads
6. Stores quest definitions and dialogue tree references in metadata

### Phase 2: Entity spawn conventions

Establish conventions for bootstrapping gameplay systems from
`entity_spawn` rows. Each `prefab_id` maps to a factory:

| `prefab_id` | Factory | Notes |
|-------------|---------|-------|
| `npc:{id}` | `NpcSpawner` | Reads `dialogue_id`, `faction`, creates NpcComponent + InteractionTrigger |
| `enemy:{tag}` | `EnemySpawner` | Reads `level`, `loot_table`, creates enemy capsule + CombatComponent |
| `zone_exit:{target}` | `ZoneExitSpawner` | Creates ZoneExitTriggerScript with target zone |
| `loot_container` | `LootContainerSpawner` | Static searchable container |
| `building:{meshId}` | `BuildingSpawner` | Registers in BuildingRegistry |
| `prop:{meshId}` | `PropSpawner` | Registers in Props array |

The `dialogue_id` column links to dialogue trees loaded from the
content pack. The `condition_flag` column controls conditional spawns
(e.g., scar boss only appears when quest is active).

### Phase 3: NPC and quest data in world.db

Extend the schema with two new tables (or use `world_meta` JSON blobs
for simplicity):

```sql
-- NPC definitions (replaces hardcoded NPC arrays)
CREATE TABLE npc_def (
    id TEXT PRIMARY KEY,            -- "elder", "mara"
    display_name TEXT NOT NULL,
    role TEXT NOT NULL,             -- "quest_giver", "merchant", "civilian"
    dialogue_tree_id TEXT NOT NULL,
    color_r REAL, color_g REAL, color_b REAL
);

-- Quest definitions (replaces hardcoded quest chains)
CREATE TABLE quest_def (
    id TEXT PRIMARY KEY,            -- "q_rat_hunt"
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    type TEXT NOT NULL,             -- "kill", "fetch", "talk"
    first_stage_id TEXT NOT NULL,
    stages_json TEXT NOT NULL,      -- Full quest stage graph
    xp_reward INTEGER
);
```

Alternatively, keep using the content pack JSON files for NPCs/quests
and just reference them by ID from entity spawns. The content pack
is already loaded by `TryLoadContentPack()`.

**Recommended**: Use the content pack JSON path. It's already
implemented and avoids duplicating dialogue/quest data in SQL.
Entity spawns reference NPC IDs → content pack resolves the full
definition.

### Phase 4: Convert map builders to chunk data

Each existing map builder produces a `TileMapData`. Convert to
the chunk format:

```csharp
// Utility: split a TileMapData into 16×16 ChunkData records
public static IEnumerable<(int cx, int cy, TileData[,] tiles)>
    SplitIntoChunks(TileMapData mapData)
{
    int chunksW = (mapData.Width + 15) / 16;
    int chunksH = (mapData.Height + 15) / 16;
    for (int cy = 0; cy < chunksH; cy++)
        for (int cx = 0; cx < chunksW; cx++)
        {
            var tiles = new TileData[16, 16];
            for (int ly = 0; ly < 16; ly++)
                for (int lx = 0; lx < 16; lx++)
                {
                    int wx = cx * 16 + lx;
                    int wy = cy * 16 + ly;
                    tiles[ly, lx] = (wx < mapData.Width && wy < mapData.Height)
                        ? mapData.GetTileData(wx, wy)
                        : TileDataFactory.Ground();
                }
            yield return (cx, cy, tiles);
        }
}
```

### Phase 5: Delete hardcoded methods

Once the seeder can produce identical world.db data for all scenarios:

1. Delete `LoadM0Combat`, `LoadEmpty`, `LoadTown`, `LoadWasteland`,
   `LoadTerrainTest`
2. Delete `TownMapBuilder`, `WastelandMapBuilder`, `TerrainTestData`
3. Delete the `switch` statement in `ScenarioLoader.Load()`
4. Replace with the unified loading path (see `03-unified-loader.md`)

### Phase 6: Bootstrap the default world.db

On first launch (or when no `world.db` exists):

```csharp
if (!File.Exists(worldDbPath))
{
    using var store = new WorldMapStore(worldDbPath);
    var seeder = new WorldDbSeeder(store);
    seeder.SeedTown();
    seeder.SeedWasteland();
    seeder.SeedCombatArena();
    seeder.SeedEmpty();
    seeder.SeedTerrainTest();
}
```

This gives new players the same built-in scenarios, but loaded
entirely from the database.

## ScenarioSelectorScript changes

The built-in `Scenarios` array is deleted. Instead:

```csharp
public static ScenarioInfo[] DiscoverScenarios(WorldMapStore store)
{
    var regions = store.GetAllRegions();
    return regions.Select(r => new ScenarioInfo(
        r.Name,
        r.Description ?? r.Name,
        $"Biome: {r.Biome}",
        ""
    )).ToArray();
}
```

Custom maps from `Maps/` can be imported into `world.db` on discovery
(or kept as a separate fallback for backward compat during transition).

## Entities to softcode

### Town scenario entities

| Current hardcoded | Target entity_spawn |
|-------------------|-------------------|
| Elder NPC at (-4, 0.5, -4.5) | `npc:elder` at chunk(0,0) local(12,11) |
| Mara NPC at (1, 0.5, -3.5) | `npc:mara` at chunk(0,0) local(1,12) |
| Settler 1 at (6, 0.5, 2) | `npc:settler_1` at chunk(0,0) local(6,2) |
| Settler 2 at (-3, 0.5, 5) | `npc:settler_2` at chunk(0,0) local(13,5) |
| Zone exit at (14, 0.5, 9) | `zone_exit:wasteland` at chunk(0,0) local(14,9) |

### Wasteland scenario entities

| Current hardcoded | Target entity_spawn |
|-------------------|-------------------|
| Radrat south at (-2, -2) | `enemy:radrat` at chunk(-1,-1) local(14,14) |
| Radrat east at (2, -2) | `enemy:radrat` at chunk(0,-1) local(2,14) |
| Radrat road at (-2, 0) | `enemy:radrat` at chunk(-1,0) local(14,0) |
| Scar boss at (10, 0) | `enemy:scar` at chunk(0,0) local(10,0), condition_flag=`q_raider_camp_active` |
| Zone exit at (0, 9) | `zone_exit:town` at chunk(0,0) local(0,9) |

### Combat arena entities

| Current hardcoded | Target entity_spawn |
|-------------------|-------------------|
| Enemy 1 at (8, 8) | `enemy:radrat` at chunk(0,0) local(8,8) |
| Enemy 2 at (-6, 10) | `enemy:radrat` at chunk(-1,0) local(10,10) |
| Enemy 3 at (10, -6) | `enemy:radrat` at chunk(0,-1) local(10,10) |

## Testing strategy

1. **Round-trip test**: Seed town → load from DB → verify NPC count,
   tiles match original `TownMapBuilder` output
2. **Entity spawn test**: Insert entity_spawn row → `EntitySpawner`
   creates correct entity type with correct components
3. **Zone exit test**: `zone_exit:wasteland` spawns trigger that
   transitions to region "wasteland"
4. **Conditional spawn test**: Enemy with `condition_flag` only spawns
   when `WorldState.GetFlag()` returns true

## Files changed

| File | Action |
|------|--------|
| `WorldDbSeeder.cs` | **New** — seeds built-in scenarios |
| `EntitySpawner.cs` | **New** — factory for entity_spawn rows |
| `ChunkSplitter.cs` | **New** — splits TileMapData → chunks |
| `ScenarioLoader.cs` | **Major refactor** — delete switch, use unified path |
| `ScenarioSelectorScript.cs` | **Modify** — discover from DB |
| `GameBootstrapper.cs` | **Modify** — open world.db, seed if missing |
| `TownMapBuilder.cs` | **Delete** after migration |
| `WastelandMapBuilder.cs` | **Delete** after migration |
| `TerrainTestData.cs` | **Delete** after migration |
| `WorldDbSchema.sql` | **Extend** — add `npc_def`, `quest_def` tables (optional) |
