namespace Oravey2.Contracts.ContentPack;

public sealed record ZoneDto(
    string Id,
    string Name,
    int Biome,
    float RadiationLevel,
    int EnemyDifficultyTier,
    bool IsFastTravelTarget,
    int ChunkStartX,
    int ChunkStartY,
    int ChunkEndX,
    int ChunkEndY);
