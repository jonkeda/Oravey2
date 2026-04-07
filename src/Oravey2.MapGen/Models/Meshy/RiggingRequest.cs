using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed record RiggingRequest(
    [property: JsonPropertyName("input_task_id")] string InputTaskId
);
