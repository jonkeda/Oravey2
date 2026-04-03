namespace Oravey2.Core.World.Blueprint;

public static class ZoneCompiler
{
    /// <summary>
    /// Converts zone blueprints to ZoneDefinition records.
    /// </summary>
    public static ZoneDefinition[] CompileZones(ZoneBlueprint[]? zones)
    {
        if (zones is null or { Length: 0 })
            return Array.Empty<ZoneDefinition>();

        return zones.Select(z =>
        {
            var biome = Enum.TryParse<BiomeType>(z.Biome, true, out var b)
                ? b : BiomeType.Wasteland;

            return new ZoneDefinition(
                z.Id, z.Name, biome, z.RadiationLevel,
                z.EnemyDifficultyTier, z.IsFastTravelTarget,
                z.ChunkStartX, z.ChunkStartY, z.ChunkEndX, z.ChunkEndY);
        }).ToArray();
    }
}
