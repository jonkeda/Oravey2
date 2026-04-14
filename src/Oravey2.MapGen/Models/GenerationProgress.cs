namespace Oravey2.MapGen.Models;

public sealed class GenerationProgress
{
    public required GenerationPhase Phase { get; set; }
    public required string Message { get; set; }
    public string? StreamDelta { get; set; }
    public string? ToolName { get; set; }
    public string? ToolResult { get; set; }
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
