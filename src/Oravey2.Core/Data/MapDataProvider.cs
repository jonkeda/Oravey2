using Oravey2.Core.World;

namespace Oravey2.Core.Data;

public sealed class MapDataProvider
{
    private readonly WorldMapStore _world;
    private readonly SaveStateStore? _save;

    public MapDataProvider(WorldMapStore world, SaveStateStore? save = null)
    {
        _world = world;
        _save = save;
    }

    public ChunkData? GetChunkData(long regionId, int gridX, int gridY)
    {
        var chunkRec = _world.GetChunkByGrid(regionId, gridX, gridY);
        if (chunkRec is null) return null;

        // Deserialize world tile data
        var tileGrid = TileDataSerializer.DeserializeTileGrid(
            chunkRec.TileData, ChunkData.Size, ChunkData.Size);

        // Apply save delta if present
        if (_save is not null)
        {
            var state = _save.GetChunkState(regionId, gridX, gridY);
            if (state is { TileOverrides: { } overrides })
            {
                var overrideGrid = TileDataSerializer.DeserializeTileGrid(
                    overrides, ChunkData.Size, ChunkData.Size);

                for (int x = 0; x < ChunkData.Size; x++)
                    for (int y = 0; y < ChunkData.Size; y++)
                    {
                        if (overrideGrid[x, y] != TileData.Empty)
                            tileGrid[x, y] = overrideGrid[x, y];
                    }
            }
        }

        // Build TileMapData from grid
        var tileMap = new TileMapData(ChunkData.Size, ChunkData.Size);
        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                tileMap.SetTileData(x, y, tileGrid[x, y]);

        // Load entity spawns
        var entities = _world.GetEntitySpawns(chunkRec.Id);

        // Load terrain modifiers
        var modifiers = _world.GetTerrainModifiers(chunkRec.Id);

        return new ChunkData(
            chunkX: gridX,
            chunkY: gridY,
            tiles: tileMap,
            entities: entities,
            mode: chunkRec.Mode,
            layer: chunkRec.Layer,
            terrainModifiers: modifiers);
    }
}
