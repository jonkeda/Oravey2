using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed class MeshyTaskResponse
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = "";
}
