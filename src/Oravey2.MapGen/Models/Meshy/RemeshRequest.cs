using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed class RemeshRequest
{
    [JsonPropertyName("input_task_id")]
    public string InputTaskId { get; set; } = "";

    [JsonPropertyName("target_formats")]
    public string[]? TargetFormats { get; set; }

    [JsonPropertyName("topology")]
    public string? Topology { get; set; }

    [JsonPropertyName("target_polycount")]
    public int? TargetPolycount { get; set; }

    [JsonPropertyName("resize_height")]
    public double? ResizeHeight { get; set; }

    [JsonPropertyName("origin_at")]
    public string? OriginAt { get; set; }
}
