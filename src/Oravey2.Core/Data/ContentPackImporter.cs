using System.Numerics;
using System.Text.Json;
using Oravey2.Contracts;
using Oravey2.Contracts.ContentPack;
using Oravey2.Core.World;

namespace Oravey2.Core.Data;

public sealed class ContentPackImporter
{
    private readonly WorldMapStore _store;

    public ContentPackImporter(WorldMapStore store) => _store = store;

    public ImportResult Import(string contentPackPath)
    {
        var result = new ImportResult();

        // a. Read manifest
        var manifestPath = Path.Combine(contentPackPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            result.Warnings.Add("manifest.json not found; using defaults.");
        }

        var manifest = ReadJson<ManifestDto>(manifestPath);
        var regionName = manifest?.Name ?? "";

        result.RegionName = regionName;

        // b. Replace existing region if re-importing (upsert)
        var existing = _store.GetRegionByName(regionName);
        if (existing != null)
        {
            _store.DeleteRegion(existing.Id);
            result.Warnings.Add($"Replaced existing region '{regionName}'.");
        }

        // c. Create continent + region
        var continentId = _store.InsertContinent(regionName, null, 1, 1);
        var regionId = _store.InsertRegion(continentId, regionName, 0, 0);

        // c. Import towns — load curated town positions for chunk offsets
        var townsDir = Path.Combine(contentPackPath, "towns");
        var curatedTownsPath = Path.Combine(contentPackPath, "data", "curated-towns.json");
        var townOffsets = ComputeTownChunkOffsets(curatedTownsPath);

        if (Directory.Exists(townsDir))
        {
            foreach (var townDir in Directory.GetDirectories(townsDir))
            {
                var townName = Path.GetFileName(townDir);
                var offset = townOffsets.GetValueOrDefault(townName, (0, 0));
                ImportTown(townDir, regionId, offset.Item1, offset.Item2, result);
            }
        }

        // d. Import roads
        var roadsPath = Path.Combine(contentPackPath, "overworld", "roads.json");
        if (File.Exists(roadsPath))
        {
            ImportRoads(roadsPath, regionId, result);
        }

        // e. Import water
        var waterPath = Path.Combine(contentPackPath, "overworld", "water.json");
        if (File.Exists(waterPath))
        {
            ImportWater(waterPath, regionId, result);
        }

        // f. Import curated towns as POIs
        var curatedPath = Path.Combine(contentPackPath, "data", "curated-towns.json");
        if (File.Exists(curatedPath))
        {
            ImportCuratedTowns(curatedPath, regionId, result);
        }

        // g. Store content_pack_root in world_meta
        _store.GetOrSetMeta("content_pack_root", contentPackPath);

        return result;
    }

    private void ImportTown(string townDir, long regionId,
        int chunkOffsetX, int chunkOffsetY, ImportResult result)
    {
        var townName = Path.GetFileName(townDir);

        // Layout → TileMapData → ChunkSplitter → chunks
        var layoutPath = Path.Combine(townDir, "layout.json");
        if (!File.Exists(layoutPath))
        {
            result.Warnings.Add($"Town '{townName}': layout.json not found, skipping.");
            return;
        }

        var layout = ReadJson<LayoutDto>(layoutPath);
        if (layout is null || layout.Width <= 0 || layout.Height <= 0)
        {
            result.Warnings.Add($"Town '{townName}': invalid layout dimensions.");
            return;
        }

        var tileMap = new TileMapData(layout.Width, layout.Height);
        if (layout.Surface is not null)
        {
            for (int y = 0; y < layout.Height && y < layout.Surface.Length; y++)
            {
                var row = layout.Surface[y];
                var liquidRow = (layout.Liquid is not null && y < layout.Liquid.Length)
                    ? layout.Liquid[y] : null;
                for (int x = 0; x < layout.Width && x < row.Length; x++)
                {
                    var surface = (SurfaceType)(byte)row[x];
                    var liquid = (liquidRow is not null && x < liquidRow.Length)
                        ? (LiquidType)liquidRow[x] : LiquidType.None;
                    byte waterLevel = liquid != LiquidType.None ? (byte)2 : (byte)0;
                    var td = new TileData(surface, 1, waterLevel, 0, TileFlags.Walkable, 0, liquid);
                    tileMap.SetTileData(x, y, td);
                }
            }
        }

        // Split into chunks and insert (with region-level offsets)
        var chunks = ChunkSplitter.SplitAndSerialize(tileMap);
        var chunkIdMap = new Dictionary<(int, int), long>();
        foreach (var (cx, cy, blob) in chunks)
        {
            var chunkId = _store.InsertChunk(regionId, cx + chunkOffsetX, cy + chunkOffsetY, blob);
            chunkIdMap[(cx, cy)] = chunkId;
            result.ChunksWritten++;
        }

        // Buildings → entity spawns
        var buildingsPath = Path.Combine(townDir, "buildings.json");
        if (File.Exists(buildingsPath))
        {
            var buildings = ReadJsonListOrDefault<BuildingDto>(buildingsPath);
            foreach (var b in buildings)
            {
                // Encode mesh shape and size into PrefabId: building:id:shape:size
                var shape = ExtractMeshShape(b.MeshAsset);
                var spawn = new EntitySpawnInfo(
                    PrefabId: $"building:{b.Id}:{shape}:{b.Size}",
                    LocalX: b.Placement?.LocalTileX ?? 0,
                    LocalZ: b.Placement?.LocalTileY ?? 0,
                    RotationY: 0f,
                    Persistent: true);

                var key = (b.Placement?.ChunkX ?? 0, b.Placement?.ChunkY ?? 0);
                if (chunkIdMap.TryGetValue(key, out var cid))
                {
                    _store.InsertEntitySpawn(cid, spawn);
                    result.EntitySpawnsInserted++;
                }
                else
                {
                    result.Warnings.Add($"Town '{townName}': building '{b.Id}' references non-existent chunk ({key.Item1},{key.Item2}).");
                }
            }
        }

        // Props → entity spawns
        var propsPath = Path.Combine(townDir, "props.json");
        if (File.Exists(propsPath))
        {
            var props = ReadJsonListOrDefault<PropDto>(propsPath);
            foreach (var p in props)
            {
                var spawn = new EntitySpawnInfo(
                    PrefabId: $"prop:{p.Id}",
                    LocalX: p.Placement?.LocalTileX ?? 0,
                    LocalZ: p.Placement?.LocalTileY ?? 0,
                    RotationY: p.Rotation,
                    Persistent: false);

                var key = (p.Placement?.ChunkX ?? 0, p.Placement?.ChunkY ?? 0);
                if (chunkIdMap.TryGetValue(key, out var cid))
                {
                    _store.InsertEntitySpawn(cid, spawn);
                    result.EntitySpawnsInserted++;
                }
                else
                {
                    result.Warnings.Add($"Town '{townName}': prop '{p.Id}' references non-existent chunk ({key.Item1},{key.Item2}).");
                }
            }
        }

        // Zones → POIs
        var zonesPath = Path.Combine(townDir, "zones.json");
        if (File.Exists(zonesPath))
        {
            var zones = ReadJsonListOrDefault<ZoneDto>(zonesPath);
            foreach (var z in zones)
            {
                _store.InsertPoi(regionId, z.Name, "zone", z.ChunkStartX, z.ChunkStartY,
                    description: z.Id);
                result.PoisInserted++;
            }
        }

        result.TownsImported++;
    }

    private void ImportRoads(string roadsPath, long regionId, ImportResult result)
    {
        var roads = ReadJsonListOrDefault<RoadDto>(roadsPath);
        foreach (var r in roads)
        {
            var featureType = r.RoadClass?.ToLowerInvariant() switch
            {
                "motorway" => LinearFeatureType.Motorway,
                "trunk" => LinearFeatureType.Trunk,
                "primary" => LinearFeatureType.Primary,
                "secondary" => LinearFeatureType.Secondary,
                "tertiary" => LinearFeatureType.Tertiary,
                "residential" => LinearFeatureType.Residential,
                "path" => LinearFeatureType.Path,
                _ => LinearFeatureType.Residential,
            };

            var nodes = (r.Nodes ?? [])
                .Where(n => n.Length >= 2)
                .Select(n => new LinearFeatureNode(new Vector2(n[0], n[1])))
                .ToList();

            if (nodes.Count < 2) continue;

            var feature = new LinearFeature(featureType, r.RoadClass ?? "residential", 1f, nodes);
            _store.InsertLinearFeature(regionId, feature);
            result.LinearFeaturesInserted++;
        }
    }

    private void ImportWater(string waterPath, long regionId, ImportResult result)
    {
        var waters = ReadJsonListOrDefault<WaterDto>(waterPath);
        foreach (var w in waters)
        {
            var featureType = w.WaterType?.ToLowerInvariant() switch
            {
                "river" => LinearFeatureType.River,
                "stream" => LinearFeatureType.Stream,
                "canal" => LinearFeatureType.Canal,
                _ => LinearFeatureType.Stream,
            };

            var nodes = (w.Geometry ?? [])
                .Where(n => n.Length >= 2)
                .Select(n => new LinearFeatureNode(new Vector2(n[0], n[1])))
                .ToList();

            if (nodes.Count < 2) continue;

            var feature = new LinearFeature(featureType, w.WaterType ?? "stream", 2f, nodes);
            _store.InsertLinearFeature(regionId, feature);
            result.LinearFeaturesInserted++;
        }
    }

    /// <summary>
    /// Compute per-town chunk offsets from curated town lat/lon positions.
    /// Each town's local (0,0) chunk maps to a unique region-level position.
    /// </summary>
    private static Dictionary<string, (int ChunkX, int ChunkY)> ComputeTownChunkOffsets(
        string curatedPath)
    {
        var offsets = new Dictionary<string, (int, int)>();
        var file = ReadJson<CuratedTownsFile>(curatedPath);
        var towns = file?.Towns;
        if (towns is null || towns.Count == 0)
            return offsets;

        // Find region bounds from all town positions
        double minLat = towns.Min(t => t.Latitude);
        double minLon = towns.Min(t => t.Longitude);
        double avgLat = towns.Average(t => t.Latitude);

        // Geo constants
        const double metersPerDegreeLat = 111320.0;
        double metersPerDegreeLon = metersPerDegreeLat * Math.Cos(avgLat * Math.PI / 180.0);
        const double tileSizeMeters = 2.0;
        const int chunkSize = 16;

        foreach (var t in towns)
        {
            double dLat = t.Latitude - minLat;
            double dLon = t.Longitude - minLon;
            int tileX = (int)(dLon * metersPerDegreeLon / tileSizeMeters);
            int tileY = (int)(dLat * metersPerDegreeLat / tileSizeMeters);
            int chunkX = tileX / chunkSize;
            int chunkY = tileY / chunkSize;
            offsets[t.GameName] = (chunkX, chunkY);
        }

        return offsets;
    }

    private void ImportCuratedTowns(string curatedPath, long regionId, ImportResult result)
    {
        var file = ReadJson<CuratedTownsFile>(curatedPath);
        foreach (var t in file?.Towns ?? [])
        {
            _store.InsertPoi(regionId, t.GameName, "town",
                (int)t.Longitude, (int)t.Latitude,
                description: t.RealName, icon: t.Size);
            result.PoisInserted++;
        }
    }

    private static T? ReadJson<T>(string path)
    {
        if (!File.Exists(path)) return default;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, ContentPackSerializer.ReadOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static List<T> ReadJsonListOrDefault<T>(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<T>>(json, ContentPackSerializer.ReadOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ExtractMeshShape(string meshAsset)
    {
        if (string.IsNullOrEmpty(meshAsset)) return "cube";
        var fileName = Path.GetFileNameWithoutExtension(meshAsset);
        return fileName ?? "cube";
    }

    private sealed class CuratedTownsFile
    {
        public List<CuratedTownDto>? Towns { get; set; }
    }
}

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
