using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed class TextTo3DRequest
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "";

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("art_style")]
    public string? ArtStyle { get; set; }

    [JsonPropertyName("should_remesh")]
    public bool? ShouldRemesh { get; set; }
}
