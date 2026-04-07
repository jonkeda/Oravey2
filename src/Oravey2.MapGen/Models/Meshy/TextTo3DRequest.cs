using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed record TextTo3DRequest(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("art_style")] string? ArtStyle = null,
    [property: JsonPropertyName("should_remesh")] bool? ShouldRemesh = null
);
