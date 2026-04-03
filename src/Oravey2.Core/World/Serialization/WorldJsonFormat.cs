namespace Oravey2.Core.World.Serialization;

public sealed record WorldJson(
    int ChunksWide,
    int ChunksHigh,
    float TileSize,
    PlayerStartJson PlayerStart,
    string? DefaultWeather,
    string? Name = null,
    string? Description = null,
    string? Source = null
);

public sealed record PlayerStartJson(
    int ChunkX,
    int ChunkY,
    int LocalTileX,
    int LocalTileY
);
