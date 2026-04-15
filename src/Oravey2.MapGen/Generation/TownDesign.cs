using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
    }

    public static TownDesign Load(string path)
        => JsonSerializer.Deserialize<TownDesign>(
            File.ReadAllText(path), SerializerOptions) ?? new();
}

public sealed class LandmarkBuilding
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

public sealed class KeyLocation
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

public sealed class EnvironmentalHazard
{
    [Description("Hazard type: flooding, radiation, collapse, fire, toxic, wildlife, or other")]
    public string Type { get; set; } = "";
    [Description("Description of the hazard")]
    public string Description { get; set; } = "";
    [Description("Where in the town the hazard is located (e.g. 'south-west waterfront')")]
    public string LocationHint { get; set; } = "";
}
