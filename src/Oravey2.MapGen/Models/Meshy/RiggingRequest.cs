using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed class RiggingRequest
{
    [JsonPropertyName("input_task_id")]
    public string InputTaskId { get; set; } = "";
}
