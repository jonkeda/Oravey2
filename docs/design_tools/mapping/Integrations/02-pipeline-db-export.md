# 02 — Pipeline DB Export

## Goal

The MapGen pipeline tool (steps 01–10) currently writes JSON files
into a content pack directory. This refactor adds an export step that
writes the same data into `world.db` so the game can load it through
the unified `MapDataProvider` path.

## Prerequisite

`01-softcode-scenarios.md` — the game must load all scenarios from
`world.db` before this export is useful.

## Current pipeline output

```
{ContentPackPath}/
  manifest.json
  catalog.json
  data/curated-towns.json
  overworld/world.json, roads.json, water.json
  scenarios/{id}.json
  towns/{gameName}/
    design.json, layout.json, buildings.json, props.json, zones.json
  assets/meshes/*.glb, *.meta.json
```

The game cannot read this directly (see `pipeline-game-loading-review.md`).

## Target: `ContentPackExporter`

A new service in `Oravey2.MapGen` that reads the content pack files
and writes them into a `world.db`.

```csharp
namespace Oravey2.MapGen.Pipeline;

public sealed class ContentPackExporter
{
    /// Export a content pack directory into a WorldMapStore.
    public ExportResult Export(string contentPackPath, WorldMapStore store)
    {
        var result = new ExportResult();

        // 1. Read curated towns for metadata
        var curatedTowns = LoadCuratedTowns(contentPackPath);

        // 2. Create continent + region
        var continentId = store.InsertContinent(
            ReadRegionName(contentPackPath), null, 1, 1);
        var regionId = store.InsertRegion(
            continentId,
            ReadRegionName(contentPackPath),
            gridX: 0, gridY: 0,
            biome: "wasteland",
            baseHeight: 0,
            description: ReadRegionDescription(contentPackPath));

        // 3. Export each town as chunks
        foreach (var townDir in GetTownDirectories(contentPackPath))
        {
            ExportTown(townDir, regionId, store, result);
        }

        // 4. Export overworld data (roads, water)
        ExportOverworld(contentPackPath, regionId, store);

        // 5. Export POIs from curated towns
        ExportPois(curatedTowns, regionId, store);

        // 6. Store metadata
        store.GetOrSetMeta("content_pack_path", contentPackPath);
        store.GetOrSetMeta("exported_at", DateTime.UtcNow.ToString("O"));

        return result;
    }
}
```

### Town export: `layout.json` → chunks

Each town has a `layout.json` with a 2D height/surface array. Convert
to `TileData[,]` chunks:

```csharp
private void ExportTown(
    string townDir, long regionId, WorldMapStore store, ExportResult result)
{
    var townName = Path.GetFileName(townDir);

    // 1. Read layout → TileMapData
    var layout = ReadLayout(townDir);
    var tileMap = LayoutToTileMap(layout);

    // 2. Read buildings + props → building structs
    var buildings = ReadBuildings(townDir);
    var props = ReadProps(townDir);

    // 3. Apply building footprints to tileMap walkability
    foreach (var b in buildings)
        ApplyBuildingFootprint(tileMap, b);
    foreach (var p in props)
        ApplyPropFootprint(tileMap, p);

    // 4. Split into 16×16 chunks and write to DB
    var chunks = ChunkSplitter.Split(tileMap);
    foreach (var (cx, cy, tiles) in chunks)
    {
        var blob = TileDataSerializer.SerializeTileGrid(tiles);
        var chunkId = store.InsertChunk(regionId, cx, cy, blob,
            ChunkMode.Hybrid);

        // 5. Convert buildings in this chunk to entity_spawn rows
        foreach (var b in buildings.Where(b =>
            b.Placement.ChunkX == cx && b.Placement.ChunkY == cy))
        {
            store.InsertEntitySpawn(chunkId, new EntitySpawnInfo(
                PrefabId: $"building:{b.Id}",
                LocalX: b.Placement.LocalTileX,
                LocalZ: b.Placement.LocalTileY,
                RotationY: 0));
        }

        // 6. Convert props in this chunk
        foreach (var p in props.Where(p =>
            p.Placement.ChunkX == cx && p.Placement.ChunkY == cy))
        {
            store.InsertEntitySpawn(chunkId, new EntitySpawnInfo(
                PrefabId: $"prop:{p.Id}",
                LocalX: p.Placement.LocalTileX,
                LocalZ: p.Placement.LocalTileY,
                RotationY: p.Rotation));
        }
    }

    // 7. Read zones → POIs
    var zones = ReadZones(townDir);
    foreach (var zone in zones)
    {
        store.InsertPoi(regionId, zone.Name,
            zone.IsFastTravelTarget ? "fast_travel" : "zone",
            zone.ChunkStartX, zone.ChunkStartY,
            description: $"Biome {zone.Biome}, threat {zone.EnemyDifficultyTier}");
    }

    result.TownsExported++;
    result.ChunksWritten += chunks.Count;
}
```

### Layout → TileMapData conversion

The pipeline's `layout.json` stores a `surface` 2D array of integers.
Map each value to a `TileData`:

```csharp
private static TileMapData LayoutToTileMap(LayoutData layout)
{
    var map = new TileMapData(layout.Width, layout.Height);
    for (int y = 0; y < layout.Height; y++)
        for (int x = 0; x < layout.Width; x++)
        {
            var surfaceVal = layout.Surface[y][x];
            map.SetTileData(x, y, surfaceVal switch
            {
                0 => TileDataFactory.Ground(),
                1 => TileDataFactory.Road(),
                2 => TileDataFactory.Rubble(),
                3 => TileDataFactory.Water(),
                4 => TileDataFactory.Wall(),
                _ => TileDataFactory.Ground(),
            });
        }
    return map;
}
```

### Overworld export

```csharp
private void ExportOverworld(
    string contentPackPath, long regionId, WorldMapStore store)
{
    var overworldDir = Path.Combine(contentPackPath, "overworld");
    if (!Directory.Exists(overworldDir)) return;

    // Roads → linear features
    var overworldData = OverworldFiles.Load(overworldDir);
    foreach (var road in overworldData.Roads)
    {
        var type = road.RoadClass switch
        {
            "motorway" or "trunk" => LinearFeatureType.Highway,
            "primary" or "secondary" => LinearFeatureType.Road,
            "tertiary" or "residential" => LinearFeatureType.DirtRoad,
            _ => LinearFeatureType.Path,
        };
        var nodes = road.Nodes
            .Select(n => new LinearFeatureNode(n))
            .ToList();
        store.InsertLinearFeature(regionId,
            new LinearFeature(type, road.RoadClass, 2f, nodes));
    }

    // Water → linear features (rivers/canals)
    foreach (var water in overworldData.Water)
    {
        var type = water.WaterType switch
        {
            "river" => LinearFeatureType.River,
            "stream" => LinearFeatureType.Stream,
            "canal" => LinearFeatureType.Canal,
            _ => LinearFeatureType.River,
        };
        var nodes = water.Geometry
            .Select(n => new LinearFeatureNode(n))
            .ToList();
        store.InsertLinearFeature(regionId,
            new LinearFeature(type, water.WaterType, 3f, nodes));
    }

    // Town references → POIs
    foreach (var townRef in overworldData.World.Towns)
    {
        store.InsertPoi(regionId, townRef.GameName, "town",
            (int)(townRef.GameX / ChunkData.Size),
            (int)(townRef.GameY / ChunkData.Size),
            description: $"Role: {townRef.Role}, Threat: {townRef.ThreatLevel}");
    }
}
```

### Mesh asset path resolution

Buildings and props reference meshes as `meshes/primitives/cube.glb`.
When exported to world.db, the mesh path is stored in a new column or
in the `prefab_id` metadata. At runtime, the mesh loader resolves:

```
content pack root + "/" + meshAsset → absolute GLB path
```

Store the content pack root in `world_meta`:
```sql
INSERT INTO world_meta (key, value) VALUES
    ('content_pack_root', '/path/to/content/Oravey2.Apocalyptic.NL.NH');
```

The runtime mesh loader reads this to resolve GLB paths.

## Integration with AssemblyStepView

Add an **"Export to Game DB"** button to the step 10 UI:

```csharp
// In AssemblyStepViewModel
internal void RunExportToDb()
{
    var dbPath = Path.Combine(_state.ContentPackPath, "..", "world.db");
    using var store = new WorldMapStore(dbPath);
    var exporter = new ContentPackExporter();
    var result = exporter.Export(_state.ContentPackPath, store);
    StatusText = $"Exported {result.TownsExported} towns, " +
                 $"{result.ChunksWritten} chunks to {dbPath}";
}
```

## ExportResult

```csharp
public sealed class ExportResult
{
    public int TownsExported { get; set; }
    public int ChunksWritten { get; set; }
    public int PoisInserted { get; set; }
    public int LinearFeaturesInserted { get; set; }
    public List<string> Warnings { get; } = [];
}
```

## ChunkSplitter (shared utility)

Used by both `WorldDbSeeder` (01) and `ContentPackExporter` (02):

```csharp
namespace Oravey2.Core.Data;

public static class ChunkSplitter
{
    public static List<(int cx, int cy, TileData[,] tiles)>
        Split(TileMapData mapData)
    {
        var result = new List<(int, int, TileData[,])>();
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
                result.Add((cx, cy, tiles));
            }
        return result;
    }
}
```

## Testing strategy

1. **Round-trip**: Export Island Haven → load from DB → verify chunk
   count, building entity count, tile data matches
2. **Overworld**: Export roads/water → verify LinearFeature rows in DB
3. **POI**: Export curated towns → verify POI rows with correct
   grid coordinates
4. **Mesh path**: Verify `world_meta` stores content pack root, and
   entity `prefab_id` contains the mesh reference
5. **Empty pack**: Export a pack with no towns → no crash, zero
   chunks written

## Files changed

| File | Action |
|------|--------|
| `ContentPackExporter.cs` | **New** in `Oravey2.MapGen.Pipeline` |
| `ExportResult.cs` | **New** in `Oravey2.MapGen.Pipeline` |
| `ChunkSplitter.cs` | **New** in `Oravey2.Core.Data` (shared) |
| `AssemblyStepViewModel.cs` | **Modify** — add export command |
| `AssemblyStepView.xaml` | **Modify** — add Export button |
| `ContentPackAssemblerTests.cs` | **Extend** — export round-trip tests |
