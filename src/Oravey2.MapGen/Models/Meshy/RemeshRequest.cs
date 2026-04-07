using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed record RemeshRequest(
    [property: JsonPropertyName("input_task_id")] string InputTaskId,
    [property: JsonPropertyName("target_formats")] string[]? TargetFormats = null,
    [property: JsonPropertyName("topology")] string? Topology = null,
    [property: JsonPropertyName("target_polycount")] int? TargetPolycount = null,
    [property: JsonPropertyName("resize_height")] double? ResizeHeight = null,
    [property: JsonPropertyName("origin_at")] string? OriginAt = null
);
