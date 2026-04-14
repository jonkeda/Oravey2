using System.Text.Json;
using Oravey2.Contracts;
using Oravey2.Contracts.ContentPack;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Handles reading and writing of asset metadata (.meta.json) and updating
/// building mesh references after asset acceptance.
/// </summary>
public static class AssetFiles
{
    public static void SaveMeta(AssetMeta meta, string metaPath)
    {
        var dir = Path.GetDirectoryName(metaPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, ContentPackSerializer.WriteOptions));
    }

    public static AssetMeta LoadMeta(string metaPath) =>
        JsonSerializer.Deserialize<AssetMeta>(File.ReadAllText(metaPath), ContentPackSerializer.ReadOptions) ?? new();

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
        var buildings = JsonSerializer.Deserialize<List<BuildingDto>>(json, ContentPackSerializer.ReadOptions) ?? [];

        var updated = false;
        var updatedBuildings = buildings.Select(b =>
        {
            if (string.Equals(b.Name, buildingName, StringComparison.OrdinalIgnoreCase))
            {
                updated = true;
                return new BuildingDto
                {
                    Id = b.Id, Name = b.Name, MeshAsset = newMeshPath, Size = b.Size,
                    Footprint = b.Footprint, Floors = b.Floors, Condition = b.Condition,
                    InteriorChunkId = b.InteriorChunkId, Placement = b.Placement,
                };
            }
            return b;
        }).ToList();

        if (updated)
            File.WriteAllText(buildingsPath, JsonSerializer.Serialize(updatedBuildings, ContentPackSerializer.WriteOptions));
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
