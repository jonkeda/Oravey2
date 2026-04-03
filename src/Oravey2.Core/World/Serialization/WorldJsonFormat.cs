namespace Oravey2.Core.World.Serialization;

public sealed record WorldJson(
    int ChunksWide,
    int ChunksHigh,
    float TileSize,
    PlayerStartJson PlayerStart,
    string? DefaultWeather
);

public sealed record PlayerStartJson(
    int ChunkX,
    int ChunkY,
    int LocalTileX,
    int LocalTileY
);
