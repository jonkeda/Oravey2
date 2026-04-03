using Oravey2.Core.World.Blueprint;

namespace Oravey2.MapGen.Models;

public sealed record GenerationResult
{
    public required bool Success { get; init; }
    public MapBlueprint? Blueprint { get; init; }
    public string? RawJson { get; init; }
    public string[]? ValidationErrors { get; init; }
    public string? ErrorMessage { get; init; }
    public string? SessionId { get; init; }
    public TimeSpan Elapsed { get; init; }
}
