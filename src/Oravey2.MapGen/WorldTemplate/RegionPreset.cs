using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.MapGen.WorldTemplate;

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
    public string? OsmFileName { get; init; }

    // Default paths
    public string DefaultSrtmDir { get; init; } = "data/srtm";
    public string DefaultOutputDir { get; init; } = "content";

    // Default cull settings
    public CullSettings DefaultCullSettings { get; init; } = new();

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
}
