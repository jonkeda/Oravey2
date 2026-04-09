using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.MapGen.RegionTemplates;

public record RegionPreset
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }

    // Geographic bounds
    public required double NorthLat { get; init; }
    public required double SouthLat { get; init; }
    public required double EastLon { get; init; }
    public required double WestLon { get; init; }

    // Download sources
    public required string OsmDownloadUrl { get; init; }

    // Default cull settings
    public CullSettings DefaultCullSettings { get; init; } = new();

    // Computed paths (not serialized)
    [JsonIgnore] public string RegionDir => Path.Combine("data", "regions", Name);
    [JsonIgnore] public string SrtmDir => Path.Combine(RegionDir, "srtm");
    [JsonIgnore] public string OsmDir => Path.Combine(RegionDir, "osm");
    [JsonIgnore] public string OutputDir => Path.Combine(RegionDir, "output");
    [JsonIgnore] public string OsmFilePath => Path.Combine(OsmDir, $"{Name}-latest.osm.pbf");
    [JsonIgnore] public string OutputFilePath => Path.Combine(OutputDir, $"{Name}.RegionTemplateFile");
    [JsonIgnore] public string PresetFilePath => Path.Combine(RegionDir, "region.json");

    public static string CacheDir => Path.Combine("data", "cache");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static RegionPreset Load(string path)
        => JsonSerializer.Deserialize<RegionPreset>(
            File.ReadAllText(path), SerializerOptions)
           ?? throw new InvalidOperationException($"Failed to deserialize preset from {path}");

    public void Save(string path)
        => File.WriteAllText(path,
            JsonSerializer.Serialize(this, SerializerOptions));

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(SrtmDir);
        Directory.CreateDirectory(OsmDir);
        Directory.CreateDirectory(OutputDir);
    }
}
