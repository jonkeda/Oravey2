# 04 — Region Travel

## Goal

Players can start in any region and travel between regions at
runtime, following the flow from `world-creation-flow.md`:

```
Start Menu → World Template Picker → Town Picker → Generation → Play
```

This refactor adds:
- A **region picker** in the New Game UI
- **Inter-region travel** during gameplay
- A **multi-region world.db** that can hold multiple imported regions

## Prerequisites

- `01-softcode-scenarios.md` — built-in scenarios are regions in DB
- `02-pipeline-db-export.md` — pipeline regions are in DB
- `03-unified-loader.md` — `RegionLoader` handles loading/unloading

## World creation flow (from design)

The `world-creation-flow.md` design describes:

```
MainMenu
  → "New Game"
    → WorldTemplatePicker: list available world templates
      → TownPicker: player picks starting town within the template
        → WorldGenerationProgress: generate chunks into world.db
          → Load "generated" scenario
```

After these refactors, the flow becomes:

```
MainMenu
  → "New Game"
    → RegionPicker: list regions from world.db
      (+ "Import Content Pack" button to run pipeline export)
      → TownPicker: pick starting town within region if multiple
        → RegionLoader.LoadRegion(regionName)
```

For "Continue Game", load from save state:

```
MainMenu
  → "Continue"
    → Load save file
    → Read last region name from save
    → RegionLoader.LoadRegion(lastRegion)
    → Restore player position from save
```

## Multi-region world.db structure

The existing schema already supports multiple continents and regions:

```
world_meta
  └── continent (1+)
        └── region (1+ per continent)
              ├── chunk (N per region)
              │     └── entity_spawn (N per chunk)
              ├── linear_feature (roads, rivers)
              ├── poi (towns, zone exits, landmarks)
              ├── terrain_modifier
              └── location_description
```

Each pipeline run exports **one region**. Multiple pipeline runs
accumulate regions in the same `world.db`. The seeder provides
built-in regions.

### Region metadata

The `region` table needs additional columns (or use the existing
`description` column with a JSON blob):

| Column | Type | Purpose |
|--------|------|---------|
| `display_name` | TEXT | User-visible name ("Noord-Holland") |
| `biome` | TEXT | Default biome ("wasteland", "urban") |
| `base_height` | INTEGER | Default terrain height |
| `spawn_x` | INTEGER | Default player spawn chunk X |
| `spawn_y` | INTEGER | Default player spawn chunk Y |
| `difficulty` | INTEGER | Region difficulty 1–5 |
| `tags` | TEXT | Comma-separated tags for filtering |
| `icon_path` | TEXT | Optional preview image path |

## Region picker UI

### ScenarioSelectorScript refactor

```csharp
public sealed class RegionSelectorScript : SyncScript
{
    private RegionInfo[] _regions;
    private int _selectedIndex;

    public override void Start()
    {
        _regions = _worldStore.GetAllRegions().ToArray();
    }

    public override void Update()
    {
        // Navigate with up/down
        if (Input.IsKeyPressed(Keys.Down))
            _selectedIndex = Math.Min(_selectedIndex + 1, _regions.Length - 1);
        if (Input.IsKeyPressed(Keys.Up))
            _selectedIndex = Math.Max(_selectedIndex - 1, 0);

        // Confirm selection
        if (Input.IsKeyPressed(Keys.Enter))
        {
            var region = _regions[_selectedIndex];
            GameBootstrapper.Instance.StartScenario(region.Name);
        }

        // Draw region list with details
        DrawRegionList();
    }

    private void DrawRegionList()
    {
        foreach (var (region, i) in _regions.Select((r, i) => (r, i)))
        {
            var selected = i == _selectedIndex;
            var text = selected
                ? $"► {region.DisplayName}  [{region.Biome}]  ★{region.Difficulty}"
                : $"  {region.DisplayName}  [{region.Biome}]  ★{region.Difficulty}";
            // Draw via HUD text system
            HudRenderer.DrawText(text, x: 100, y: 200 + i * 30,
                color: selected ? Color.Yellow : Color.White);
        }

        // Import button
        if (Input.IsKeyPressed(Keys.I))
            ShowImportDialog();
    }
}
```

### Import content pack dialog

Players (or developers during debug) can import a content pack:

```csharp
private void ShowImportDialog()
{
    // Show file picker for content pack directory
    var packPath = FileDialogs.SelectFolder("Select Content Pack");
    if (packPath == null) return;

    var exporter = new ContentPackExporter();
    var result = exporter.Export(packPath, _worldStore);

    // Refresh region list
    _regions = _worldStore.GetAllRegions().ToArray();

    HudRenderer.ShowNotification(
        $"Imported region: {result.TownsExported} towns, " +
        $"{result.ChunksWritten} chunks");
}
```

## Inter-region travel

### Fast travel between regions

When a player discovers a `zone_exit` POI whose target is a different
region (not a zone within the same region):

```csharp
public sealed class RegionExitTriggerScript : SyncScript
{
    public string TargetRegionName { get; set; }
    public string TargetSpawnPoi { get; set; } // Optional: spawn at a specific POI

    public override void Update()
    {
        if (PlayerInRange())
        {
            // Save current position in current region
            SaveState.SavePlayerPosition(
                _currentRegion.Name,
                Player.Transform.Position);

            // Transition
            ZoneManager.TransitionToRegion(TargetRegionName, TargetSpawnPoi);
        }
    }
}
```

### ZoneManager region transit

```csharp
public void TransitionToRegion(string regionName, string spawnPoi = null)
{
    // Fade out
    StartCoroutine(FadeTransition(async () =>
    {
        _regionLoader.UnloadCurrentRegion();

        // Determine spawn position
        Vector3? spawnPos = null;
        if (spawnPoi != null)
        {
            var poi = _worldStore.GetPoi(regionName, spawnPoi);
            spawnPos = new Vector3(
                poi.ChunkX * 16 + 8, 0f,
                poi.ChunkY * 16 + 8);
        }

        _regionLoader.LoadRegion(regionName, spawnPos);
    }));
}
```

### Region adjacency

For open-world movement between regions (without explicit exits),
define adjacency in the database:

```sql
CREATE TABLE region_adjacency (
    region_a_id INTEGER NOT NULL REFERENCES region(id),
    region_b_id INTEGER NOT NULL REFERENCES region(id),
    direction TEXT NOT NULL,  -- "north", "east", etc.
    border_chunk_a_x INTEGER,
    border_chunk_a_y INTEGER,
    border_chunk_b_x INTEGER,
    border_chunk_b_y INTEGER,
    PRIMARY KEY (region_a_id, region_b_id)
);
```

When the player walks past the edge of the current region's chunk
grid, `ChunkStreamingProcessor` can detect the boundary and trigger
a region transition:

```csharp
// In ChunkStreamingProcessor
if (newChunkX > maxRegionChunkX)
{
    var adj = _worldStore.GetAdjacentRegion(
        _currentRegionId, "east");
    if (adj != null)
        ZoneManager.TransitionToRegion(
            adj.RegionName, entryChunkX: 0, entryChunkY: newChunkY);
}
```

This is **future work** — initial implementation uses explicit
`zone_exit` POIs.

## Save state across regions

### Current save schema (save.db)

```
save_meta       — one row, world_db path
party           — player stats, inventory
chunk_state     — per-chunk tile overrides (region-scoped)
fog_of_war      — per-chunk visibility (region-scoped)
discovered_poi  — per-poi (region-scoped)
fast_travel_unlock — per-poi
quest_state     — global
map_marker      — per-region
```

Add `current_region` to `save_meta`:

```sql
ALTER TABLE save_meta ADD COLUMN current_region TEXT;
```

When saving:
```csharp
_saveStore.SetMeta("current_region", _currentRegion.Name);
```

When loading:
```csharp
var lastRegion = _saveStore.GetMeta("current_region") ?? "town";
_regionLoader.LoadRegion(lastRegion);
```

### Per-region chunk_state

The `chunk_state` table already has `chunk_id` which is scoped to a
region. No schema change needed — just ensure that when switching
regions, the save layer queries the correct region's chunks.

## Debug scenario loader integration

Per the user's requirement, the pipeline tool should create
**Region Scenarios** loadable by the debug scenario selector.

After running the pipeline and pressing "Export to Game DB":

1. The region appears in `world.db` with all metadata
2. `RegionSelectorScript.DiscoverScenarios()` finds it
3. Developer selects it from the debug list
4. `RegionLoader` loads it normally

No special "debug scenario" format needed — every region in the
database **is** a loadable scenario.

The pipeline's `scenario/*.json` files are consumed by the exporter
to set the region's `spawn_x/spawn_y`, `difficulty`, and `tags`.

## Implementation phases

### Phase A: Single-region play (MVP)

1. Implement `RegionSelectorScript` reading from DB
2. "New Game" shows all regions in DB
3. Player picks one → `RegionLoader` loads it
4. No inter-region travel yet

### Phase B: Zone exits between regions

1. `zone_exit:{region}` entity spawns trigger region transitions
2. `ZoneManager.TransitionToRegion()` saves position, unloads,
   loads target
3. Save state tracks `current_region`

### Phase C: Region import from pipeline

1. "Import Content Pack" in region selector
2. Runs `ContentPackExporter.Export()` into active `world.db`
3. New region appears immediately in list

### Phase D: Open-world region adjacency (future)

1. `region_adjacency` table
2. `ChunkStreamingProcessor` detects boundary crossings
3. Seamless transitions without explicit exits

## Files changed

| File | Action |
|------|--------|
| `RegionSelectorScript.cs` | **New** (replaces `ScenarioSelectorScript.cs`) |
| `RegionExitTriggerScript.cs` | **New** |
| `ZoneManager.cs` | **Refactor** — region-aware transitions |
| `SaveStateStore.cs` | **Extend** — per-region queries |
| `WorldDbSchema.sql` | **Extend** — region columns, `region_adjacency` |
| `SaveDbSchema.sql` | **Extend** — `current_region` in save_meta |
| `ChunkStreamingProcessor.cs` | **Future** — boundary detection |
| `RegionLoader.cs` | **Extend** — spawn POI support |
| `GameBootstrapper.cs` | **Extend** — save/load region |
