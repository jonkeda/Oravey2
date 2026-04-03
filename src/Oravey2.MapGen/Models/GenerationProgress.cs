namespace Oravey2.MapGen.Models;

public sealed record GenerationProgress
{
    public required GenerationPhase Phase { get; init; }
    public required string Message { get; init; }
    public string? StreamDelta { get; init; }
    public string? ToolName { get; init; }
    public string? ToolResult { get; init; }
}

public enum GenerationPhase
{
    Initializing,
    Prompting,
    Streaming,
    ToolCall,
    Validating,
    Fixing,
    Complete,
    Error
}
