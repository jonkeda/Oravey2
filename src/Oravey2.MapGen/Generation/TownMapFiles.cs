using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// JSON serialization for town map output files: layout.json, buildings.json,
/// props.json, and zones.json — matching the existing portland map format.
/// </summary>
public static class TownMapFiles
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Save(TownMapResult result, string townDir)
    {
        Directory.CreateDirectory(townDir);

        File.WriteAllText(
            Path.Combine(townDir, "layout.json"),
            JsonSerializer.Serialize(new LayoutFile
            {
                Width = result.Layout.Width,
                Height = result.Layout.Height,
                Surface = result.Layout.Surface,
            }, Options));

        File.WriteAllText(
            Path.Combine(townDir, "buildings.json"),
            JsonSerializer.Serialize(result.Buildings.Select(b => new BuildingFile
            {
                Id = b.Id,
                Name = b.Name,
                MeshAsset = b.MeshAsset,
                Size = b.SizeCategory,
                Footprint = b.Footprint,
                Floors = b.Floors,
                Condition = b.Condition,
                Placement = PlacementFrom(b.Placement),
            }).ToList(), Options));

        File.WriteAllText(
            Path.Combine(townDir, "props.json"),
            JsonSerializer.Serialize(result.Props.Select(p => new PropFile
            {
                Id = p.Id,
                MeshAsset = p.MeshAsset,
                Placement = PlacementFrom(p.Placement),
                Rotation = p.Rotation,
                Scale = p.Scale,
                BlocksWalkability = p.BlocksWalkability,
            }).ToList(), Options));

        File.WriteAllText(
            Path.Combine(townDir, "zones.json"),
            JsonSerializer.Serialize(result.Zones.Select(z => new ZoneFile
            {
                Id = z.Id,
                Name = z.Name,
                Biome = z.Biome,
                RadiationLevel = z.RadiationLevel,
                EnemyDifficultyTier = z.EnemyDifficultyTier,
                IsFastTravelTarget = z.IsFastTravelTarget,
                ChunkStartX = z.ChunkStartX,
                ChunkStartY = z.ChunkStartY,
                ChunkEndX = z.ChunkEndX,
                ChunkEndY = z.ChunkEndY,
            }).ToList(), Options));
    }

    public static TownMapResult Load(string townDir)
    {
        var layoutJson = File.ReadAllText(Path.Combine(townDir, "layout.json"));
        var layoutFile = JsonSerializer.Deserialize<LayoutFile>(layoutJson, Options) ?? new();

        var buildingsJson = File.ReadAllText(Path.Combine(townDir, "buildings.json"));
        var buildingFiles = JsonSerializer.Deserialize<List<BuildingFile>>(buildingsJson, Options) ?? [];

        var propsJson = File.ReadAllText(Path.Combine(townDir, "props.json"));
        var propFiles = JsonSerializer.Deserialize<List<PropFile>>(propsJson, Options) ?? [];

        var zonesJson = File.ReadAllText(Path.Combine(townDir, "zones.json"));
        var zoneFiles = JsonSerializer.Deserialize<List<ZoneFile>>(zonesJson, Options) ?? [];

        var layout = new TownLayout(layoutFile.Width, layoutFile.Height, layoutFile.Surface ?? []);

        var buildings = buildingFiles.Select(b => new PlacedBuilding(
            b.Id, b.Name, b.MeshAsset, b.Size,
            b.Footprint ?? [],
            b.Floors, b.Condition,
            PlacementTo(b.Placement))).ToList();

        var props = propFiles.Select(p => new PlacedProp(
            p.Id, p.MeshAsset, PlacementTo(p.Placement),
            p.Rotation, p.Scale, p.BlocksWalkability)).ToList();

        var zones = zoneFiles.Select(z => new TownZone(
            z.Id, z.Name, z.Biome, z.RadiationLevel,
            z.EnemyDifficultyTier, z.IsFastTravelTarget,
            z.ChunkStartX, z.ChunkStartY,
            z.ChunkEndX, z.ChunkEndY)).ToList();

        return new TownMapResult(layout, buildings, props, zones);
    }

    private static PlacementFile PlacementFrom(TilePlacement p) => new()
    {
        ChunkX = p.ChunkX,
        ChunkY = p.ChunkY,
        LocalTileX = p.LocalTileX,
        LocalTileY = p.LocalTileY,
    };

    private static TilePlacement PlacementTo(PlacementFile? p) =>
        p is null ? new(0, 0, 0, 0) : new(p.ChunkX, p.ChunkY, p.LocalTileX, p.LocalTileY);
}

// --- JSON DTO classes matching existing portland format ---

internal sealed class LayoutFile
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int[][]? Surface { get; set; }
}

internal sealed class BuildingFile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string MeshAsset { get; set; } = "";
    public string Size { get; set; } = "";
    public int[][]? Footprint { get; set; }
    public int Floors { get; set; }
    public float Condition { get; set; }
    public PlacementFile? Placement { get; set; }
}

internal sealed class PropFile
{
    public string Id { get; set; } = "";
    public string MeshAsset { get; set; } = "";
    public PlacementFile? Placement { get; set; }
    public float Rotation { get; set; }
    public float Scale { get; set; }
    public bool BlocksWalkability { get; set; }
}

internal sealed class ZoneFile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Biome { get; set; }
    public float RadiationLevel { get; set; }
    public int EnemyDifficultyTier { get; set; }
    public bool IsFastTravelTarget { get; set; }
    public int ChunkStartX { get; set; }
    public int ChunkStartY { get; set; }
    public int ChunkEndX { get; set; }
    public int ChunkEndY { get; set; }
}

internal sealed class PlacementFile
{
    public int ChunkX { get; set; }
    public int ChunkY { get; set; }
    public int LocalTileX { get; set; }
    public int LocalTileY { get; set; }
}
