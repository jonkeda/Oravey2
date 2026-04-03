using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.Core.World.Serialization;

public static class ChunkSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SerializeChunk(ChunkData chunk)
    {
        var json = ToChunkJson(chunk);
        return JsonSerializer.Serialize(json, JsonOptions);
    }

    public static ChunkData DeserializeChunk(string json)
    {
        var chunkJson = JsonSerializer.Deserialize<ChunkJson>(json, JsonOptions)
            ?? throw new JsonException("Failed to deserialize chunk JSON: result was null.");
        return FromChunkJson(chunkJson);
    }

    public static void SaveChunk(ChunkData chunk, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{chunk.ChunkX}_{chunk.ChunkY}.json");
        var json = SerializeChunk(chunk);
        File.WriteAllText(path, json);
    }

    public static ChunkData LoadChunk(string directory, int cx, int cy)
    {
        var path = Path.Combine(directory, $"{cx}_{cy}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Chunk file not found: {path}", path);
        var json = File.ReadAllText(path);
        return DeserializeChunk(json);
    }

    internal static ChunkJson ToChunkJson(ChunkData chunk)
    {
        int width = chunk.Tiles.Width;
        int height = chunk.Tiles.Height;
        var tiles = chunk.Tiles;

        var surface = new int[height][];
        var heightArr = new int[height][];
        var water = new int[height][];
        var structure = new int[height][];
        var flags = new int[height][];
        var variant = new int[height][];

        for (int y = 0; y < height; y++)
        {
            surface[y] = new int[width];
            heightArr[y] = new int[width];
            water[y] = new int[width];
            structure[y] = new int[width];
            flags[y] = new int[width];
            variant[y] = new int[width];

            for (int x = 0; x < width; x++)
            {
                var td = tiles.GetTileData(x, y);
                surface[y][x] = (byte)td.Surface;
                heightArr[y][x] = td.HeightLevel;
                water[y][x] = td.WaterLevel;
                structure[y][x] = td.StructureId;
                flags[y][x] = (byte)td.Flags;
                variant[y][x] = td.VariantSeed;
            }
        }

        EntitySpawnJson[]? entities = chunk.Entities.Count > 0
            ? chunk.Entities.Select(e => new EntitySpawnJson(
                e.PrefabId, e.LocalX, e.LocalZ, e.RotationY,
                e.Faction, e.Level, e.DialogueId, e.LootTable,
                e.Persistent, e.ConditionFlag)).ToArray()
            : null;

        return new ChunkJson(chunk.ChunkX, chunk.ChunkY,
            surface, heightArr, water, structure, flags, variant, entities);
    }

    internal static ChunkData FromChunkJson(ChunkJson cj)
    {
        int height = cj.Surface.Length;
        int width = height > 0 ? cj.Surface[0].Length : 0;
        var tiles = new TileMapData(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var td = new TileData(
                    (SurfaceType)cj.Surface[y][x],
                    (byte)cj.Height[y][x],
                    (byte)cj.Water[y][x],
                    cj.Structure[y][x],
                    (TileFlags)cj.Flags[y][x],
                    (byte)cj.Variant[y][x]);
                tiles.SetTileData(x, y, td);
            }
        }

        var entities = cj.Entities?.Select(e => new EntitySpawnInfo(
            e.PrefabId, e.LocalX, e.LocalZ, e.RotationY,
            e.Faction, e.Level, e.DialogueId, e.LootTable,
            e.Persistent, e.ConditionFlag)).ToList()
            ?? new List<EntitySpawnInfo>();

        return new ChunkData(cj.ChunkX, cj.ChunkY, tiles, entities);
    }
}
