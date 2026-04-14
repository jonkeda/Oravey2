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
public sealed class BuildingPlacement
{
    public string Name { get; set; } = "";
    public double CenterLat { get; set; }
    public double CenterLon { get; set; }
    public double WidthMeters { get; set; }
    public double DepthMeters { get; set; }
    public double RotationDegrees { get; set; }
    public string AlignmentHint { get; set; } = "";
}

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
public sealed class RoadNetwork
{
    public List<Vector2> Nodes { get; set; } = [];
    public List<RoadEdge> Edges { get; set; } = [];
    public float RoadWidthMeters { get; set; }
}

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
public sealed class SpatialWaterBody
{
    public string Name { get; set; } = "";
    public List<Vector2> Polygon { get; set; } = [];
    public SpatialWaterType Type { get; set; }
}

/// <summary>
/// Complete spatial specification for a town.
/// Shared across Core and MapGen via Contracts.
/// </summary>
public sealed class TownSpatialSpecification
{
    public BoundingBox RealWorldBounds { get; set; } = new(0, 0, 0, 0);
    public Dictionary<string, BuildingPlacement> BuildingPlacements { get; set; } = new();
    public RoadNetwork RoadNetwork { get; set; } = new();
    public List<SpatialWaterBody> WaterBodies { get; set; } = [];
    public string TerrainDescription { get; set; } = "";
}
