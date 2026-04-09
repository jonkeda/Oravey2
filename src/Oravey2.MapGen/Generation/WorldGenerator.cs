using System.Numerics;
using System.Text.Json;
using Oravey2.Core.Data;
using Oravey2.Core.World;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

public sealed class WorldGenerator
{
    private readonly Func<string, CancellationToken, Task<string>> _llmCall;
    private readonly Action<string>? _onProgress;

    public WorldGenerator(
        Func<string, CancellationToken, Task<string>> llmCall,
        Action<string>? onProgress = null)
    {
        _llmCall = llmCall;
        _onProgress = onProgress;
    }

    public async Task GenerateAsync(
        RegionTemplateFile template,
        int seed,
        WorldMapStore store,
        CancellationToken ct = default)
    {
        _onProgress?.Invoke("Curating towns…");

        // Phase 1: LLM curation
        var curator = new TownCurator(_llmCall);
        var curatedRegions = new List<CuratedRegion>();
        foreach (var region in template.Regions)
        {
            ct.ThrowIfCancellationRequested();
            var curated = await curator.CurateAsync(region, seed, ct);
            curatedRegions.Add(curated);
        }

        var plan = new CuratedWorldPlan(template.Name, seed, curatedRegions);
        store.GetOrSetMeta("curated_plan", JsonSerializer.Serialize(plan));
        store.GetOrSetMeta("world_seed", seed.ToString());

        // Phase 2: Level 3 Continent
        _onProgress?.Invoke("Generating continent…");
        var continentGen = new ContinentGenerator();
        var continentData = continentGen.Generate(template);
        long continentId = store.InsertContinent(
            continentData.Name, null, continentData.GridWidth, continentData.GridHeight);

        // Phase 3: Level 2 Regions
        _onProgress?.Invoke("Generating regions…");
        var regionGen = new RegionGenerator();
        var roadSelector = new RoadSelector();
        var riverGen = new RiverGenerator();

        for (int ri = 0; ri < template.Regions.Count; ri++)
        {
            ct.ThrowIfCancellationRequested();
            var regionTemplate = template.Regions[ri];
            var curatedRegion = curatedRegions[ri];

            var regionData = regionGen.Generate(regionTemplate, continentData, curatedRegion, seed);
            long regionId = store.InsertRegion(
                continentId, regionData.Name, ri, 0,
                biome: "wasteland",
                baseHeight: 0);

            // Roads
            var selectedRoads = roadSelector.Select(regionTemplate, curatedRegion);
            var rivers = riverGen.Generate(regionTemplate);

            foreach (var road in selectedRoads)
            {
                var nodes = road.Nodes.Select(n => new LinearFeatureNode(n)).ToList();
                store.InsertLinearFeature(regionId, new LinearFeature(road.Type, "default", road.Width, nodes));
            }

            foreach (var river in rivers)
            {
                var nodes = river.Nodes.Select(n => new LinearFeatureNode(n)).ToList();
                store.InsertLinearFeature(regionId, new LinearFeature(river.Type, "default", river.Width, nodes));
            }

            // POIs for curated towns
            foreach (var town in curatedRegion.Towns)
            {
                store.InsertPoi(regionId, town.GameName, "town",
                    (int)(town.GamePosition.X / ChunkData.Size),
                    (int)(town.GamePosition.Y / ChunkData.Size),
                    town.Description);
            }

            // Phase 4: Level 1 Chunks — starting area
            _onProgress?.Invoke($"Building terrain for {regionData.Name}…");
            await GenerateChunksAsync(store, regionId, regionTemplate, curatedRegion, seed, ct);
        }

        _onProgress?.Invoke("World generation complete.");
    }

    private Task GenerateChunksAsync(
        WorldMapStore store,
        long regionId,
        RegionTemplate regionTemplate,
        CuratedRegion curatedRegion,
        int seed,
        CancellationToken ct)
    {
        var wildernessGen = new WildernessChunkGenerator();
        var townGen = new TownChunkGenerator();

        // Generate chunks in a grid around the origin (starting area)
        const int radius = 5; // 11×11 chunk area
        var townLookup = curatedRegion.Towns
            .Where(t => regionTemplate.Towns.Any(rt => rt.Name.Equals(t.RealName, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(
                t => t.RealName,
                t => (Town: t, Entry: regionTemplate.Towns.First(rt =>
                    rt.Name.Equals(t.RealName, StringComparison.OrdinalIgnoreCase))),
                StringComparer.OrdinalIgnoreCase);

        for (int cx = -radius; cx <= radius; cx++)
        {
            for (int cy = -radius; cy <= radius; cy++)
            {
                ct.ThrowIfCancellationRequested();

                // Check if this chunk is inside a town boundary
                CuratedTown? chunkTown = null;
                TownEntry? chunkTownEntry = null;
                float chunkCenterX = cx * ChunkData.Size + ChunkData.Size / 2f;
                float chunkCenterZ = cy * ChunkData.Size + ChunkData.Size / 2f;

                foreach (var kvp in townLookup)
                {
                    var entry = kvp.Value.Entry;
                    if (entry.BoundaryPolygon != null &&
                        ContinentGenerator.PointInPolygon(
                            new Vector2(chunkCenterX, chunkCenterZ), entry.BoundaryPolygon))
                    {
                        chunkTown = kvp.Value.Town;
                        chunkTownEntry = entry;
                        break;
                    }
                }

                ChunkResult chunk;
                if (chunkTown != null && chunkTownEntry != null)
                {
                    chunk = townGen.Generate(cx, cy, chunkTown, chunkTownEntry, regionTemplate, seed);
                }
                else
                {
                    chunk = wildernessGen.Generate(cx, cy, seed, regionTemplate);
                }

                var tileBlob = TileDataSerializer.SerializeTileGrid(chunk.Tiles.TileDataGrid);
                long chunkId = store.InsertChunk(regionId, cx, cy, tileBlob, chunk.Mode);

                foreach (var entity in chunk.Entities)
                {
                    store.InsertEntitySpawn(chunkId, entity);
                }
            }
        }

        return Task.CompletedTask;
    }
}
