using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed record ImageTo3DRequest(
    [property: JsonPropertyName("image_url")] string ImageUrl,
    [property: JsonPropertyName("prompt")] string? Prompt = null,
    [property: JsonPropertyName("art_style")] string? ArtStyle = null
);
