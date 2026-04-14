namespace Oravey2.Contracts.ContentPack;

public sealed class ZoneDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Biome { get; set; } = 0;
    public float RadiationLevel { get; set; } = 0f;
    public int EnemyDifficultyTier { get; set; } = 0;
    public bool IsFastTravelTarget { get; set; } = false;
    public int ChunkStartX { get; set; } = 0;
    public int ChunkStartY { get; set; } = 0;
    public int ChunkEndX { get; set; } = 0;
    public int ChunkEndY { get; set; } = 0;
}
