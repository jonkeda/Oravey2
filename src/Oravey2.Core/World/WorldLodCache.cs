using Oravey2.Core.Camera;

namespace Oravey2.Core.World;

/// <summary>
/// Zoom-level biome classification for L2/L3 rendering.
/// Maps dominant surface types to visual biome categories.
/// </summary>
public enum LodBiome : byte
{
    Grassland,
    Forest,
    Desert,
    Urban,
    Wasteland,
    Snow,
    Water,
    Mountain,
}

/// <summary>
/// Cached LOD data for a single L2 cell (typically one chunk or a small group of chunks).
/// </summary>
public sealed record LodCellData(float AverageHeight, LodBiome Biome);

/// <summary>
/// Caches derived LOD data for Level 2 and Level 3 rendering.
///
/// Level 2: average chunk heights + dominant biome per chunk.
/// Level 3: average of L2 regions.
///
/// Invalidated per-region when chunks change.
/// </summary>
public sealed class WorldLodCache
{
    private readonly WorldMapData _world;

    // L2 cache: one entry per chunk
    private readonly LodCellData?[,] _l2Cache;
    // L3 cache: one entry per L3 cell (group of L2 cells)
    private readonly LodCellData?[,] _l3Cache;

    /// <summary>How many L2 cells (chunks) are grouped into one L3 cell.</summary>
    public const int L3GroupSize = 4;

    public int L2Width => _world.ChunksWide;
    public int L2Height => _world.ChunksHigh;
    public int L3Width { get; }
    public int L3Height { get; }

    public WorldLodCache(WorldMapData world)
    {
        _world = world;
        _l2Cache = new LodCellData?[world.ChunksWide, world.ChunksHigh];
        L3Width = (world.ChunksWide + L3GroupSize - 1) / L3GroupSize;
        L3Height = (world.ChunksHigh + L3GroupSize - 1) / L3GroupSize;
        _l3Cache = new LodCellData?[L3Width, L3Height];
    }

    /// <summary>
    /// Gets or computes the L2 LOD data for a chunk.
    /// </summary>
    public LodCellData GetL2Cell(int chunkX, int chunkY)
    {
        if (chunkX < 0 || chunkX >= L2Width || chunkY < 0 || chunkY >= L2Height)
            return new LodCellData(0f, LodBiome.Wasteland);

        var cached = _l2Cache[chunkX, chunkY];
        if (cached is not null)
            return cached;

        var computed = ComputeL2Cell(chunkX, chunkY);
        _l2Cache[chunkX, chunkY] = computed;
        return computed;
    }

    /// <summary>
    /// Gets or computes the L3 LOD data for a region group.
    /// </summary>
    public LodCellData GetL3Cell(int l3X, int l3Y)
    {
        if (l3X < 0 || l3X >= L3Width || l3Y < 0 || l3Y >= L3Height)
            return new LodCellData(0f, LodBiome.Wasteland);

        var cached = _l3Cache[l3X, l3Y];
        if (cached is not null)
            return cached;

        var computed = ComputeL3Cell(l3X, l3Y);
        _l3Cache[l3X, l3Y] = computed;
        return computed;
    }

    /// <summary>
    /// Invalidates cached data for all chunks in a rectangular region.
    /// Also invalidates affected L3 cells.
    /// </summary>
    public void InvalidateRegion(int fromChunkX, int fromChunkY, int toChunkX, int toChunkY)
    {
        for (int x = fromChunkX; x <= toChunkX; x++)
        {
            for (int y = fromChunkY; y <= toChunkY; y++)
            {
                if (x >= 0 && x < L2Width && y >= 0 && y < L2Height)
                    _l2Cache[x, y] = null;
            }
        }

        // Invalidate affected L3 cells
        int l3FromX = fromChunkX / L3GroupSize;
        int l3FromY = fromChunkY / L3GroupSize;
        int l3ToX = toChunkX / L3GroupSize;
        int l3ToY = toChunkY / L3GroupSize;

        for (int x = l3FromX; x <= l3ToX; x++)
        {
            for (int y = l3FromY; y <= l3ToY; y++)
            {
                if (x >= 0 && x < L3Width && y >= 0 && y < L3Height)
                    _l3Cache[x, y] = null;
            }
        }
    }

    /// <summary>
    /// Invalidates a single chunk and its containing L3 cell.
    /// </summary>
    public void InvalidateChunk(int chunkX, int chunkY)
        => InvalidateRegion(chunkX, chunkY, chunkX, chunkY);

    private LodCellData ComputeL2Cell(int chunkX, int chunkY)
    {
        var chunk = _world.GetChunk(chunkX, chunkY);
        if (chunk is null)
            return new LodCellData(0f, LodBiome.Wasteland);

        var tiles = chunk.Tiles;
        float heightSum = 0f;
        int count = 0;

        // Surface type tallies
        Span<int> surfaceCounts = stackalloc int[9]; // SurfaceType has 9 values
        bool hasForestFlag = false;
        int waterCount = 0;

        for (int x = 0; x < ChunkData.Size; x++)
        {
            for (int y = 0; y < ChunkData.Size; y++)
            {
                var td = tiles.GetTileData(x, y);
                heightSum += td.HeightLevel * HeightHelper.HeightStep;
                count++;

                if (td.HasWater)
                    waterCount++;

                int surfIdx = (int)td.Surface;
                if (surfIdx >= 0 && surfIdx < 9)
                    surfaceCounts[surfIdx]++;

                if (td.Flags.HasFlag(TileFlags.Forested))
                    hasForestFlag = true;
            }
        }

        float avgHeight = count > 0 ? heightSum / count : 0f;
        var biome = DeriveBiome(surfaceCounts, hasForestFlag, waterCount, count, avgHeight);
        return new LodCellData(avgHeight, biome);
    }

    private LodCellData ComputeL3Cell(int l3X, int l3Y)
    {
        float heightSum = 0f;
        int count = 0;
        Span<int> biomeCounts = stackalloc int[8]; // LodBiome has 8 values

        int startX = l3X * L3GroupSize;
        int startY = l3Y * L3GroupSize;

        for (int dx = 0; dx < L3GroupSize; dx++)
        {
            for (int dy = 0; dy < L3GroupSize; dy++)
            {
                int cx = startX + dx;
                int cy = startY + dy;
                if (cx >= L2Width || cy >= L2Height) continue;

                var l2 = GetL2Cell(cx, cy);
                heightSum += l2.AverageHeight;
                count++;
                biomeCounts[(int)l2.Biome]++;
            }
        }

        float avgHeight = count > 0 ? heightSum / count : 0f;

        // Dominant biome
        int maxIdx = 0;
        for (int i = 1; i < 8; i++)
        {
            if (biomeCounts[i] > biomeCounts[maxIdx])
                maxIdx = i;
        }

        return new LodCellData(avgHeight, (LodBiome)maxIdx);
    }

    internal static LodBiome DeriveBiome(
        ReadOnlySpan<int> surfaceCounts, bool hasForestFlag, int waterCount,
        int totalTiles, float avgHeight)
    {
        if (totalTiles == 0) return LodBiome.Wasteland;

        float waterRatio = (float)waterCount / totalTiles;
        if (waterRatio > 0.5f)
            return LodBiome.Water;

        // Mountain: high average height
        if (avgHeight > 20f)
            return LodBiome.Mountain;

        // Dominant surface type
        int dominant = 0;
        for (int i = 1; i < surfaceCounts.Length; i++)
        {
            if (surfaceCounts[i] > surfaceCounts[dominant])
                dominant = i;
        }

        var dominantSurface = (SurfaceType)dominant;

        // Forest: grass-dominant + forested flag
        if (hasForestFlag && (dominantSurface == SurfaceType.Grass || dominantSurface == SurfaceType.Dirt))
            return LodBiome.Forest;

        return dominantSurface switch
        {
            SurfaceType.Grass => LodBiome.Grassland,
            SurfaceType.Concrete or SurfaceType.Asphalt or SurfaceType.Metal => LodBiome.Urban,
            SurfaceType.Sand => LodBiome.Desert,
            SurfaceType.Rock => LodBiome.Mountain,
            SurfaceType.Dirt => LodBiome.Wasteland,
            SurfaceType.Mud => LodBiome.Wasteland,
            SurfaceType.Gravel => LodBiome.Wasteland,
            _ => LodBiome.Wasteland,
        };
    }
}
