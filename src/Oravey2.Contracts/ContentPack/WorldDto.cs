namespace Oravey2.Contracts.ContentPack;

public sealed record WorldDto(
    string Name,
    string Description,
    string Source,
    int ChunksWide,
    int ChunksHigh,
    int TileSize,
    PlacementDto? PlayerStart,
    List<TownRefDto> Towns);

public sealed record TownRefDto(
    string GameName,
    string RealName,
    float GameX,
    float GameY,
    string Description,
    string Size,
    int Inhabitants,
    string Destruction);
