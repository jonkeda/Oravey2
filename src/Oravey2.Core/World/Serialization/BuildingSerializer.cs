using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.Core.World.Serialization;

public static class BuildingSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // --- Buildings ---

    public static string SerializeBuildings(IEnumerable<BuildingJson> buildings)
        => JsonSerializer.Serialize(buildings.ToArray(), JsonOptions);

    public static BuildingJson[] DeserializeBuildings(string json)
        => JsonSerializer.Deserialize<BuildingJson[]>(json, JsonOptions)
           ?? Array.Empty<BuildingJson>();

    public static BuildingDefinition FromBuildingJson(BuildingJson bj)
    {
        var footprint = bj.Footprint
            .Select(pair => (pair[0], pair[1]))
            .ToArray();

        var size = Enum.TryParse<BuildingSize>(bj.Size, true, out var s)
            ? s : BuildingSize.Small;

        return new BuildingDefinition(
            bj.Id, bj.Name, bj.MeshAsset, size,
            footprint, bj.Floors, bj.Condition, bj.InteriorChunkId);
    }

    public static BuildingJson ToBuildingJson(BuildingDefinition b, int chunkX, int chunkY)
    {
        var footprint = b.Footprint
            .Select(p => new[] { p.X, p.Y })
            .ToArray();

        // Place building at the center of its footprint
        int cx = b.Footprint.Length > 0 ? b.Footprint[0].X : 0;
        int cy = b.Footprint.Length > 0 ? b.Footprint[0].Y : 0;

        return new BuildingJson(
            b.Id, b.Name, b.MeshAssetPath, b.Size.ToString(),
            footprint, b.Floors, b.Condition, b.InteriorChunkId,
            new PlacementJson(chunkX, chunkY, cx, cy));
    }

    // --- Props ---

    public static string SerializeProps(IEnumerable<PropJson> props)
        => JsonSerializer.Serialize(props.ToArray(), JsonOptions);

    public static PropJson[] DeserializeProps(string json)
        => JsonSerializer.Deserialize<PropJson[]>(json, JsonOptions)
           ?? Array.Empty<PropJson>();

    public static PropDefinition FromPropJson(PropJson pj)
    {
        var footprint = pj.Footprint?
            .Select(pair => (pair[0], pair[1]))
            .ToArray();

        return new PropDefinition(
            pj.Id, pj.MeshAsset,
            pj.Placement.ChunkX, pj.Placement.ChunkY,
            pj.Placement.LocalTileX, pj.Placement.LocalTileY,
            pj.Rotation, pj.Scale, pj.BlocksWalkability, footprint);
    }

    public static PropJson ToPropJson(PropDefinition p)
    {
        var footprint = p.Footprint?
            .Select(fp => new[] { fp.X, fp.Y })
            .ToArray();

        return new PropJson(
            p.Id, p.MeshAssetPath,
            new PlacementJson(p.ChunkX, p.ChunkY, p.LocalTileX, p.LocalTileY),
            p.RotationDegrees, p.Scale, p.BlocksWalkability, footprint);
    }

    // --- File I/O ---

    public static BuildingJson[] LoadBuildings(string mapDirectory)
    {
        var path = Path.Combine(mapDirectory, "buildings.json");
        if (!File.Exists(path))
            return Array.Empty<BuildingJson>();
        return DeserializeBuildings(File.ReadAllText(path));
    }

    public static PropJson[] LoadProps(string mapDirectory)
    {
        var path = Path.Combine(mapDirectory, "props.json");
        if (!File.Exists(path))
            return Array.Empty<PropJson>();
        return DeserializeProps(File.ReadAllText(path));
    }

    public static void SaveBuildings(IEnumerable<BuildingJson> buildings, string mapDirectory)
    {
        Directory.CreateDirectory(mapDirectory);
        File.WriteAllText(
            Path.Combine(mapDirectory, "buildings.json"),
            SerializeBuildings(buildings));
    }

    public static void SaveProps(IEnumerable<PropJson> props, string mapDirectory)
    {
        Directory.CreateDirectory(mapDirectory);
        File.WriteAllText(
            Path.Combine(mapDirectory, "props.json"),
            SerializeProps(props));
    }
}
