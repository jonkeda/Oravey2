namespace Oravey2.MapGen.Models;

public sealed record MapGenerationRequest
{
    public required string LocationName { get; init; }
    public required string GeographyDescription { get; init; }
    public required string PostApocContext { get; init; }
    public required int ChunksWide { get; init; }
    public required int ChunksHigh { get; init; }
    public required int MinLevel { get; init; }
    public required int MaxLevel { get; init; }
    public required string DifficultyDescription { get; init; }
    public required string[] Factions { get; init; }
    public string? Model { get; init; }
    public string TimeOfDay { get; init; } = "Dawn";
    public string WeatherDefault { get; init; } = "overcast";
}
