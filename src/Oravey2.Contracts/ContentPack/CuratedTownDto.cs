namespace Oravey2.Contracts.ContentPack;

public sealed record CuratedTownDto(
    string GameName,
    string RealName,
    double Latitude,
    double Longitude,
    string Description,
    string Size,
    int Inhabitants,
    string Destruction);
