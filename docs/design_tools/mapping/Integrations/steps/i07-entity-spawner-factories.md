# Step i07 — IEntitySpawnerFactory + Spawners

**Design doc:** 03
**Depends on:** i05 (WorldMapStore queries — for `EntitySpawnInfo`
with chunk coords)
**Deliverable:** `IEntitySpawnerFactory` interface and concrete
spawner factories for NPC, enemy, zone exit, building, and prop
entity types.

---

## Goal

Create a dispatch system that turns `entity_spawn` database rows into
Stride entities at runtime. Each spawner handles one `prefab_id`
prefix and knows how to create the correct entity with components.

NPC/quest/dialogue definitions come from content pack JSON files
(per QA §5) — spawners only receive IDs and resolve from the pack.

---

## Tasks

### i07.1 — Define `IEntitySpawnerFactory`

File: `src/Oravey2.Core/Bootstrap/IEntitySpawnerFactory.cs`

- [ ] Interface:
  ```csharp
  public interface IEntitySpawnerFactory
  {
      bool CanHandle(string prefabId);
      Entity? Spawn(Scene scene, EntitySpawnInfo spawn,
          int chunkX, int chunkY);
  }
  ```
- [ ] Returns `null` if spawn should be skipped (conditional flag
  not met)

### i07.2 — `NpcSpawnerFactory`

File: `src/Oravey2.Core/Bootstrap/Spawners/NpcSpawnerFactory.cs`

- [ ] Handles `npc:*` prefab IDs
- [ ] Extracts NPC ID from prefix
- [ ] Creates entity with: `NpcComponent`, `InteractionTrigger`,
  capsule model
- [ ] NPC display name, dialogue ID, color come from content pack
  JSON (resolved via `DialogueId` field on `EntitySpawnInfo`, or
  a simple JSON lookup)
- [ ] For now, keep simple — hardcode fallback display name from
  the ID if content pack has no definition

### i07.3 — `EnemySpawnerFactory`

File: `src/Oravey2.Core/Bootstrap/Spawners/EnemySpawnerFactory.cs`

- [ ] Handles `enemy:*` prefab IDs
- [ ] Checks `ConditionFlag` — skip spawn if flag not set
- [ ] Creates entity with: `EnemyComponent` (tag, level, HP),
  capsule collider
- [ ] HP/level from `EntitySpawnInfo.Level` or defaults

### i07.4 — `ZoneExitSpawnerFactory`

File: `src/Oravey2.Core/Bootstrap/Spawners/ZoneExitSpawnerFactory.cs`

- [ ] Handles `zone_exit:*` prefab IDs
- [ ] Creates entity with `ZoneExitTriggerScript` pointing at target
  region name

### i07.5 — `BuildingSpawnerFactory`

File: `src/Oravey2.Core/Bootstrap/Spawners/BuildingSpawnerFactory.cs`

- [ ] Handles `building:*` and `building_ruin` prefab IDs
- [ ] Creates entity positioned at spawn world coords
- [ ] Registers in building registry if one exists
- [ ] Mesh loading is a placeholder for now (cube primitive)

### i07.6 — `PropSpawnerFactory`

File: `src/Oravey2.Core/Bootstrap/Spawners/PropSpawnerFactory.cs`

- [ ] Handles `prop:*` prefab IDs
- [ ] Creates entity with rotation and scale from spawn info
- [ ] Mesh loading is a placeholder for now

### i07.7 — `EntitySpawnerDispatcher` (convenience)

File: `src/Oravey2.Core/Bootstrap/EntitySpawnerDispatcher.cs`

- [ ] Holds a list of `IEntitySpawnerFactory`
- [ ] `SpawnAll(scene, spawns)` iterates and dispatches:
  ```csharp
  public void SpawnAll(Scene scene,
      IEnumerable<(int ChunkX, int ChunkY, EntitySpawnInfo Spawn)> spawns)
  {
      foreach (var (cx, cy, spawn) in spawns)
      {
          var factory = _factories.FirstOrDefault(
              f => f.CanHandle(spawn.PrefabId));
          factory?.Spawn(scene, spawn, cx, cy);
      }
  }
  ```

### i07.8 — Tests

File: `tests/Oravey2.Tests/Bootstrap/EntitySpawnerTests.cs`

- [ ] `NpcSpawnerFactory_CanHandle_NpcPrefix` — true for `npc:elder`
- [ ] `NpcSpawnerFactory_CanHandle_RejectsEnemy` — false for
  `enemy:radrat`
- [ ] `EnemySpawnerFactory_ConditionFlag_SkipsWhenNotSet`
- [ ] `ZoneExitSpawnerFactory_ExtractsTargetRegion` — verify
  `zone_exit:wasteland` → target = "wasteland"
- [ ] `EntitySpawnerDispatcher_DispatchesToCorrectFactory`
- [ ] Build + all tests pass

---

## Files changed

| File | Action |
|------|--------|
| `IEntitySpawnerFactory.cs` | **New** |
| `NpcSpawnerFactory.cs` | **New** |
| `EnemySpawnerFactory.cs` | **New** |
| `ZoneExitSpawnerFactory.cs` | **New** |
| `BuildingSpawnerFactory.cs` | **New** |
| `PropSpawnerFactory.cs` | **New** |
| `EntitySpawnerDispatcher.cs` | **New** |
| `EntitySpawnerTests.cs` | **New** |
