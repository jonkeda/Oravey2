namespace Oravey2.Core.World;

/// <summary>
/// Defines a named zone with biome, radiation, difficulty, and chunk range.
/// </summary>
public sealed record ZoneDefinition(
    string Id,
    string Name,
    BiomeType Biome,
    float RadiationLevel,
    int EnemyDifficultyTier,
    bool IsFastTravelTarget,
    int ChunkStartX,
    int ChunkStartY,
    int ChunkEndX,
    int ChunkEndY
)
{
    /// <summary>
    /// Checks whether the given chunk coordinates fall within this zone's chunk range (inclusive).
    /// </summary>
    public bool ContainsChunk(int cx, int cy)
        => cx >= ChunkStartX && cx <= ChunkEndX
        && cy >= ChunkStartY && cy <= ChunkEndY;
}
