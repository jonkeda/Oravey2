using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Generation;

public sealed class TownDesignFile
{
    public string TownName { get; set; } = "";
    public LandmarkEntry Landmark { get; set; } = new();
    public List<KeyLocationEntry> KeyLocations { get; set; } = [];
    public string LayoutStyle { get; set; } = "";
    public List<HazardEntry> Hazards { get; set; } = [];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static TownDesignFile FromTownDesign(TownDesign design) => new()
    {
        TownName = design.TownName,
        Landmark = new LandmarkEntry
        {
            Name = design.Landmark.Name,
            VisualDescription = design.Landmark.VisualDescription,
            SizeCategory = design.Landmark.SizeCategory,
        },
        KeyLocations = design.KeyLocations.Select(k => new KeyLocationEntry
        {
            Name = k.Name,
            Purpose = k.Purpose,
            VisualDescription = k.VisualDescription,
            SizeCategory = k.SizeCategory,
        }).ToList(),
        LayoutStyle = design.LayoutStyle,
        Hazards = design.Hazards.Select(h => new HazardEntry
        {
            Type = h.Type,
            Description = h.Description,
            LocationHint = h.LocationHint,
        }).ToList(),
    };

    public TownDesign ToTownDesign() => new(
        TownName,
        new LandmarkBuilding(Landmark.Name, Landmark.VisualDescription, Landmark.SizeCategory),
        KeyLocations.Select(k => new KeyLocation(k.Name, k.Purpose, k.VisualDescription, k.SizeCategory)).ToList(),
        LayoutStyle,
        Hazards.Select(h => new EnvironmentalHazard(h.Type, h.Description, h.LocationHint)).ToList());

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
    }

    public static TownDesignFile Load(string path)
        => JsonSerializer.Deserialize<TownDesignFile>(
            File.ReadAllText(path), SerializerOptions) ?? new();
}

public sealed class LandmarkEntry
{
    public string Name { get; set; } = "";
    public string VisualDescription { get; set; } = "";
    public string SizeCategory { get; set; } = "";
}

public sealed class KeyLocationEntry
{
    public string Name { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string VisualDescription { get; set; } = "";
    public string SizeCategory { get; set; } = "";
}

public sealed class HazardEntry
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string LocationHint { get; set; } = "";
}
