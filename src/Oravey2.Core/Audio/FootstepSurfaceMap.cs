using Oravey2.Core.World;

namespace Oravey2.Core.Audio;

/// <summary>
/// Maps TileType to a footstep SFX identifier. Returns null for non-walkable tiles.
/// </summary>
public static class FootstepSurfaceMap
{
    private static readonly Dictionary<TileType, string> Map = new()
    {
        [TileType.Ground] = "sfx_footstep_ground",
        [TileType.Road] = "sfx_footstep_road",
        [TileType.Rubble] = "sfx_footstep_rubble",
        [TileType.Water] = "sfx_footstep_water",
    };

    /// <summary>
    /// Returns the footstep SFX id for the tile type, or null if no footstep
    /// should play (e.g., Empty, Wall).
    /// </summary>
    public static string? GetSfxId(TileType tileType)
        => Map.TryGetValue(tileType, out var id) ? id : null;
}
