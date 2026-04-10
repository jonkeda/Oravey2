# Integration Plan — Q&A

Answers to review questions on the integration refactor plans
(`01`–`04`).

---

## 1. Cities bigger than 32×32

**Yes, chunking handles this natively.** `ChunkData.Size` is a const
16. A city of any size is split into as many 16×16 chunks as needed.
A 64×48 city becomes 4×3 = 12 chunks. `TownLayout` already carries
its own `Width` and `Height` — the pipeline does not assume 32×32.

The `ChunkSplitter` utility proposed in `02-pipeline-db-export.md`
iterates `(width+15)/16 × (height+15)/16`, padding edge chunks with
default tiles. No upper limit on city size.

The 32×32 figure in `01-softcode-scenarios.md` only describes the
**existing hardcoded** `TownMapBuilder` / `WastelandMapBuilder`. The
new data-driven path has no such constraint.

---

## 2. Rectangular (non-square) cities

**Yes.** `TownLayout(Width, Height, Surface)` already supports
independent width and height. `ChunkSplitter.Split()` uses
`mapData.Width` and `mapData.Height` independently — no assumption of
square. Rectangular towns produce a rectangular grid of chunks.

---

## 3. Separate Game DB and Debug DB

**Good idea.** Two databases:

| Database | Contents | Shipped with game? |
|----------|----------|--------------------|
| `world.db` | Pipeline-generated and player-created regions | Yes (can be empty on first launch) |
| `debug.db` | Seeded built-in test scenarios (town, wasteland, combat arena, empty, terrain_test) | Dev builds only |

Changes to the plan:

- `WorldDbSeeder` writes to `debug.db` instead of `world.db`
- `RegionSelectorScript` reads from both databases; debug regions show
  a `[DEBUG]` tag and are hidden in release builds via a compile flag
  or config toggle
- `GameBootstrapper` opens `world.db` always, and opens `debug.db`
  only when `#if DEBUG` or a settings flag is set
- Pipeline export always targets `world.db`

This keeps test data out of the shipping database and avoids polluting
the player's save with debug scenarios. Plans `01` and `03` will be
updated to reflect this split.

---

## 4. SQLite vs flat files

**Flat files are a viable alternative.** The argument for each:

| Concern | SQLite | Flat files (JSON per chunk) |
|---------|--------|---------------------------|
| Query by region/chunk | Built-in SQL | Directory walk + naming convention |
| Partial loading | `SELECT WHERE chunk_x/y` | Read single file |
| Entity spawns | JOIN across tables | Co-located in chunk JSON |
| Write performance | Transaction batching | One file write per chunk |
| Merge / diff | Binary blob, hard to diff | Text, easy to diff and merge |
| Shipping as package | Single `.db` file | Zip/folder of files |
| Tooling | DB Browser, SQL queries | Any text editor |
| Existing code | `WorldMapStore` already works | Would need new loader |

**Recommendation**: Keep SQLite for the **runtime game** (streaming
chunks by coordinate is a natural fit, and `WorldMapStore` + 
`MapDataProvider` + `ChunkStreamingProcessor` already work). But use
**flat JSON files as the interchange format** between the pipeline and
the game. The pipeline writes JSON; the game imports JSON into SQLite
on first load or via an import command.

This gives the best of both worlds: easy-to-inspect pipeline output,
fast runtime queries. The import step is small (iterate files, insert
rows).

---

## 5. Keep NPCs, quests, factions simple — JSON files

**Agreed.** The plans already mention this option. Concrete decision:

- **No** `npc_def` or `quest_def` database tables
- NPCs, quests, factions, dialogue trees stay in **content pack JSON
  files** alongside the pipeline output
- `entity_spawn` rows reference IDs (e.g. `npc:elder`) and the
  runtime resolves them via `ContentPackLoader` which reads the JSON
- This means NPC/quest/faction definitions are just `.json` files in
  the content pack, trivially editable and replaceable
- When these systems mature, they can optionally move to DB tables —
  but for now JSON is the single source of truth

Plans `01` and `03` will be simplified to remove the `npc_def` /
`quest_def` SQL tables and note JSON-only.

---

## 6. RoadClass should be an enum

**Already enums, but they shouldn't be two separate enums.**

Currently there are two:
- `RoadClass` in MapGen: `Motorway, Trunk, Primary, Secondary,
  Tertiary, Residential`
- `LinearFeatureType` in Core: `Path, DirtRoad, Road, Highway, Rail,
  River, Stream, Canal, Pipeline`

Having both means a mapping layer, which is exactly what we should
avoid. Instead, **unify into one enum in Core** that both the tool and
the game use:

```csharp
// In Oravey2.Core.World
public enum LinearFeatureType : byte
{
    // Roads (from OSM classification)
    Path = 0,
    Residential = 1,
    Tertiary = 2,
    Secondary = 3,
    Primary = 4,
    Trunk = 5,
    Motorway = 6,

    // Rail
    Rail = 10,

    // Water
    Stream = 20,
    River = 21,
    Canal = 22,

    // Infrastructure
    Pipeline = 30,
}
```

- Delete `RoadClass` from MapGen
- MapGen references `LinearFeatureType` directly (it already depends
  on `Oravey2.Core`)
- No mapping code needed — the pipeline writes the same enum value
  the game reads
- Extensible: new values slot into the numbered gaps

---

## 7. SurfaceVal should also be an enum

**Already is.** `SurfaceType` is a `byte`-backed enum:

```csharp
public enum SurfaceType : byte
{
    Dirt = 0, Asphalt = 1, Concrete = 2, Grass = 3,
    Sand = 4, Mud = 5, Rock = 6, Metal = 7
}
```

The `surfaceVal switch { 0 => …, 1 => … }` in
`02-pipeline-db-export.md` was pseudocode. The real implementation
should cast or map from the pipeline's integer surface values to
`SurfaceType`:

```csharp
var surface = (SurfaceType)layout.Surface[y][x];
```

Or if the pipeline uses a different numbering, an explicit mapping.
Either way, it's enum-to-enum — no magic integers.

---

## 8. Game-side import (not just export)

**Yes — the game needs an import command.** Two use cases:

| Use case | Flow |
|----------|------|
| **Dev/debug** | Pipeline exports JSON content pack → dev presses "Import" in debug menu → game reads JSON, inserts into `world.db` |
| **Shipping** | Content pack `.zip` ships with game → on first launch or DLC install, game auto-imports into `world.db` |

### Proposed `ContentPackImporter` (in `Oravey2.Core`)

```csharp
namespace Oravey2.Core.Data;

public sealed class ContentPackImporter
{
    private readonly WorldMapStore _store;

    public ImportResult Import(string contentPackPath)
    {
        // Same logic as ContentPackExporter but lives in Core,
        // not MapGen — so the game can call it without depending
        // on the pipeline tool
        ...
    }
}
```

Key difference from the exporter in `02-pipeline-db-export`:
- `ContentPackExporter` lives in **Oravey2.MapGen** (tool-side)
- `ContentPackImporter` lives in **Oravey2.Core** (game-side)
- Both produce the same DB rows; the importer just reads from a
  shipped content pack directory or zip

### Auto-import on startup

```csharp
// In GameBootstrapper
foreach (var pack in DiscoverNewContentPacks())
{
    var importer = new ContentPackImporter(_worldStore);
    importer.Import(pack.Path);
    MarkAsImported(pack);
}
```

`DiscoverNewContentPacks()` scans `ContentPacks/` for directories not
yet in `world_meta`. This enables a DLC / mod workflow where dropping
a folder into `ContentPacks/` automatically adds the region to the
game.

Plan `02` will be updated to define the shared format and add the
game-side importer alongside the tool-side exporter.
