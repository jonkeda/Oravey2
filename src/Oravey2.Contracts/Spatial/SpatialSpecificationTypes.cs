using System.Collections.Generic;
using System.Numerics;

namespace Oravey2.Contracts.Spatial;

/// <summary>
/// Real-world bounding box in lat/lon coordinates.
/// Shared across Core and MapGen via Contracts.
/// </summary>
public sealed record BoundingBox(
    double MinLat,
    double MaxLat,
    double MinLon,
    double MaxLon);

/// <summary>
/// A single building placement in real-world coordinates.
/// Shared across Core and MapGen via Contracts.
/// </summary>
public sealed record BuildingPlacement(
    string Name,
    double CenterLat,
    double CenterLon,
    double WidthMeters,
    double DepthMeters,
    double RotationDegrees,
    string AlignmentHint);

/// <summary>
/// A road segment in real-world coordinates.
/// Shared across Core and MapGen via Contracts.
/// </summary>
public sealed record RoadEdge(
    double FromLat,
    double FromLon,
    double ToLat,
    double ToLon);

/// <summary>
/// Road network: intersections + connections.
/// Shared across Core and MapGen via Contracts.
/// </summary>
public sealed record RoadNetwork(
    List<Vector2> Nodes,
    List<RoadEdge> Edges,
    float RoadWidthMeters);

/// <summary>
/// Spatial water body type enumeration.
/// Shared across Core and MapGen via Contracts.
/// </summary>
public enum SpatialWaterType
{
    River,
    Canal,
    Harbour,
    Lake
}

/// <summary>
/// Spatial water body polygon (for town design spatial specs).
/// Shared across Core and MapGen via Contracts.
/// </summary>
public sealed record SpatialWaterBody(
    string Name,
    List<Vector2> Polygon,
    SpatialWaterType Type);

/// <summary>
/// Complete spatial specification for a town.
/// Shared across Core and MapGen via Contracts.
/// </summary>
public sealed record TownSpatialSpecification(
    BoundingBox RealWorldBounds,
    Dictionary<string, BuildingPlacement> BuildingPlacements,
    RoadNetwork RoadNetwork,
    List<SpatialWaterBody> WaterBodies,
    string TerrainDescription);
