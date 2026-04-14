namespace Oravey2.MapGen.Models;

public sealed class GenerationResult
{
    public required bool Success { get; set; }
    public string? RawJson { get; set; }
    public string[]? ValidationErrors { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SessionId { get; set; }
    public TimeSpan Elapsed { get; set; }
}
