# Step i09 — ContentPackImporter (Game-Side)

**Design doc:** 02, QA §8
**Depends on:** i04 (ChunkSplitter)
**Deliverable:** `ContentPackImporter` in `Oravey2.Core` that reads
a content pack JSON directory and inserts chunks, entities, POIs, and
linear features into `world.db`.

---

## Goal

The game needs to import content packs without depending on the
MapGen tool assembly. This importer lives in `Oravey2.Core` and reads
the JSON interchange format that the pipeline produces. Used for:
- Dev/debug import via UI button
- Auto-import on startup for shipped content packs / DLC
- Import of player-downloaded content packs

---

## Tasks

### i09.1 — Create `ContentPackImporter`

File: `src/Oravey2.Core/Data/ContentPackImporter.cs`

- [ ] Constructor takes `WorldMapStore`
- [ ] `Import(string contentPackPath)` → `ImportResult`
- [ ] Steps:
  1. Read `manifest.json` → get region name, description
  2. Create continent + region rows
  3. For each `towns/{name}/` subdirectory:
     - Read `layout.json` → build `TileMapData` from surface array
       (use `SurfaceType` enum directly)
     - Read `buildings.json` → entity spawns
     - Read `props.json` → entity spawns
     - Read `zones.json` → POIs
     - Split tile map via `ChunkSplitter` → insert chunks + spawns
  4. Read `overworld/roads.json` → linear features
  5. Read `overworld/water.json` → linear features
  6. Read `data/curated-towns.json` → POIs
  7. Store `content_pack_root` in `world_meta`
  8. Return `ImportResult`

### i09.2 — `ImportResult`

File: `src/Oravey2.Core/Data/ContentPackImporter.cs` (nested or
separate)

- [ ] ```csharp
  public sealed class ImportResult
  {
      public string RegionName { get; set; } = "";
      public int TownsImported { get; set; }
      public int ChunksWritten { get; set; }
      public int PoisInserted { get; set; }
      public int LinearFeaturesInserted { get; set; }
      public int EntitySpawnsInserted { get; set; }
      public List<string> Warnings { get; } = [];
  }
  ```

### i09.3 — Layout → TileMapData conversion

- [ ] Read `layout.json` — expected format:
  ```json
  { "width": 48, "height": 32, "surface": [[0,1,2,...], ...] }
  ```
- [ ] Map surface integers directly to `SurfaceType` via cast
  (relies on i02 fixing the value mismatch)
- [ ] Build `TileMapData` and apply building footprints for
  walkability

### i09.4 — Building/prop → entity spawn conversion

- [ ] Read `buildings.json` — each entry has `Id`, `MeshAsset`,
  `Placement` (ChunkX, ChunkY, LocalTileX, LocalTileY)
- [ ] Convert to `EntitySpawnInfo(PrefabId: $"building:{id}", ...)`
- [ ] Same for `props.json` → `prop:{id}`

### i09.5 — Overworld → linear features

- [ ] Read `roads.json` — each road has `LinearFeatureType`, nodes
  (after i01 unification)
- [ ] Read `water.json` — same pattern
- [ ] Insert via `store.InsertLinearFeature()`

### i09.6 — Auto-import on startup

File: `src/Oravey2.Core/Bootstrap/GameBootstrapper.cs`

- [ ] On startup, scan `ContentPacks/` directory
- [ ] For each pack not yet imported (check `world_meta` for
  `imported:{packId}`), run import
- [ ] Mark as imported: `store.GetOrSetMeta($"imported:{packId}", "true")`

### i09.7 — Tests

File: `tests/Oravey2.Tests/Data/ContentPackImporterTests.cs`

- [ ] `Import_SingleTown_CreatesChunksAndSpawns` — create a
  minimal content pack on disk (temp dir), import, verify DB rows
- [ ] `Import_Overworld_CreatesLinearFeatures`
- [ ] `Import_MissingLayout_WarnsButContinues`
- [ ] `Import_AlreadyImported_SkipsOnAutoImport`
- [ ] `Import_LargeTown_ChunksCorrectly` — 64×48 layout → 4×3 chunks
- [ ] Build + all tests pass

---

## Files changed

| File | Action |
|------|--------|
| `ContentPackImporter.cs` | **New** in `Oravey2.Core/Data/` |
| `GameBootstrapper.cs` | **Modify** — auto-import on startup |
| `ContentPackImporterTests.cs` | **New** |
