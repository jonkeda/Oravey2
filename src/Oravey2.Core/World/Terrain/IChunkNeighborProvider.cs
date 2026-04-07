namespace Oravey2.Core.World.Terrain;

/// <summary>
/// Provides tile data from neighbouring chunks for seamless edge stitching.
/// </summary>
public interface IChunkNeighborProvider
{
    /// <summary>
    /// Gets the tile at the given local-space offset relative to the current chunk.
    /// Coordinates may be negative or >= ChunkData.Size, indicating a neighbour chunk.
    /// Returns TileData.Empty if the neighbour is unavailable.
    /// </summary>
    TileData GetTileAt(int localX, int localY);
}
