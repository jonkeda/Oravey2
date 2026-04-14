using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed class AnimationRequest
{
    [JsonPropertyName("action_id")]
    public string ActionId { get; set; } = "";
}
