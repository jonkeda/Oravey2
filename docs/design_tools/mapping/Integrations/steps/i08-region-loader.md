# Step i08 — RegionLoader

**Design doc:** 03
**Depends on:** i06 (WorldDbSeeder — data to load), i07 (entity
spawner factories)
**Deliverable:** `RegionLoader` that replaces the `ScenarioLoader`
switch. Loads any region from `world.db` or `debug.db` using
`MapDataProvider` + spawner dispatch.

---

## Goal

Create a single `RegionLoader` class that can load any region by
name from the database. It bootstraps the player, camera, HUD,
terrain, entities, and gameplay systems — everything the old
hardcoded methods did, but entirely data-driven.

---

## Tasks

### i08.1 — Create `RegionLoader`

File: `src/Oravey2.Core/Bootstrap/RegionLoader.cs`

- [ ] Constructor takes: `Scene`, `SceneSystem`, `WorldMapStore`,
  `SaveStateStore?`, `EntitySpawnerDispatcher`
- [ ] `LoadRegion(string regionName, Vector3? spawnOverride = null)`:
  1. Query `store.GetRegionByName(regionName)` — throw if null
  2. Bootstrap player at spawn point (from save, region meta, or
     default center)
  3. Bootstrap camera following player
  4. Bootstrap HUD
  5. Bootstrap skybox (parameterized by biome)
  6. Create `MapDataProvider` + `ChunkStreamingProcessor`
  7. Query `store.GetEntitySpawnsForRegion(regionId)` → dispatch
     via `EntitySpawnerDispatcher`
  8. Bootstrap gameplay systems (combat, dialogue, quest tracker)
- [ ] `UnloadCurrentRegion()`:
  1. Remove all entities from scene
  2. Dispose streaming processor
  3. Clear state

### i08.2 — Extract bootstrap helpers from `ScenarioLoader`

The existing `ScenarioLoader` already has working code for creating
the player, camera, HUD, skybox, terrain renderer, etc. Extract these
into private methods on `RegionLoader`:

- [ ] `BootstrapPlayer(Vector3 position)` — from
  `ScenarioLoader`'s player creation code
- [ ] `BootstrapCamera(Entity player)` — from camera setup code
- [ ] `BootstrapHud()` — from HUD setup code
- [ ] `BootstrapSkybox(string biome)` — from skybox code
- [ ] `BootstrapGameplaySystems()` — combat engine, dialogue, quest
  tracker

### i08.3 — Multi-DB support (world.db + debug.db)

- [ ] `RegionLoader` accepts a list of `WorldMapStore` instances
- [ ] `LoadRegion` tries each store in order:
  ```csharp
  var region = _stores
      .Select(s => (Store: s, Region: s.GetRegionByName(name)))
      .FirstOrDefault(x => x.Region != null);
  ```
- [ ] This allows loading from `world.db` first, fallback to
  `debug.db`

### i08.4 — Wire into `GameBootstrapper`

File: `src/Oravey2.Core/Bootstrap/GameBootstrapper.cs`

- [ ] Replace `ScenarioLoader` instantiation with `RegionLoader`
- [ ] Open world.db + optionally debug.db
- [ ] `StartScenario(id)` calls `_regionLoader.LoadRegion(id)`
- [ ] Keep `ScenarioLoader` alive temporarily (don't delete yet —
  that's step i13)

### i08.5 — Tests

File: `tests/Oravey2.Tests/Bootstrap/RegionLoaderTests.cs`

- [ ] `LoadRegion_Empty_CreatesPlayerAndTerrain` — seed empty →
  load → verify player entity exists
- [ ] `LoadRegion_Town_SpawnsFourNpcs` — seed town → load → verify
  4 NPC entities
- [ ] `LoadRegion_UnknownRegion_Throws`
- [ ] `LoadRegion_MultiDb_FindsInSecondStore` — region in debug.db
  only → still loads
- [ ] `UnloadCurrentRegion_ClearsScene`

Note: full entity creation requires Stride Scene, so tests may need
to mock the scene or test only the query + dispatch logic without
the Stride entity creation.

- [ ] Build + all tests pass

---

## Files changed

| File | Action |
|------|--------|
| `RegionLoader.cs` | **New** in `Oravey2.Core/Bootstrap/` |
| `GameBootstrapper.cs` | **Modify** — wire RegionLoader |
| `RegionLoaderTests.cs` | **New** |
