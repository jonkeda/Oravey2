using System.ComponentModel;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Schema class for the LLM town-design tool call.
/// Properties map to the JSON schema that AIFunctionFactory exposes to the model.
/// </summary>
internal sealed class LlmTownDesignEntry
{
    [Description("Array of landmark buildings")]
    public List<LandmarkBuilding> Landmarks { get; set; } = [];

    [Description("Array of key locations in the town")]
    public List<KeyLocation> KeyLocations { get; set; } = [];

    [Description("Layout style: grid, radial, organic, linear, clustered, or compound")]
    public string LayoutStyle { get; set; } = "organic";

    [Description("Array of environmental hazards (0 to 4)")]
    public List<EnvironmentalHazard> Hazards { get; set; } = [];

    [Description("Spatial layout specification with real-world coordinates")]
    public LlmTownSpatialSpec SpatialSpec { get; set; } = new();
}

internal sealed class LlmTownSpatialSpec
{
    [Description("Bounding box: { minLat, maxLat, minLon, maxLon }")]
    public LlmBoundingBoxDto RealWorldBounds { get; set; } = new();

    [Description("Building placements (landmarks + key locations) in real-world coordinates")]
    public List<LlmBuildingPlacementDto> BuildingPlacements { get; set; } = [];

    [Description("Main road network: intersections and connecting edges")]
    public LlmRoadNetworkDto RoadNetwork { get; set; } = new();

    [Description("Water bodies (rivers, canals, harbour) if present")]
    public List<LlmWaterBodyDto> WaterBodies { get; set; } = [];

    [Description("Terrain description: 'flat', 'hilly north', 'sloped south-west', etc.")]
    public string TerrainDescription { get; set; } = "flat";
}

internal sealed class LlmBoundingBoxDto
{
    [Description("Minimum latitude (south)")]
    public double MinLat { get; set; }

    [Description("Maximum latitude (north)")]
    public double MaxLat { get; set; }

    [Description("Minimum longitude (west)")]
    public double MinLon { get; set; }

    [Description("Maximum longitude (east)")]
    public double MaxLon { get; set; }
}

internal sealed class LlmBuildingPlacementDto
{
    [Description("Name of the building (must match a landmark or key location name)")]
    public string BuildingName { get; set; } = "";

    [Description("Center latitude")]
    public double CenterLat { get; set; }

    [Description("Center longitude")]
    public double CenterLon { get; set; }

    [Description("Building width in meters")]
    public double WidthMeters { get; set; }

    [Description("Building depth/length in meters")]
    public double DepthMeters { get; set; }

    [Description("Rotation in degrees (0–360) relative to nearest road")]
    public double RotationDegrees { get; set; }

    [Description("Alignment hint: 'on_main_road', 'square_corner', 'harbour_adjacent', 'residential_area', 'hillside'")]
    public string AlignmentHint { get; set; } = "on_main_road";

    // TODO: Notes is part of the LLM schema but currently unused in ToDomain() mapping
    [Description("Optional notes about this placement")]
    public string? Notes { get; set; }
}

internal sealed class LlmRoadNetworkDto
{
    [Description("Intersection points in (lat, lon) format")]
    public List<LlmCoordinateDto> Nodes { get; set; } = [];

    [Description("Road segments connecting the nodes")]
    public List<LlmRoadEdgeDto> Edges { get; set; } = [];

    [Description("Typical road width in meters")]
    public float RoadWidthMeters { get; set; } = 10.0f;
}

internal sealed class LlmCoordinateDto
{
    [Description("Latitude")]
    public double Lat { get; set; }

    [Description("Longitude")]
    public double Lon { get; set; }
}

internal sealed class LlmRoadEdgeDto
{
    [Description("Starting intersection index")]
    public int FromNodeIndex { get; set; }

    [Description("Ending intersection index")]
    public int ToNodeIndex { get; set; }

    // TODO: RoadType is part of the LLM schema but currently unused in ToDomain() mapping
    [Description("Road type: 'main', 'secondary', 'residential', 'alley'")]
    public string RoadType { get; set; } = "main";
}

internal sealed class LlmWaterBodyDto
{
    [Description("Name of the water body")]
    public string Name { get; set; } = "";

    [Description("Type: 'river', 'canal', 'harbour', 'lake'")]
    public string Type { get; set; } = "river";

    [Description("Polygon vertices as (lat, lon) pairs")]
    public List<LlmCoordinateDto> Polygon { get; set; } = [];
}


