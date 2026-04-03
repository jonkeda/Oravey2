using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.Core.World.Serialization;

public static class MapExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void ExportChunk(ChunkData chunk, string directory)
    {
        var chunksDir = Path.Combine(directory, "chunks");
        ChunkSerializer.SaveChunk(chunk, chunksDir);
    }

    public static void ExportWorld(WorldMapData world, string directory, float tileSize = 1f,
        PlayerStartJson? playerStart = null, string? defaultWeather = null)
    {
        Directory.CreateDirectory(directory);

        var worldJson = new WorldJson(
            world.ChunksWide,
            world.ChunksHigh,
            tileSize,
            playerStart ?? new PlayerStartJson(0, 0, 0, 0),
            defaultWeather);

        var json = JsonSerializer.Serialize(worldJson, JsonOptions);
        File.WriteAllText(Path.Combine(directory, "world.json"), json);

        foreach (var chunk in world.GetAllChunks())
        {
            ExportChunk(chunk, directory);
        }
    }

    public static void ExportBuilderMap(TileMapData map, int cx, int cy, string directory)
    {
        var chunk = new ChunkData(cx, cy, map);
        ExportChunk(chunk, directory);
    }
}
