using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Oravey2.Contracts.Spatial;

namespace Oravey2.MapGen.Generation;

/// Validates and builds TownSpatialSpecification from LLM output
public sealed class BuildSpatialSpecification
{
    internal static TownSpatialSpecification Build(
        LlmTownSpatialSpec spec,
        TownDesign design,
        float tileSizeMeters = 2.0f)
    {
        // Validate that all building names in spatial spec match design buildings
        var allBuildingNames = design.Landmarks
            .Select(l => l.Name)
            .Concat(design.KeyLocations.Select(k => k.Name))
            .ToHashSet();

        ValidateBuildingNames(spec.BuildingPlacements, allBuildingNames);

        // Convert DTO to domain types
        var realWorldBounds = spec.RealWorldBounds.ToDomain();
        var buildingPlacements = spec.BuildingPlacements
            .ToDictionary(
                p => p.BuildingName,
                p => p.ToDomain()
            );

        var roadNetwork = spec.RoadNetwork.ToDomain();
        var waterBodies = spec.WaterBodies.Select(w => w.ToDomain()).ToList();

        // Validate spatial constraints
        ValidateBoundingBox(realWorldBounds);
        ValidateBuildingFootprints(buildingPlacements);
        ValidateRoadNetwork(roadNetwork, realWorldBounds);
        ValidateWaterBodies(waterBodies, realWorldBounds);

        return new TownSpatialSpecification(
            RealWorldBounds: realWorldBounds,
            BuildingPlacements: buildingPlacements,
            RoadNetwork: roadNetwork,
            WaterBodies: waterBodies,
            TerrainDescription: spec.TerrainDescription ?? "mixed"
        );
    }

    private static void ValidateBuildingNames(
        List<LlmBuildingPlacementDto> placements,
        HashSet<string> validNames)
    {
        var invalidNames = placements
            .Select(p => p.BuildingName)
            .Where(name => !validNames.Contains(name))
            .ToList();

        if (invalidNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"Spatial spec contains buildings not in design: {string.Join(", ", invalidNames)}");
        }
    }

    private static void ValidateBoundingBox(BoundingBox bbox)
    {
        if (bbox.MinLat >= bbox.MaxLat)
            throw new InvalidOperationException($"Invalid latitude range: {bbox.MinLat} >= {bbox.MaxLat}");

        if (bbox.MinLon >= bbox.MaxLon)
            throw new InvalidOperationException($"Invalid longitude range: {bbox.MinLon} >= {bbox.MaxLon}");

        // Sanity check: not larger than a large country region (~2 degrees)
        if ((bbox.MaxLat - bbox.MinLat) > 2.0 || (bbox.MaxLon - bbox.MinLon) > 2.0)
            throw new InvalidOperationException("Bounding box is unreasonably large (>2° in any dimension)");
    }

    private static void ValidateBuildingFootprints(Dictionary<string, BuildingPlacement> placements)
    {
        foreach (var (name, placement) in placements)
        {
            if (placement.WidthMeters <= 0 || placement.DepthMeters <= 0)
                throw new InvalidOperationException(
                    $"Building '{name}' has invalid footprint: {placement.WidthMeters}×{placement.DepthMeters}m");

            // Sanity: no building larger than 500m in any dimension
            if (placement.WidthMeters > 500 || placement.DepthMeters > 500)
                throw new InvalidOperationException(
                    $"Building '{name}' is unreasonably large: {placement.WidthMeters}×{placement.DepthMeters}m");
        }
    }

    private static void ValidateRoadNetwork(RoadNetwork network, BoundingBox bounds)
    {
        if (network.Edges.Count == 0)
            throw new InvalidOperationException("Road network has no edges");

        if (network.RoadWidthMeters <= 0)
            throw new InvalidOperationException($"Invalid road width: {network.RoadWidthMeters}m");

        // Validate all road endpoints are within bounds
        foreach (var edge in network.Edges)
        {
            if (edge.FromLat < bounds.MinLat || edge.FromLat > bounds.MaxLat ||
                edge.FromLon < bounds.MinLon || edge.FromLon > bounds.MaxLon)
                throw new InvalidOperationException($"Road edge start point outside bounds: ({edge.FromLat}, {edge.FromLon})");

            if (edge.ToLat < bounds.MinLat || edge.ToLat > bounds.MaxLat ||
                edge.ToLon < bounds.MinLon || edge.ToLon > bounds.MaxLon)
                throw new InvalidOperationException($"Road edge end point outside bounds: ({edge.ToLat}, {edge.ToLon})");
        }
    }

    private static void ValidateWaterBodies(List<SpatialWaterBody> waters, BoundingBox bounds)
    {
        foreach (var water in waters)
        {
            if (water.Polygon.Count < 3)
                throw new InvalidOperationException($"Water body '{water.Name}' must have at least 3 vertices");

            foreach (var point in water.Polygon)
            {
                if (point.X < bounds.MinLat || point.X > bounds.MaxLat ||
                    point.Y < bounds.MinLon || point.Y > bounds.MaxLon)
                    throw new InvalidOperationException(
                        $"Water body '{water.Name}' has vertices outside bounds");
            }
        }
    }
}

/// Extension methods to convert LLM DTOs to domain types
internal static class LlmTodomainExtensions
{
    public static BoundingBox ToDomain(this LlmBoundingBoxDto dto)
        => new BoundingBox(dto.MinLat, dto.MaxLat, dto.MinLon, dto.MaxLon);

    public static BuildingPlacement ToDomain(this LlmBuildingPlacementDto dto)
        => new BuildingPlacement(
            Name: dto.BuildingName,
            CenterLat: dto.CenterLat,
            CenterLon: dto.CenterLon,
            WidthMeters: dto.WidthMeters,
            DepthMeters: dto.DepthMeters,
            RotationDegrees: dto.RotationDegrees,
            AlignmentHint: dto.AlignmentHint ?? ""
        );

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
        
        return new RoadNetwork(nodes, edges, dto.RoadWidthMeters);
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
        
        return new SpatialWaterBody(
            Name: dto.Name,
            Polygon: polygon,
            Type: waterType
        );
    }
}
