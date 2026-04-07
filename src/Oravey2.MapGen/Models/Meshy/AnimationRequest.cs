using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed record AnimationRequest(
    [property: JsonPropertyName("action_id")] string ActionId
);
