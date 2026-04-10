using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// JSON serialization for overworld output files: world.json, roads.json, water.json.
/// Format matches the existing portland overworld structure.
/// </summary>
public static class OverworldFiles
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Save(OverworldResult result, string overworldDir)
    {
        Directory.CreateDirectory(overworldDir);

        File.WriteAllText(
            Path.Combine(overworldDir, "world.json"),
            JsonSerializer.Serialize(new WorldFile
            {
                Name = result.World.Name,
                Description = result.World.Description,
                Source = result.World.Source,
                ChunksWide = result.World.ChunksWide,
                ChunksHigh = result.World.ChunksHigh,
                TileSize = result.World.TileSize,
                PlayerStart = PlacementFrom(result.World.PlayerStart),
                Towns = result.World.Towns.Select(t => new TownRefFile
                {
                    GameName = t.GameName,
                    RealName = t.RealName,
                    GameX = t.GameX,
                    GameY = t.GameY,
                    Role = t.Role,
                    ThreatLevel = t.ThreatLevel,
                }).ToList(),
            }, Options));

        File.WriteAllText(
            Path.Combine(overworldDir, "roads.json"),
            JsonSerializer.Serialize(result.Roads.Select(r => new RoadFile
            {
                Id = r.Id,
                RoadClass = r.RoadClass,
                Nodes = r.Nodes.Select(n => new[] { n.X, n.Y }).ToArray(),
                FromTown = r.FromTown,
                ToTown = r.ToTown,
            }).ToList(), Options));

        File.WriteAllText(
            Path.Combine(overworldDir, "water.json"),
            JsonSerializer.Serialize(result.Water.Select(w => new WaterFile
            {
                Id = w.Id,
                WaterType = w.WaterType,
                Geometry = w.Geometry.Select(v => new[] { v.X, v.Y }).ToArray(),
            }).ToList(), Options));
    }

    public static OverworldResult Load(string overworldDir)
    {
        var worldJson = File.ReadAllText(Path.Combine(overworldDir, "world.json"));
        var wf = JsonSerializer.Deserialize<WorldFile>(worldJson, Options) ?? new();

        var roadsJson = File.ReadAllText(Path.Combine(overworldDir, "roads.json"));
        var roadFiles = JsonSerializer.Deserialize<List<RoadFile>>(roadsJson, Options) ?? [];

        var waterJson = File.ReadAllText(Path.Combine(overworldDir, "water.json"));
        var waterFiles = JsonSerializer.Deserialize<List<WaterFile>>(waterJson, Options) ?? [];

        var townRefs = wf.Towns.Select(t =>
            new OverworldTownRef(t.GameName, t.RealName, t.GameX, t.GameY, t.Role, t.ThreatLevel)).ToList();

        var world = new OverworldInfo(
            wf.Name, wf.Description, wf.Source,
            wf.ChunksWide, wf.ChunksHigh, wf.TileSize,
            PlacementTo(wf.PlayerStart),
            townRefs);

        var roads = roadFiles.Select(r => new OverworldRoad(
            r.Id, r.RoadClass,
            r.Nodes.Select(n => new Vector2(n[0], n[1])).ToArray(),
            r.FromTown, r.ToTown)).ToList();

        var water = waterFiles.Select(w => new OverworldWater(
            w.Id, w.WaterType,
            w.Geometry.Select(v => new Vector2(v[0], v[1])).ToArray())).ToList();

        return new OverworldResult(world, roads, water);
    }

    private static OverworldPlacementFile PlacementFrom(TilePlacement p) => new()
    {
        ChunkX = p.ChunkX,
        ChunkY = p.ChunkY,
        LocalTileX = p.LocalTileX,
        LocalTileY = p.LocalTileY,
    };

    private static TilePlacement PlacementTo(OverworldPlacementFile? p) =>
        p is null ? new(0, 0, 0, 0) : new(p.ChunkX, p.ChunkY, p.LocalTileX, p.LocalTileY);
}

// --- JSON DTO classes matching portland overworld format ---

internal sealed class WorldFile
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Source { get; set; } = "";
    public int ChunksWide { get; set; }
    public int ChunksHigh { get; set; }
    public int TileSize { get; set; }
    public OverworldPlacementFile? PlayerStart { get; set; }
    public List<TownRefFile> Towns { get; set; } = [];
}

internal sealed class TownRefFile
{
    public string GameName { get; set; } = "";
    public string RealName { get; set; } = "";
    public float GameX { get; set; }
    public float GameY { get; set; }
    public string Role { get; set; } = "";
    public int ThreatLevel { get; set; }
}

internal sealed class RoadFile
{
    public string Id { get; set; } = "";
    public string RoadClass { get; set; } = "";
    public float[][] Nodes { get; set; } = [];
    public string? FromTown { get; set; }
    public string? ToTown { get; set; }
}

internal sealed class WaterFile
{
    public string Id { get; set; } = "";
    public string WaterType { get; set; } = "";
    public float[][] Geometry { get; set; } = [];
}

internal sealed class OverworldPlacementFile
{
    public int ChunkX { get; set; }
    public int ChunkY { get; set; }
    public int LocalTileX { get; set; }
    public int LocalTileY { get; set; }
}
