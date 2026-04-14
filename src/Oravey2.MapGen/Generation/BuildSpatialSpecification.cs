using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Oravey2.Contracts.Spatial;

namespace Oravey2.MapGen.Generation;

/// Validates and builds TownSpatialSpecification from LLM output.
/// Uses a lenient approach: repairs common LLM issues instead of throwing.
public sealed class BuildSpatialSpecification
{
    internal static TownSpatialSpecification Build(
        LlmTownSpatialSpec spec,
        TownDesign design,
        float tileSizeMeters = 2.0f)
    {
        var allBuildingNames = design.Landmarks
            .Select(l => l.Name)
            .Concat(design.KeyLocations.Select(k => k.Name))
            .ToHashSet();

        // Convert DTO to domain types
        var realWorldBounds = spec.RealWorldBounds.ToDomain();

        // Fix swapped lat/lon bounds
        if (realWorldBounds.MinLat > realWorldBounds.MaxLat)
            realWorldBounds = realWorldBounds with
            {
                MinLat = realWorldBounds.MaxLat,
                MaxLat = realWorldBounds.MinLat
            };
        if (realWorldBounds.MinLon > realWorldBounds.MaxLon)
            realWorldBounds = realWorldBounds with
            {
                MinLon = realWorldBounds.MaxLon,
                MaxLon = realWorldBounds.MinLon
            };

        // Fix zero-area bounding box (expand by ~500m around centre)
        if (realWorldBounds.MaxLat - realWorldBounds.MinLat < 0.0001)
        {
            double midLat = (realWorldBounds.MinLat + realWorldBounds.MaxLat) / 2;
            realWorldBounds = realWorldBounds with
            {
                MinLat = midLat - 0.0025,
                MaxLat = midLat + 0.0025
            };
        }
        if (realWorldBounds.MaxLon - realWorldBounds.MinLon < 0.0001)
        {
            double midLon = (realWorldBounds.MinLon + realWorldBounds.MaxLon) / 2;
            realWorldBounds = realWorldBounds with
            {
                MinLon = midLon - 0.004,
                MaxLon = midLon + 0.004
            };
        }

        // Filter building placements to only those matching design names,
        // and drop duplicates (keep first)
        var validPlacements = spec.BuildingPlacements
            .Where(p => allBuildingNames.Contains(p.BuildingName))
            .GroupBy(p => p.BuildingName)
            .ToDictionary(g => g.Key, g => FixBuildingPlacement(g.First().ToDomain()));

        // Build road network leniently
        var roadNetwork = spec.RoadNetwork.ToDomain();
        if (roadNetwork.RoadWidthMeters <= 0)
            roadNetwork.RoadWidthMeters = 6.0f;

        // Fix road edges: clamp to bounding box instead of rejecting
        var clampedEdges = roadNetwork.Edges
            .Select(e => ClampRoadEdge(e, realWorldBounds))
            .ToList();

        // If no valid edges, synthesize a road through the centre
        if (clampedEdges.Count == 0)
        {
            double midLat = (realWorldBounds.MinLat + realWorldBounds.MaxLat) / 2;
            clampedEdges.Add(new RoadEdge(
                midLat, realWorldBounds.MinLon,
                midLat, realWorldBounds.MaxLon));
        }
        roadNetwork = new RoadNetwork { Nodes = roadNetwork.Nodes, Edges = clampedEdges, RoadWidthMeters = roadNetwork.RoadWidthMeters };

        // Filter water bodies: drop invalid, clamp vertices
        var waterBodies = spec.WaterBodies
            .Select(w => w.ToDomain())
            .Where(w => w.Polygon.Count >= 3)
            .ToList();

        return new TownSpatialSpecification
        {
            RealWorldBounds = realWorldBounds,
            BuildingPlacements = validPlacements,
            RoadNetwork = roadNetwork,
            WaterBodies = waterBodies,
            TerrainDescription = spec.TerrainDescription ?? "mixed"
        };
    }

    private static BuildingPlacement FixBuildingPlacement(BuildingPlacement p)
    {
        double w = p.WidthMeters > 0 ? Math.Min(p.WidthMeters, 500) : 10;
        double d = p.DepthMeters > 0 ? Math.Min(p.DepthMeters, 500) : 10;
        p.WidthMeters = w;
        p.DepthMeters = d;
        return p;
    }

    private static RoadEdge ClampRoadEdge(RoadEdge edge, BoundingBox bounds)
    {
        return new RoadEdge(
            Math.Clamp(edge.FromLat, bounds.MinLat, bounds.MaxLat),
            Math.Clamp(edge.FromLon, bounds.MinLon, bounds.MaxLon),
            Math.Clamp(edge.ToLat, bounds.MinLat, bounds.MaxLat),
            Math.Clamp(edge.ToLon, bounds.MinLon, bounds.MaxLon));
    }
}

/// Extension methods to convert LLM DTOs to domain types
internal static class LlmToDomainExtensions
{
    public static BoundingBox ToDomain(this LlmBoundingBoxDto dto)
        => new BoundingBox(dto.MinLat, dto.MaxLat, dto.MinLon, dto.MaxLon);

    public static BuildingPlacement ToDomain(this LlmBuildingPlacementDto dto)
        => new BuildingPlacement
        {
            Name = dto.BuildingName,
            CenterLat = dto.CenterLat,
            CenterLon = dto.CenterLon,
            WidthMeters = dto.WidthMeters,
            DepthMeters = dto.DepthMeters,
            RotationDegrees = dto.RotationDegrees,
            AlignmentHint = dto.AlignmentHint ?? ""
        };

    public static RoadNetwork ToDomain(this LlmRoadNetworkDto dto)
    {
        var nodes = dto.Nodes.Select(n => new System.Numerics.Vector2((float)n.Lat, (float)n.Lon)).ToList();
        
        // Convert node indices to edges with lat/lon
        var edges = new List<RoadEdge>();
        foreach (var edgeDto in dto.Edges)
        {
            if (edgeDto.FromNodeIndex >= 0 && edgeDto.FromNodeIndex < dto.Nodes.Count &&
                edgeDto.ToNodeIndex >= 0 && edgeDto.ToNodeIndex < dto.Nodes.Count)
            {
                var fromNode = dto.Nodes[edgeDto.FromNodeIndex];
                var toNode = dto.Nodes[edgeDto.ToNodeIndex];
                edges.Add(new RoadEdge(fromNode.Lat, fromNode.Lon, toNode.Lat, toNode.Lon));
            }
        }
        
        return new RoadNetwork { Nodes = nodes, Edges = edges, RoadWidthMeters = dto.RoadWidthMeters };
    }

    public static SpatialWaterBody ToDomain(this LlmWaterBodyDto dto)
    {
        var polygon = dto.Polygon.Select(p => new System.Numerics.Vector2((float)p.Lat, (float)p.Lon)).ToList();
        
        // Parse water type from string
        var waterType = dto.Type?.ToLowerInvariant() switch
        {
            "river" => SpatialWaterType.River,
            "canal" => SpatialWaterType.Canal,
            "harbour" => SpatialWaterType.Harbour,
            "lake" => SpatialWaterType.Lake,
            _ => SpatialWaterType.River,
        };
        
        return new SpatialWaterBody
        {
            Name = dto.Name,
            Polygon = polygon,
            Type = waterType
        };
    }
}
