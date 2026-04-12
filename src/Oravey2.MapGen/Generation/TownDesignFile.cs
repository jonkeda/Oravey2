using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Generation;

public sealed class TownDesignFile
{
    public string TownName { get; set; } = "";
    public List<LandmarkEntry> Landmarks { get; set; } = [];
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
        Landmarks = design.Landmarks.Select(l => new LandmarkEntry
        {
            Name = l.Name,
            VisualDescription = l.VisualDescription,
            SizeCategory = l.SizeCategory,
            OriginalDescription = l.OriginalDescription,
            MeshyPrompt = l.MeshyPrompt,
            PositionHint = l.PositionHint,
        }).ToList(),
        KeyLocations = design.KeyLocations.Select(k => new KeyLocationEntry
        {
            Name = k.Name,
            Purpose = k.Purpose,
            VisualDescription = k.VisualDescription,
            SizeCategory = k.SizeCategory,
            OriginalDescription = k.OriginalDescription,
            MeshyPrompt = k.MeshyPrompt,
            PositionHint = k.PositionHint,
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
        Landmarks.Select(l => new LandmarkBuilding(
            l.Name, l.VisualDescription, l.SizeCategory,
            l.OriginalDescription, l.MeshyPrompt, l.PositionHint)).ToList(),
        KeyLocations.Select(k => new KeyLocation(
            k.Name, k.Purpose, k.VisualDescription, k.SizeCategory,
            k.OriginalDescription, k.MeshyPrompt, k.PositionHint)).ToList(),
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
    public string OriginalDescription { get; set; } = "";
    public string MeshyPrompt { get; set; } = "";
    public string PositionHint { get; set; } = "";
}

public sealed class KeyLocationEntry
{
    public string Name { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string VisualDescription { get; set; } = "";
    public string SizeCategory { get; set; } = "";
    public string OriginalDescription { get; set; } = "";
    public string MeshyPrompt { get; set; } = "";
    public string PositionHint { get; set; } = "";
}

public sealed class HazardEntry
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string LocationHint { get; set; } = "";
}
