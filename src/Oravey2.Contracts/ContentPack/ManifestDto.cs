using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.Contracts.ContentPack;

public sealed class ManifestDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "0.1.0";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string Parent { get; set; } = "";
    public string? EngineVersion { get; set; }
    public string? RegionCode { get; set; }
    public string? DefaultScenario { get; set; }
    public List<string>? Tags { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
