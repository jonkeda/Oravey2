namespace Oravey2.MapGen.Generation;

public sealed record TownDesign(
    string TownName,
    LandmarkBuilding Landmark,
    List<KeyLocation> KeyLocations,
    string LayoutStyle,
    List<EnvironmentalHazard> Hazards);

public sealed record LandmarkBuilding(
    string Name, string VisualDescription, string SizeCategory);

public sealed record KeyLocation(
    string Name, string Purpose, string VisualDescription, string SizeCategory);

public sealed record EnvironmentalHazard(
    string Type, string Description, string LocationHint);
