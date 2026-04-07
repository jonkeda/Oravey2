using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed record MeshyTaskStatus(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("progress")] int Progress,
    [property: JsonPropertyName("model_urls")] Dictionary<string, string>? ModelUrls = null,
    [property: JsonPropertyName("thumbnail_url")] string? ThumbnailUrl = null,
    [property: JsonPropertyName("task_error")] string? TaskError = null
);
