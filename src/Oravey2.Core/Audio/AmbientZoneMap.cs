using Oravey2.Core.World;

namespace Oravey2.Core.Audio;

/// <summary>
/// Maps BiomeType to ambient sound identifiers. Each biome may have multiple ambient loops
/// that play simultaneously (e.g., wind + distant thunder).
/// </summary>
public static class AmbientZoneMap
{
    private static readonly Dictionary<BiomeType, string[]> Map = new()
    {
        [BiomeType.RuinedCity] = ["amb_wind_urban", "amb_creaking_metal"],
        [BiomeType.Wasteland] = ["amb_wind_open", "amb_distant_thunder"],
        [BiomeType.Bunker] = ["amb_hum_mechanical", "amb_dripping_water"],
        [BiomeType.Settlement] = ["amb_crowd_murmur", "amb_campfire_crackle"],
        [BiomeType.Industrial] = ["amb_machinery_hum", "amb_steam_hiss"],
        [BiomeType.ForestOvergrown] = ["amb_wind_leaves", "amb_insects"],
        [BiomeType.IrradiatedCrater] = ["amb_geiger_crackle", "amb_eerie_hum"],
        [BiomeType.Underground] = ["amb_cave_echo", "amb_dripping_water"],
        [BiomeType.Coastal] = ["amb_waves", "amb_seabirds"],
    };

    /// <summary>
    /// Returns the ambient sound IDs for the given biome. Returns an empty array for unknown biomes.
    /// </summary>
    public static string[] GetAmbientIds(BiomeType biome)
        => Map.TryGetValue(biome, out var ids) ? ids : [];
}
