using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed class MeshyTaskStatus
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("model_urls")]
    public Dictionary<string, string>? ModelUrls { get; set; }

    [JsonPropertyName("thumbnail_url")]
    public string? ThumbnailUrl { get; set; }

    [JsonPropertyName("task_error")]
    public string? TaskError { get; set; }
}
