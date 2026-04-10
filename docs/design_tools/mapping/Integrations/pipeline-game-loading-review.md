# Pipeline → Game Loading Review

**Date:** 2026-04-10
**Scope:** Steps 01–10 of the MapGen pipeline vs Oravey2 runtime loading

---

## Summary

The pipeline produces a **content pack** directory. The game has **two
independent** loading paths, and the pipeline output currently fits
**neither** without manual bridging work. The issues range from
"wrong directory" to "schema mismatch" to "code path not yet written."

| Verdict | Count |
|---------|-------|
| **Blockers** (will crash or load nothing) | 4 |
| **Gaps** (feature not wired, data ignored) | 5 |
| **Schema mismatches** (silent data loss) | 3 |
| **OK** (compatible today) | 4 |

---

## 1  How the game loads content today

### Path A — `LoadFromCompiledMap` (custom map)

Triggered by `ScenarioLoader.Load(scenarioId, …)` when `scenarioId` is not
a built-in name. Looks for:

```
{AppContext.BaseDirectory}/Maps/{scenarioId}/
   buildings.json     ← BuildingSerializer reads this
   props.json         ← BuildingSerializer reads this
```

Loads into `HeightmapTerrainScript` with a single default-sized `TileMapData`.  
**Does NOT read:** `layout.json`, `zones.json`, `design.json`, overworld
files, or `catalog.json`.

### Path B — `LoadGeneratedWorld` (SQLite database)

Triggered when `scenarioId == "generated"`. Reads `world.db` via
`WorldMapStore` + `MapDataProvider`.

### Path C — Content pack discovery (`ContentPackService`)

Scans `{AppContext.BaseDirectory}/ContentPacks/` for `manifest.json`.
Loads `data/items.json`, `data/npcs.json`, `data/enemies.json`,
`data/dialogues/*.json`, `data/quests/*.json`.  
**Never loads:** towns, overworld, scenarios, or catalog at runtime.

---

## 2  What the pipeline produces (content pack directory)

```
{ContentPackPath}/
   manifest.json                      ← Step 01 (region) creates skeleton
   catalog.json                       ← Step 10 rebuilds
   data/curated-towns.json            ← Step 04
   overworld/world.json               ← Step 06
   overworld/roads.json               ← Step 06
   overworld/water.json               ← Step 06
   scenarios/{id}.json                ← Step 10
   towns/{gameName}/design.json       ← Step 05
   towns/{gameName}/layout.json       ← Step 06
   towns/{gameName}/buildings.json    ← Step 06
   towns/{gameName}/props.json        ← Step 06
   towns/{gameName}/zones.json        ← Step 06
   assets/meshes/*.glb                ← Step 09
   assets/meshes/*.meta.json          ← Step 09
```

---

## 3  Blocker issues

### B-1  Content pack is not deployed to `ContentPacks/`

The pipeline writes to an arbitrary `ContentPackPath` (usually under
`content/`). The game scans `{AppContext.BaseDirectory}/ContentPacks/`.
There is **no copy/link/deploy step** connecting the two.

**Impact:** Content pack is invisible to the game at runtime. NPCs, items,
dialogues, and quests from the pack never load.

**Fix:** Add a deploy step that copies (or symlinks) the content pack
directory into the game's `ContentPacks/` folder, or make the game
scan `content/` as well.

---

### B-2  Town maps are not deployed to `Maps/{scenarioId}/`

`LoadFromCompiledMap` looks in `Maps/{scenarioId}/` for `buildings.json`
and `props.json`. The pipeline stores them under
`towns/{gameName}/buildings.json`. The game never looks in `towns/`.

**Impact:** Picking a scenario that should load a town results in
"Unknown scenario, falling back to m0_combat." No generated town map
ever renders.

**Fix:** Either:
- (a) Add a build step that copies/flattens each town's files into
  `Maps/{gameName}/`, or
- (b) Extend `ScenarioLoader` to look inside
  `ContentPacks/{pack}/towns/{id}/` when loading a scenario.

---

### B-3  Scenario file schema mismatch

The pipeline's `ScenarioFile` writes:

```json
{ "id", "name", "description", "towns": [...], "playerStart": {…},
  "difficulty", "tags": [...] }
```

The game's `ScenarioDefinition` record expects:

```json
{ "id", "name", "description", "map", "features", "difficulty", "tags" }
```

Key differences:
| Pipeline field | Core field | Status |
|----------------|-----------|--------|
| `towns` | *(none)* | **Ignored** — Core has no `Towns` property |
| `playerStart` | *(none)* | **Ignored** |
| *(none)* | `map` | **Missing** — Core uses `Map` to resolve the map dir |
| *(none)* | `features` | **Missing** (nullable, minor) |

**Impact:** `ContentPackService.LoadScenarios()` can deserialize the file
without error (extra properties are silently ignored with
`JsonNamingPolicy.CamelCase`), but the `Map` field is always null.
The scenario is discoverable but unplayable because the game doesn't
know which map directory to load.

**Fix:** Either:
- Add a `map` field to `ScenarioFile` pointing to the town directory,
  or
- Have `ScenarioLoader` resolve the `map` field from the first entry
  in `towns[]`, or
- Add `Towns` and `PlayerStart` to `ScenarioDefinition` and teach
  `ScenarioLoader` to chain-load all towns.

---

### B-4  `LoadFromCompiledMap` ignores layout.json (heightmap)

`LoadFromCompiledMap` creates a blank `TileMapData(ChunkData.Size,
ChunkData.Size)` with a TODO comment: *"Step 10 will wire this to
MapDataProvider/SQLite."* The generated `layout.json` (32×32 surface
heightmap) is **never read**.

**Impact:** Terrain is flat. Buildings and props are placed per their
chunk/tile coordinates, but the heightmap undulations and surface types
from the pipeline are lost.

**Fix:** Read `layout.json` into `TileMapData.HeightMap` inside
`LoadFromCompiledMap` (or the new content-pack-aware loader).

---

## 4  Gap issues (feature not wired, data ignored)

### G-1  `zones.json` is never loaded by the game

The game has no code that reads `zones.json`. Zone data (biome,
radiation, enemy difficulty) is pipeline-only metadata that never
reaches the runtime.

### G-2  `design.json` is never loaded by the game

Narrative metadata (landmark names, key locations, hazards) has no
consumer in `Oravey2.Core`.

### G-3  Overworld files (`world.json`, `roads.json`, `water.json`) are only used by the scenario selector UI

`ScenarioSelectorScript.DiscoverCustomMaps()` reads `world.json` to
show map names, but `ScenarioLoader` never uses overworld data to
set up roads, water bodies, or multi-town navigation at runtime.

### G-4  `catalog.json` is never consumed at runtime

`ContentPackService.GetCatalogPath()` exists but is never called.
The catalog represents available assets but nothing in the rendering
pipeline reads it.

### G-5  `data/` directory has no items, NPCs, enemies, dialogues, or quests

The pipeline produces `data/curated-towns.json` but no gameplay data
files. `ContentPackLoader` tries to load `data/items.json`,
`data/npcs.json`, `data/enemies.json`, `data/dialogues/*.json`, and
`data/quests/*.json` — all missing. The game silently falls back to
hardcoded defaults.

This is not a bug per se (fallbacks exist), but means the pipeline
produces a content pack that adds **zero** gameplay content beyond
terrain and buildings. All NPCs, items, and quests are still hardcoded.

---

## 5  Schema mismatches (silent data loss)

### S-1  Manifest field mismatch

Pipeline `ManifestFile` includes fields not in Core's `ContentManifest`:

| Pipeline | Core `ContentManifest` |
|----------|----------------------|
| `author` | *(missing)* |
| `engineVersion` | *(missing)* |
| `parent` | *(missing)* |
| `palette` | *(missing)* |
| `defaultScenario` | ✓ `DefaultScenario` |

Core only reads `Id`, `Name`, `Version`, `Description`,
`DefaultScenario`, `Tags`. Extra fields are silently dropped during
deserialization. The pack still loads, but parent-chain validation is
never enforced at runtime.

### S-2  Building JSON differences

Pipeline `BuildingFile` (via `TownMapFiles`) writes fields:
`id`, `name`, `meshAsset`, `size`, `footprint`, `floors`, `condition`,
`placement`

Core `BuildingJson` (via `BuildingSerializer`) expects:
`id`, `name`, `meshAsset`, `size`, `footprint`, `floors`, `condition`,
**`interiorChunkId`**, `placement`

The formats are **compatible** (Core's `InteriorChunkId` is nullable),
but the pipeline never generates `interiorChunkId`, so interior zones
will not work for any pipeline-generated building.

### S-3  Mesh path base directory ambiguity

Pipeline buildings reference meshes as `meshes/primitives/cube.glb`
(relative to content pack root). `LoadFromCompiledMap` puts the
building into a `BuildingDefinition` but the game's mesh rendering
is **not yet implemented** (`HeightmapTerrainScript` accepts
`Buildings` and `Props` but doesn't instantiate mesh entities).

When mesh loading is eventually implemented, the code must resolve
paths relative to the **content pack root**, not the map directory or
the executable directory. This is currently undefined.

---

## 6  What works today

| Item | Status |
|------|--------|
| `manifest.json` round-trip (Core reads Id, Name, Version correctly) | ✅ |
| `buildings.json` / `props.json` JSON format compatibility | ✅ |
| `BuildingSerializer` can deserialize pipeline output (tested with Island Haven) | ✅ |
| Walkability footprints apply correctly from pipeline building data | ✅ |

---

## 7  Recommended fixes (priority order)

| # | Fix | Effort |
|---|-----|--------|
| 1 | Add `Map` field to pipeline `ScenarioFile`, set to the town game-name | S |
| 2 | Deploy step: copy content pack into `ContentPacks/` and each town into `Maps/{gameName}/` | M |
| 3 | Read `layout.json` in `LoadFromCompiledMap` to populate the heightmap | S |
| 4 | Add `Towns`, `PlayerStart` to `ScenarioDefinition`; teach `ScenarioLoader` to chain-load | L |
| 5 | Read `zones.json` at runtime — wire biome, radiation, enemy tiers into gameplay | L |
| 6 | Read overworld data at runtime — roads, water, multi-town travel | L |
| 7 | Implement GLB mesh loading from `MeshAssetPath` in `HeightmapTerrainScript` | L |
| 8 | Pipeline step to generate `data/items.json`, `npcs.json`, etc. | L |

Fixes 1–3 are blocking and would unblock a basic "load one town with
terrain and buildings" flow. Fixes 4–8 are needed for the full
multi-town, multi-scenario, mesh-rendered experience.

---

## 8  Conclusion

The pipeline tooling (steps 01–10) produces structurally valid content
pack files that pass internal validation. However, **the game cannot
load this output as-is** due to directory placement (B-1, B-2), schema
gaps (B-3), and unimplemented loading code (B-4). The most critical
path — rendering a single generated town with its heightmap and
building placements — requires fixes 1–3 above.
