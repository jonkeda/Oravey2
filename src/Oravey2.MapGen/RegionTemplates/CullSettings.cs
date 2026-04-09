using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.MapGen.RegionTemplates;

public record CullSettings
{
    // ---- Town culling ----
    public TownCategory TownMinCategory { get; init; } = TownCategory.Village;
    public int TownMinPopulation { get; init; } = 1_000;
    public double TownMinSpacingKm { get; init; } = 5.0;
    public int TownMaxCount { get; init; } = 30;
    public CullPriority TownPriority { get; init; } = CullPriority.Category;
    public bool TownAlwaysKeepCities { get; init; } = true;
    public bool TownAlwaysKeepMetropolis { get; init; } = true;

    // ---- Road culling ----
    public RoadClass RoadMinClass { get; init; } = RoadClass.Primary;
    public bool RoadAlwaysKeepMotorways { get; init; } = true;
    public bool RoadKeepNearTowns { get; init; } = true;
    public double RoadTownProximityKm { get; init; } = 2.0;
    public bool RoadRemoveDeadEnds { get; init; } = true;
    public double RoadDeadEndMinKm { get; init; } = 1.0;
    public bool RoadSimplifyGeometry { get; init; } = true;
    public double RoadSimplifyToleranceM { get; init; } = 50.0;

    // ---- Water culling ----
    public double WaterMinAreaKm2 { get; init; } = 0.1;
    public double WaterMinRiverLengthKm { get; init; } = 2.0;
    public bool WaterAlwaysKeepSea { get; init; } = true;
    public bool WaterAlwaysKeepLakes { get; init; } = true;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static CullSettings Load(string path)
        => JsonSerializer.Deserialize<CullSettings>(
            File.ReadAllText(path), SerializerOptions) ?? new();

    public void Save(string path)
        => File.WriteAllText(path,
            JsonSerializer.Serialize(this, SerializerOptions));
}
