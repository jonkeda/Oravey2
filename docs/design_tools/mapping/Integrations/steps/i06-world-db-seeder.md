# Step i06 — WorldDbSeeder (debug.db)

**Design doc:** 01 phases 1+6, QA §3
**Depends on:** i04 (ChunkSplitter), i05 (WorldMapStore queries)
**Deliverable:** `WorldDbSeeder` that seeds all 5 built-in scenarios
into a separate `debug.db`. Debug regions are isolated from game
regions.

---

## Goal

Convert the hardcoded `LoadM0Combat`, `LoadEmpty`, `LoadTown`,
`LoadWasteland`, and `LoadTerrainTest` data into database rows.
Write to `debug.db` (not `world.db`) per QA §3. This is a
prerequisite for deleting the hardcoded methods later (step i13).

---

## Tasks

### i06.1 — Create `WorldDbSeeder`

File: `src/Oravey2.Core/Data/WorldDbSeeder.cs`

- [ ] Constructor takes `WorldMapStore`
- [ ] `SeedAll()` method calls each individual seeder
- [ ] Each method creates continent + region + chunks + entity spawns + POIs

### i06.2 — `SeedEmpty()`

- [ ] Region "empty", biome "test"
- [ ] Single 16×16 chunk of flat `SurfaceType.Dirt` tiles
- [ ] No entity spawns
- [ ] Simplest — good first method to implement

### i06.3 — `SeedCombatArena()`

- [ ] Region "m0_combat", biome "test"
- [ ] Flat 32×32 → 4 chunks via `ChunkSplitter`
- [ ] 3 entity spawns: `enemy:radrat` at known positions
- [ ] Positions from `01-softcode-scenarios.md` entity table

### i06.4 — `SeedTerrainTest()`

- [ ] Region "terrain_test", biome "test"
- [ ] Reproduce `TerrainTestData` height gradients as `TileData` with
  varying `HeightLevel`
- [ ] No entity spawns

### i06.5 — `SeedTown()`

- [ ] Region "town", biome "urban"
- [ ] Use `TownMapBuilder.CreateTownMap()` → `ChunkSplitter.Split()`
- [ ] 4 NPC entity spawns: `npc:elder`, `npc:mara`,
  `npc:settler_1`, `npc:settler_2`
- [ ] 1 zone exit: `zone_exit:wasteland`
- [ ] POI for town center
- [ ] NPC definitions themselves stay in content pack JSON (per QA §5)
  — only the spawn references go in DB

### i06.6 — `SeedWasteland()`

- [ ] Region "wasteland", biome "wasteland"
- [ ] Use `WastelandMapBuilder.CreateWastelandMap()` →
  `ChunkSplitter.Split()`
- [ ] 3 radrat spawns + 1 scar boss with
  `ConditionFlag = "q_raider_camp_active"`
- [ ] 1 zone exit: `zone_exit:town`

### i06.7 — Bootstrap integration

File: `src/Oravey2.Core/Bootstrap/GameBootstrapper.cs`

- [ ] On startup, check if `debug.db` exists — if not, create and
  seed
- [ ] Only run under `#if DEBUG` or a config flag
- [ ] Do NOT touch `world.db`

### i06.8 — Tests

File: `tests/Oravey2.Tests/Data/WorldDbSeederTests.cs`

- [ ] `SeedEmpty_CreatesRegionWithOneChunk`
- [ ] `SeedCombatArena_CreatesThreeEnemySpawns`
- [ ] `SeedTown_CreatesFourNpcSpawns`
- [ ] `SeedTown_CreatesZoneExit`
- [ ] `SeedWasteland_CreatesScarWithConditionFlag`
- [ ] `SeedAll_CreatesAllFiveRegions` — verify
  `store.GetAllRegions().Count == 5`
- [ ] `SeedTown_ChunkTilesMatchTownMapBuilder` — compare tile data
  from seeded DB with direct `TownMapBuilder.CreateTownMap()` output
- [ ] Build + all tests pass

---

## Files changed

| File | Action |
|------|--------|
| `WorldDbSeeder.cs` | **New** in `Oravey2.Core/Data/` |
| `GameBootstrapper.cs` | **Modify** — add debug.db bootstrap |
| `WorldDbSeederTests.cs` | **New** |
