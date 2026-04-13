using System.ComponentModel;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Schema class for the LLM town-design tool call.
/// Properties map to the JSON schema that AIFunctionFactory exposes to the model.
/// </summary>
internal sealed class LlmTownDesignEntry
{
    [Description("Array of landmark buildings")]
    public List<LlmLandmarkEntry> Landmarks { get; set; } = [];

    [Description("Array of key locations in the town")]
    public List<LlmKeyLocationEntry> KeyLocations { get; set; } = [];

    [Description("Layout style: grid, radial, organic, linear, clustered, or compound")]
    public string LayoutStyle { get; set; } = "organic";

    [Description("Array of environmental hazards (0 to 4)")]
    public List<LlmHazardEntry> Hazards { get; set; } = [];

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

internal sealed class LlmLandmarkEntry
{
    [Description("Name of the landmark building (post-apocalyptic rename)")]
    public string Name { get; set; } = "";

    [Description("Visual description for 3D asset generation (exterior only)")]
    public string VisualDescription { get; set; } = "";

    [Description("Size category: small, medium, or large")]
    public string SizeCategory { get; set; } = "large";

    [Description("One sentence: what this building was in reality — real name, style, era, purpose")]
    public string OriginalDescription { get; set; } = "";

    [Description("30–60 word Meshy text-to-3D prompt: materials, damage, style. End with 'low-poly game asset'")]
    public string MeshyPrompt { get; set; } = "";

    [Description("Compass direction + nearby feature relative to town centre (e.g. 'north-east, near the harbour')")]
    public string PositionHint { get; set; } = "";
}

internal sealed class LlmKeyLocationEntry
{
    [Description("Name of the location (post-apocalyptic rename)")]
    public string Name { get; set; } = "";

    [Description("Purpose: shop, quest_giver, crafting, medical, barracks, tavern, storage, or other")]
    public string Purpose { get; set; } = "";

    [Description("Visual description for 3D asset generation (exterior only)")]
    public string VisualDescription { get; set; } = "";

    [Description("Size category: small, medium, or large")]
    public string SizeCategory { get; set; } = "medium";

    [Description("One sentence: what this building was in reality — real name, style, era, purpose")]
    public string OriginalDescription { get; set; } = "";

    [Description("30–60 word Meshy text-to-3D prompt: materials, damage, style. End with 'low-poly game asset'")]
    public string MeshyPrompt { get; set; } = "";

    [Description("Compass direction + nearby feature relative to town centre (e.g. 'south along main road')")]
    public string PositionHint { get; set; } = "";
}

internal sealed class LlmHazardEntry
{
    [Description("Hazard type: flooding, radiation, collapse, fire, toxic, wildlife, or other")]
    public string Type { get; set; } = "";

    [Description("Description of the hazard")]
    public string Description { get; set; } = "";

    [Description("Where in the town the hazard is located (e.g. 'south-west waterfront')")]
    public string LocationHint { get; set; } = "";
}
