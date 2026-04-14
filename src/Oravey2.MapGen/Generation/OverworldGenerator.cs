using System.Numerics;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Builds overworld data (world info, inter-town road network, water features)
/// from RegionTemplate data filtered to curated towns.
/// </summary>
public sealed class OverworldGenerator
{
    private const int TilesPerChunk = 16;

    /// <summary>
    /// Proximity threshold in game-space units; a road segment is included
    /// when at least one node falls within this distance of a curated town.
    /// </summary>
    private const float RoadProximityThreshold = 0.15f;

    public OverworldResult Generate(
        RegionTemplate region,
        IReadOnlyList<CuratedTown> towns,
        string regionName)
    {
        var townRefs = towns.Select(t => new OverworldTownRef
        {
            GameName = t.GameName, RealName = t.RealName,
            GameX = t.GamePosition.X, GameY = t.GamePosition.Y,
            Description = t.Description, Size = t.Size.ToString(), Inhabitants = t.Inhabitants, Destruction = t.Destruction.ToString(),
        }).ToList();

        // Compute world bounds from town positions
        var (chunksWide, chunksHigh) = ComputeWorldBounds(towns);

        // Player starts at the largest town (most inhabitants)
        var startTown = towns.MaxBy(t => t.Inhabitants) ?? towns[0];
        var playerStart = GamePosToPlacement(startTown.GamePosition, chunksWide, chunksHigh);

        var world = new OverworldInfo
        {
            Name = regionName,
            Description = $"Overworld map for {regionName}",
            Source = region.Name,
            ChunksWide = chunksWide, ChunksHigh = chunksHigh,
            TileSize = 1,
            PlayerStart = playerStart,
            Towns = townRefs,
        };

        var roads = FilterRoads(region.Roads, towns);
        var water = FilterWater(region.WaterBodies, towns);

        return new OverworldResult { World = world, Roads = roads, Water = water };
    }

    internal static (int ChunksWide, int ChunksHigh) ComputeWorldBounds(
        IReadOnlyList<CuratedTown> towns)
    {
        if (towns.Count == 0) return (1, 1);

        var maxX = towns.Max(t => t.GamePosition.X);
        var maxY = towns.Max(t => t.GamePosition.Y);

        // Each chunk is TilesPerChunk tiles; ensure all towns fit
        var chunksWide = Math.Max(1, (int)Math.Ceiling(maxX + 1));
        var chunksHigh = Math.Max(1, (int)Math.Ceiling(maxY + 1));
        return (chunksWide, chunksHigh);
    }

    internal static TilePlacement GamePosToPlacement(
        Vector2 gamePos, int chunksWide, int chunksHigh)
    {
        var cx = Math.Clamp((int)gamePos.X, 0, Math.Max(0, chunksWide - 1));
        var cy = Math.Clamp((int)gamePos.Y, 0, Math.Max(0, chunksHigh - 1));
        var lx = Math.Clamp((int)((gamePos.X - cx) * TilesPerChunk), 0, TilesPerChunk - 1);
        var ly = Math.Clamp((int)((gamePos.Y - cy) * TilesPerChunk), 0, TilesPerChunk - 1);
        return new TilePlacement(cx, cy, lx, ly);
    }

    internal static List<OverworldRoad> FilterRoads(
        IReadOnlyList<RoadSegment> allRoads,
        IReadOnlyList<CuratedTown> towns)
    {
        var roads = new List<OverworldRoad>();
        var id = 0;

        foreach (var seg in allRoads)
        {
            // Include road if any node is near a curated town
            string? nearestFrom = null;
            string? nearestTo = null;

            foreach (var node in seg.Nodes)
            {
                foreach (var town in towns)
                {
                    var dist = Vector2.Distance(node, town.GamePosition);
                    if (dist < RoadProximityThreshold)
                    {
                        if (nearestFrom is null)
                            nearestFrom = town.GameName;
                        else if (nearestFrom != town.GameName)
                            nearestTo = town.GameName;
                    }
                }
            }

            if (nearestFrom is not null)
            {
                roads.Add(new OverworldRoad
                {
                    Id = $"road_{id++}",
                    RoadClass = seg.RoadClass.ToString(),
                    Nodes = seg.Nodes,
                    FromTown = nearestFrom,
                    ToTown = nearestTo,
                });
            }
        }

        return roads;
    }

    internal static List<OverworldWater> FilterWater(
        IReadOnlyList<WaterBody> allWater,
        IReadOnlyList<CuratedTown> towns)
    {
        var water = new List<OverworldWater>();
        var id = 0;

        // Sea and large features are always included; rivers/canals only if
        // near a town
        foreach (var body in allWater)
        {
            if (body.Type == WaterType.Sea)
            {
                water.Add(new OverworldWater
                {
                    Id = $"water_{id++}", WaterType = body.Type.ToString(), Geometry = body.Geometry,
                });
                continue;
            }

            // Include if any vertex is near a curated town
            var near = body.Geometry.Any(v =>
                towns.Any(t => Vector2.Distance(v, t.GamePosition) < RoadProximityThreshold * 3));
            if (near)
            {
                water.Add(new OverworldWater
                {
                    Id = $"water_{id++}", WaterType = body.Type.ToString(), Geometry = body.Geometry,
                });
            }
        }

        return water;
    }
}
