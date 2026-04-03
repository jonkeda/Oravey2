using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.Core.World.Blueprint;

public static class BlueprintLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static MapBlueprint Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Blueprint file not found: {path}", path);
        return LoadFromString(File.ReadAllText(path));
    }

    public static MapBlueprint LoadFromString(string json)
    {
        return JsonSerializer.Deserialize<MapBlueprint>(json, JsonOptions)
               ?? throw new JsonException("Failed to deserialize blueprint: result was null.");
    }

    public static ValidationResult Validate(MapBlueprint blueprint)
        => BlueprintValidator.Validate(blueprint);

    internal static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
