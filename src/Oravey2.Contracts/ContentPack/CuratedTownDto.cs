namespace Oravey2.Contracts.ContentPack;

public sealed record CuratedTownDto(
    string GameName,
    string RealName,
    double Latitude,
    double Longitude,
    string Role,
    string Faction,
    int ThreatLevel,
    string Description,
    int EstimatedPopulation);
