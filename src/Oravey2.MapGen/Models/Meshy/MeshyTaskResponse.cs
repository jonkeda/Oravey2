using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed record MeshyTaskResponse(
    [property: JsonPropertyName("result")] string Result
);
