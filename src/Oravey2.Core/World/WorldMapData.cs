namespace Oravey2.Core.World;

public sealed class WorldMapData
{
    public int ChunksWide { get; }
    public int ChunksHigh { get; }

    private readonly ChunkData?[,] _chunks;

    public WorldMapData(int chunksWide, int chunksHigh)
    {
        if (chunksWide <= 0) throw new ArgumentOutOfRangeException(nameof(chunksWide));
        if (chunksHigh <= 0) throw new ArgumentOutOfRangeException(nameof(chunksHigh));

        ChunksWide = chunksWide;
        ChunksHigh = chunksHigh;
        _chunks = new ChunkData?[chunksWide, chunksHigh];
    }

    /// <summary>
    /// Gets the chunk at the given chunk coordinates, or null if out of bounds / not loaded.
    /// </summary>
    public ChunkData? GetChunk(int cx, int cy)
    {
        if (cx < 0 || cx >= ChunksWide || cy < 0 || cy >= ChunksHigh)
            return null;
        return _chunks[cx, cy];
    }

    /// <summary>
    /// Sets a chunk at the given chunk coordinates. Used during world setup and streaming.
    /// </summary>
    public void SetChunk(int cx, int cy, ChunkData? chunk)
    {
        if (cx < 0 || cx >= ChunksWide || cy < 0 || cy >= ChunksHigh)
            return;
        _chunks[cx, cy] = chunk;
    }

    /// <summary>
    /// Converts world-space tile coordinates to chunk coordinates.
    /// </summary>
    public static (int cx, int cy) TileToChunk(int tileX, int tileY)
    {
        int cx = tileX / ChunkData.Size;
        int cy = tileY / ChunkData.Size;
        return (cx, cy);
    }

    /// <summary>
    /// Checks if chunk coordinates are within the world grid.
    /// </summary>
    public bool InBounds(int cx, int cy)
        => cx >= 0 && cx < ChunksWide && cy >= 0 && cy < ChunksHigh;

    /// <summary>
    /// Returns all non-null chunks currently set. Useful for serialization.
    /// </summary>
    public IEnumerable<ChunkData> GetAllChunks()
    {
        for (int x = 0; x < ChunksWide; x++)
            for (int y = 0; y < ChunksHigh; y++)
                if (_chunks[x, y] is { } chunk)
                    yield return chunk;
    }
}
