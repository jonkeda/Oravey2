using System.Numerics;

namespace Oravey2.MapGen.Generation;

/// Real-world bounding box in lat/lon coordinates
public sealed record BoundingBox(
    double MinLat,
    double MaxLat,
    double MinLon,
    double MaxLon
);

/// A single building placement in real-world coordinates
public sealed record BuildingPlacement(
    string Name,
    double CenterLat,
    double CenterLon,
    double WidthMeters,
    double DepthMeters,
    double RotationDegrees,
    string AlignmentHint  // "on_main_road", "square_corner", "harbour_adjacent", "residential"
);

/// A road segment in real-world coordinates
public sealed record RoadEdge(
    double FromLat,
    double FromLon,
    double ToLat,
    double ToLon
);

/// Road network: intersections + connections
public sealed record RoadNetwork(
    List<Vector2> Nodes,           // Intersections as (lat, lon)
    List<RoadEdge> Edges,          // Road segments connecting nodes
    float RoadWidthMeters          // Typical road width
);

/// Spatial water body type enumeration
public enum SpatialWaterType
{
    River,
    Canal,
    Harbour,
    Lake
}

/// Spatial water body polygon (for town design spatial specs)
public sealed record SpatialWaterBody(
    string Name,
    List<Vector2> Polygon,         // Vertices as (lat, lon)
    SpatialWaterType Type
);

/// Complete spatial specification for a town
public sealed record TownSpatialSpecification(
    BoundingBox RealWorldBounds,
    Dictionary<string, BuildingPlacement> BuildingPlacements,
    RoadNetwork RoadNetwork,
    List<SpatialWaterBody> WaterBodies,
    string TerrainDescription  // "flat", "hilly north", "sloped south-west"
);
