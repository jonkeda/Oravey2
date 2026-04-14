namespace Oravey2.MapGen.Models;

public sealed class MapGenerationRequest
{
    public required string LocationName { get; set; }
    public required string GeographyDescription { get; set; }
    public required string PostApocContext { get; set; }
    public required int ChunksWide { get; set; }
    public required int ChunksHigh { get; set; }
    public required int MinLevel { get; set; }
    public required int MaxLevel { get; set; }
    public required string DifficultyDescription { get; set; }
    public required string[] Factions { get; set; }
    public string? Model { get; set; }
    public string TimeOfDay { get; set; } = "Dawn";
    public string WeatherDefault { get; set; } = "overcast";
}
