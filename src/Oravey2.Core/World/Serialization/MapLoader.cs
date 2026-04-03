using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.Core.World.Serialization;

public static class MapLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static WorldJson LoadWorldJson(string mapDirectory)
    {
        var path = Path.Combine(mapDirectory, "world.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"World file not found: {path}", path);

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<WorldJson>(json, JsonOptions)
            ?? throw new JsonException("Failed to deserialize world.json: result was null.");
    }

    public static WorldMapData LoadWorld(string mapDirectory)
    {
        var worldJson = LoadWorldJson(mapDirectory);
        return new WorldMapData(worldJson.ChunksWide, worldJson.ChunksHigh);
    }

    public static ChunkData? LoadChunk(string mapDirectory, int cx, int cy)
    {
        var chunksDir = Path.Combine(mapDirectory, "chunks");
        var path = Path.Combine(chunksDir, $"{cx}_{cy}.json");
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return ChunkSerializer.DeserializeChunk(json);
    }

    public static WorldMapData LoadWorldFull(string mapDirectory)
    {
        var worldJson = LoadWorldJson(mapDirectory);
        var world = new WorldMapData(worldJson.ChunksWide, worldJson.ChunksHigh);

        var chunksDir = Path.Combine(mapDirectory, "chunks");
        if (!Directory.Exists(chunksDir))
            return world;

        for (int cx = 0; cx < worldJson.ChunksWide; cx++)
        {
            for (int cy = 0; cy < worldJson.ChunksHigh; cy++)
            {
                var path = Path.Combine(chunksDir, $"{cx}_{cy}.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var chunk = ChunkSerializer.DeserializeChunk(json);
                    world.SetChunk(cx, cy, chunk);
                }
            }
        }

        return world;
    }
}
