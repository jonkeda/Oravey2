using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed class ImageTo3DRequest
{
    [JsonPropertyName("image_url")]
    public string ImageUrl { get; set; } = "";

    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    [JsonPropertyName("art_style")]
    public string? ArtStyle { get; set; }
}
