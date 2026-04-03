namespace Oravey2.Core.World;

public sealed class ChunkData
{
    public const int Size = 16;

    public int ChunkX { get; }
    public int ChunkY { get; }
    public TileMapData Tiles { get; }
    public IReadOnlyList<EntitySpawnInfo> Entities { get; }

    /// <summary>
    /// Tracks runtime modifications: destroyed objects, looted containers, dead persistent NPCs.
    /// Key = entity/container id, Value = true if modified (destroyed/looted).
    /// </summary>
    public Dictionary<string, bool> ModifiedState { get; } = new();

    public ChunkData(int chunkX, int chunkY, TileMapData? tiles = null,
        IReadOnlyList<EntitySpawnInfo>? entities = null)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        Tiles = tiles ?? new TileMapData(Size, Size);
        Entities = entities ?? Array.Empty<EntitySpawnInfo>();
    }

    /// <summary>
    /// Returns the world-space tile X origin for this chunk.
    /// </summary>
    public int WorldTileX => ChunkX * Size;

    /// <summary>
    /// Returns the world-space tile Y origin for this chunk.
    /// </summary>
    public int WorldTileY => ChunkY * Size;

    /// <summary>
    /// Gets a tile using world-space coordinates. Returns Empty if out of chunk bounds.
    /// </summary>
    public TileType GetWorldTile(int worldX, int worldY)
    {
        int localX = worldX - WorldTileX;
        int localY = worldY - WorldTileY;
        return Tiles.GetTile(localX, localY);
    }

    /// <summary>
    /// Gets rich tile data using world-space coordinates. Returns TileData.Empty if out of chunk bounds.
    /// </summary>
    public TileData GetWorldTileData(int worldX, int worldY)
    {
        int localX = worldX - WorldTileX;
        int localY = worldY - WorldTileY;
        return Tiles.GetTileData(localX, localY);
    }

    /// <summary>
    /// Marks an entity or container as modified (looted/destroyed).
    /// </summary>
    public void MarkModified(string entityId)
        => ModifiedState[entityId] = true;

    /// <summary>
    /// Checks whether an entity/container has been modified.
    /// </summary>
    public bool IsModified(string entityId)
        => ModifiedState.TryGetValue(entityId, out var modified) && modified;

    /// <summary>
    /// Creates a default chunk filled with ground tiles and no entities.
    /// </summary>
    public static ChunkData CreateDefault(int chunkX, int chunkY)
    {
        var tiles = new TileMapData(Size, Size);
        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
                tiles.SetTileData(x, y, TileDataFactory.Ground());
        return new ChunkData(chunkX, chunkY, tiles);
    }
}
