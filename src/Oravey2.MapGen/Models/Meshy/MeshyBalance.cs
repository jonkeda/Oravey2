using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed class MeshyBalance
{
    [JsonPropertyName("balance")]
    public int Balance { get; set; }
}
