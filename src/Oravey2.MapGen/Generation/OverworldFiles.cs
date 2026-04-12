using System.Numerics;
using System.Text.Json;
using Oravey2.Contracts;
using Oravey2.Contracts.ContentPack;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// JSON serialization for overworld output files: world.json, roads.json, water.json.
/// Format matches the existing portland overworld structure.
/// </summary>
public static class OverworldFiles
{
    public static void Save(OverworldResult result, string overworldDir)
    {
        Directory.CreateDirectory(overworldDir);

        var worldDto = new WorldDto(
            result.World.Name, result.World.Description, result.World.Source,
            result.World.ChunksWide, result.World.ChunksHigh, result.World.TileSize,
            PlacementFrom(result.World.PlayerStart),
            result.World.Towns.Select(t => new TownRefDto(
                t.GameName, t.RealName, t.GameX, t.GameY, t.Role, t.ThreatLevel)).ToList());

        File.WriteAllText(
            Path.Combine(overworldDir, "world.json"),
            JsonSerializer.Serialize(worldDto, ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(overworldDir, "roads.json"),
            JsonSerializer.Serialize(result.Roads.Select(r => new RoadDto(
                r.Id, r.RoadClass,
                r.Nodes.Select(n => new[] { n.X, n.Y }).ToArray(),
                r.FromTown, r.ToTown)).ToList(),
                ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(overworldDir, "water.json"),
            JsonSerializer.Serialize(result.Water.Select(w => new WaterDto(
                w.Id, w.WaterType,
                w.Geometry.Select(v => new[] { v.X, v.Y }).ToArray())).ToList(),
                ContentPackSerializer.WriteOptions));
    }

    public static OverworldResult Load(string overworldDir)
    {
        var worldJson = File.ReadAllText(Path.Combine(overworldDir, "world.json"));
        var wf = JsonSerializer.Deserialize<WorldDto>(worldJson, ContentPackSerializer.ReadOptions);

        var roadsJson = File.ReadAllText(Path.Combine(overworldDir, "roads.json"));
        var roadDtos = JsonSerializer.Deserialize<List<RoadDto>>(roadsJson, ContentPackSerializer.ReadOptions) ?? [];

        var waterJson = File.ReadAllText(Path.Combine(overworldDir, "water.json"));
        var waterDtos = JsonSerializer.Deserialize<List<WaterDto>>(waterJson, ContentPackSerializer.ReadOptions) ?? [];

        var townRefs = (wf?.Towns ?? []).Select(t =>
            new OverworldTownRef(t.GameName, t.RealName, t.GameX, t.GameY, t.Role, t.ThreatLevel)).ToList();

        var world = new OverworldInfo(
            wf?.Name ?? "", wf?.Description ?? "", wf?.Source ?? "",
            wf?.ChunksWide ?? 0, wf?.ChunksHigh ?? 0, wf?.TileSize ?? 0,
            PlacementTo(wf?.PlayerStart),
            townRefs);

        var roads = roadDtos.Select(r => new OverworldRoad(
            r.Id, r.RoadClass,
            r.Nodes.Select(n => new Vector2(n[0], n[1])).ToArray(),
            r.FromTown, r.ToTown)).ToList();

        var water = waterDtos.Select(w => new OverworldWater(
            w.Id, w.WaterType,
            w.Geometry.Select(v => new Vector2(v[0], v[1])).ToArray())).ToList();

        return new OverworldResult(world, roads, water);
    }

    private static PlacementDto PlacementFrom(TilePlacement p) =>
        new(p.ChunkX, p.ChunkY, p.LocalTileX, p.LocalTileY);

    private static TilePlacement PlacementTo(PlacementDto? p) =>
        p is null ? new(0, 0, 0, 0) : new(p.ChunkX, p.ChunkY, p.LocalTileX, p.LocalTileY);
}
