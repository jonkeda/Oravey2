using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Handles reading and writing of asset metadata (.meta.json) and updating
/// building mesh references after asset acceptance.
/// </summary>
public static class AssetFiles
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void SaveMeta(AssetMeta meta, string metaPath)
    {
        var dir = Path.GetDirectoryName(metaPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, Options));
    }

    public static AssetMeta LoadMeta(string metaPath) =>
        JsonSerializer.Deserialize<AssetMeta>(File.ReadAllText(metaPath), Options) ?? new();

    /// <summary>
    /// Updates buildings.json in a town directory, replacing MeshAsset for a building
    /// matching the given name with the new mesh path.
    /// </summary>
    public static void UpdateBuildingMeshReference(
        string townDir, string buildingName, string newMeshPath)
    {
        var buildingsPath = Path.Combine(townDir, "buildings.json");
        if (!File.Exists(buildingsPath)) return;

        var json = File.ReadAllText(buildingsPath);
        var buildings = JsonSerializer.Deserialize<List<BuildingRefDto>>(json, Options) ?? [];

        var updated = false;
        foreach (var b in buildings)
        {
            if (string.Equals(b.Name, buildingName, StringComparison.OrdinalIgnoreCase))
            {
                b.MeshAsset = newMeshPath;
                updated = true;
            }
        }

        if (updated)
            File.WriteAllText(buildingsPath, JsonSerializer.Serialize(buildings, Options));
    }
}

public sealed class AssetMeta
{
    public string AssetId { get; set; } = "";
    public string MeshyTaskId { get; set; } = "";
    public string Prompt { get; set; } = "";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "accepted";
    public string SourceType { get; set; } = "text-to-3d";
    public string SizeCategory { get; set; } = "";
}

internal sealed class BuildingRefDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string MeshAsset { get; set; } = "";
    public string Size { get; set; } = "";
    public int[][]? Footprint { get; set; }
    public int Floors { get; set; }
    public float Condition { get; set; }
    public PlacementRefDto? Placement { get; set; }
}

internal sealed class PlacementRefDto
{
    public int ChunkX { get; set; }
    public int ChunkY { get; set; }
    public int LocalTileX { get; set; }
    public int LocalTileY { get; set; }
}
