using Oravey2.Contracts.Spatial;

namespace Oravey2.MapGen.Generation;

public sealed class TownDesign
{
    public string TownName { get; set; } = "";
    public List<LandmarkBuilding> Landmarks { get; set; } = [];
    public List<KeyLocation> KeyLocations { get; set; } = [];
    public string LayoutStyle { get; set; } = "";
    public List<EnvironmentalHazard> Hazards { get; set; } = [];
    public TownSpatialSpecification? SpatialSpec { get; set; }
}

public sealed class LandmarkBuilding
{
    public string Name { get; set; } = "";
    public string VisualDescription { get; set; } = "";
    public string SizeCategory { get; set; } = "";
    public string OriginalDescription { get; set; } = "";
    public string MeshyPrompt { get; set; } = "";
    public string PositionHint { get; set; } = "";
}

public sealed class KeyLocation
{
    public string Name { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string VisualDescription { get; set; } = "";
    public string SizeCategory { get; set; } = "";
    public string OriginalDescription { get; set; } = "";
    public string MeshyPrompt { get; set; } = "";
    public string PositionHint { get; set; } = "";
}

public sealed class EnvironmentalHazard
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string LocationHint { get; set; } = "";
}
