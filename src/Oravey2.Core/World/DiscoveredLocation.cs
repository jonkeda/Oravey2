namespace Oravey2.Core.World;

/// <summary>
/// A location the player has discovered and can potentially fast-travel to.
/// </summary>
public sealed record DiscoveredLocation(
    string Id,
    string Name,
    int ChunkX,
    int ChunkY
);
