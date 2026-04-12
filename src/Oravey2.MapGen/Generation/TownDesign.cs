namespace Oravey2.MapGen.Generation;

public sealed record TownDesign(
    string TownName,
    List<LandmarkBuilding> Landmarks,
    List<KeyLocation> KeyLocations,
    string LayoutStyle,
    List<EnvironmentalHazard> Hazards,
    TownSpatialSpecification? SpatialSpec = null);

public sealed record LandmarkBuilding(
    string Name, string VisualDescription, string SizeCategory,
    string OriginalDescription, string MeshyPrompt, string PositionHint);

public sealed record KeyLocation(
    string Name, string Purpose, string VisualDescription, string SizeCategory,
    string OriginalDescription, string MeshyPrompt, string PositionHint);

public sealed record EnvironmentalHazard(
    string Type, string Description, string LocationHint);
