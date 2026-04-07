using System.Numerics;

namespace Oravey2.Core.World.Terrain;

/// <summary>
/// Provides world-space height queries by converting world coordinates to chunk-local
/// coordinates and delegating to <see cref="HeightmapMeshGenerator.GetSurfaceHeight"/>.
/// Caches built height grids per chunk for efficient repeated queries.
/// </summary>
public sealed class TerrainHeightQuery
{
    private readonly TileMapData _mapData;
    private readonly float _tileSize;
    private readonly int _chunkSize;
    private readonly float _chunkWorldSize;
    private readonly int _chunksX;
    private readonly int _chunksY;
    private readonly float _halfWorldX;
    private readonly float _halfWorldZ;

    // Cached height grids per chunk [cx, cy]
    private readonly ChunkTerrainMesh?[,] _builtChunks;

    /// <summary>Offset applied to player Y so base of capsule sits on ground.</summary>
    public const float PlayerHeightOffset = 0.5f;

    /// <summary>Smoothing factor for height lerp (higher = faster snap).</summary>
    public const float HeightSmoothing = 15f;

    /// <summary>Height delta threshold for immediate snap (no lerp).</summary>
    public const float SnapThreshold = 3f;

    /// <summary>Maximum water depth the player can wade through.</summary>
    public const int MaxWadeDepth = 2;

    public TerrainHeightQuery(TileMapData mapData, float tileSize = HeightmapMeshGenerator.TileWorldSize)
    {
        _mapData = mapData;
        _tileSize = tileSize;
        _chunkSize = ChunkData.Size;
        _chunkWorldSize = _chunkSize * tileSize;
        _chunksX = (mapData.Width + _chunkSize - 1) / _chunkSize;
        _chunksY = (mapData.Height + _chunkSize - 1) / _chunkSize;
        _halfWorldX = mapData.Width * tileSize / 2f;
        _halfWorldZ = mapData.Height * tileSize / 2f;
        _builtChunks = new ChunkTerrainMesh?[_chunksX, _chunksY];
    }

    /// <summary>
    /// Returns the interpolated terrain height at the given world-space XZ position.
    /// Returns 0 if the position is out of bounds.
    /// </summary>
    public float GetHeight(float worldX, float worldZ)
    {
        // Convert world-space to tile grid space (un-centre the offset)
        float gridX = worldX + _halfWorldX;
        float gridZ = worldZ + _halfWorldZ;

        // Determine chunk index
        int cx = (int)(gridX / _chunkWorldSize);
        int cy = (int)(gridZ / _chunkWorldSize);

        // Clamp to valid chunk range
        cx = Math.Clamp(cx, 0, _chunksX - 1);
        cy = Math.Clamp(cy, 0, _chunksY - 1);

        var mesh = GetOrBuildChunk(cx, cy);
        if (mesh == null || mesh.Heights.Length == 0)
            return 0f;

        // Convert to chunk-local coordinates
        float localX = gridX - cx * _chunkWorldSize;
        float localZ = gridZ - cy * _chunkWorldSize;

        return HeightmapMeshGenerator.GetSurfaceHeight(
            new Vector2(localX, localZ),
            mesh.Heights,
            mesh.VertsPerSide,
            mesh.ChunkWorldSize);
    }

    /// <summary>
    /// Returns the tile data at the given world-space position.
    /// Returns default TileData if out of bounds.
    /// </summary>
    public TileData GetTileAt(float worldX, float worldZ)
    {
        float gridX = worldX + _halfWorldX;
        float gridZ = worldZ + _halfWorldZ;

        int tileX = (int)(gridX / _tileSize);
        int tileZ = (int)(gridZ / _tileSize);

        if (tileX < 0 || tileX >= _mapData.Width || tileZ < 0 || tileZ >= _mapData.Height)
            return default;

        return _mapData.GetTileData(tileX, tileZ);
    }

    /// <summary>
    /// Checks whether movement from one position to another would cross a cliff.
    /// Returns true if the height delta between the two tile positions exceeds <see cref="HeightHelper.CliffThreshold"/>.
    /// </summary>
    public bool IsCliffBlocking(float fromX, float fromZ, float toX, float toZ)
    {
        float gridFromX = fromX + _halfWorldX;
        float gridFromZ = fromZ + _halfWorldZ;
        float gridToX = toX + _halfWorldX;
        float gridToZ = toZ + _halfWorldZ;

        int fromTileX = (int)(gridFromX / _tileSize);
        int fromTileZ = (int)(gridFromZ / _tileSize);
        int toTileX = (int)(gridToX / _tileSize);
        int toTileZ = (int)(gridToZ / _tileSize);

        // Same tile — no cliff
        if (fromTileX == toTileX && fromTileZ == toTileZ)
            return false;

        // Out of bounds — block
        if (fromTileX < 0 || fromTileX >= _mapData.Width || fromTileZ < 0 || fromTileZ >= _mapData.Height)
            return true;
        if (toTileX < 0 || toTileX >= _mapData.Width || toTileZ < 0 || toTileZ >= _mapData.Height)
            return true;

        var fromTile = _mapData.GetTileData(fromTileX, fromTileZ);
        var toTile = _mapData.GetTileData(toTileX, toTileZ);
        int delta = toTile.HeightLevel - fromTile.HeightLevel;

        return !HeightHelper.IsPassable(delta);
    }

    /// <summary>
    /// Checks whether the target tile has liquid too deep for wading.
    /// Returns true if depth > <see cref="MaxWadeDepth"/>.
    /// </summary>
    public bool IsDeepLiquid(float worldX, float worldZ)
    {
        var tile = GetTileAt(worldX, worldZ);
        return tile.WaterDepth > MaxWadeDepth;
    }

    /// <summary>
    /// Returns the liquid surface height if shallow wading, or terrain height otherwise.
    /// </summary>
    public float GetEffectiveHeight(float worldX, float worldZ)
    {
        float terrainY = GetHeight(worldX, worldZ);
        var tile = GetTileAt(worldX, worldZ);

        if (tile.HasWater && tile.WaterDepth > 0 && tile.WaterDepth <= MaxWadeDepth)
        {
            // In shallow liquid, snap to liquid surface
            float waterY = tile.WaterLevel * HeightmapMeshGenerator.HeightStep;
            return Math.Max(terrainY, waterY);
        }

        return terrainY;
    }

    private ChunkTerrainMesh? GetOrBuildChunk(int cx, int cy)
    {
        if (cx < 0 || cx >= _chunksX || cy < 0 || cy >= _chunksY)
            return null;

        if (_builtChunks[cx, cy] != null)
            return _builtChunks[cx, cy];

        // Extract tiles for this chunk and build
        var chunkTiles = new TileData[_chunkSize, _chunkSize];
        for (int lx = 0; lx < _chunkSize; lx++)
        {
            for (int ly = 0; ly < _chunkSize; ly++)
            {
                int gx = cx * _chunkSize + lx;
                int gy = cy * _chunkSize + ly;
                if (gx < _mapData.Width && gy < _mapData.Height)
                    chunkTiles[lx, ly] = _mapData.TileDataGrid[gx, gy];
            }
        }

        var tileMap = new TileMapData(_chunkSize, _chunkSize);
        for (int x = 0; x < _chunkSize; x++)
            for (int y = 0; y < _chunkSize; y++)
                tileMap.SetTileData(x, y, chunkTiles[x, y]);

        var chunkData = new ChunkData(cx, cy, tileMap);
        var mesh = ChunkTerrainBuilder.Build(chunkData);

        _builtChunks[cx, cy] = mesh;
        return mesh;
    }
}
