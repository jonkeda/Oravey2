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

        var worldDto = new WorldDto
        {
            Name = result.World.Name, Description = result.World.Description, Source = result.World.Source,
            ChunksWide = result.World.ChunksWide, ChunksHigh = result.World.ChunksHigh, TileSize = result.World.TileSize,
            PlayerStart = PlacementFrom(result.World.PlayerStart),
            Towns = result.World.Towns.Select(t => new TownRefDto
            {
                GameName = t.GameName, RealName = t.RealName, GameX = t.GameX, GameY = t.GameY,
                Description = t.Description, Size = t.Size, Inhabitants = t.Inhabitants, Destruction = t.Destruction,
            }).ToList(),
        };

        File.WriteAllText(
            Path.Combine(overworldDir, "world.json"),
            JsonSerializer.Serialize(worldDto, ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(overworldDir, "roads.json"),
            JsonSerializer.Serialize(result.Roads.Select(r => new RoadDto
            {
                Id = r.Id, RoadClass = r.RoadClass,
                Nodes = r.Nodes.Select(n => new[] { n.X, n.Y }).ToArray(),
                FromTown = r.FromTown, ToTown = r.ToTown,
            }).ToList(),
                ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(overworldDir, "water.json"),
            JsonSerializer.Serialize(result.Water.Select(w => new WaterDto
            {
                Id = w.Id, WaterType = w.WaterType,
                Geometry = w.Geometry.Select(v => new[] { v.X, v.Y }).ToArray(),
            }).ToList(),
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

        var townRefs = (wf?.Towns ?? []).Select(t => new OverworldTownRef
        {
            GameName = t.GameName, RealName = t.RealName, GameX = t.GameX, GameY = t.GameY,
            Description = t.Description, Size = t.Size, Inhabitants = t.Inhabitants, Destruction = t.Destruction,
        }).ToList();

        var world = new OverworldInfo
        {
            Name = wf?.Name ?? "", Description = wf?.Description ?? "", Source = wf?.Source ?? "",
            ChunksWide = wf?.ChunksWide ?? 0, ChunksHigh = wf?.ChunksHigh ?? 0, TileSize = wf?.TileSize ?? 0,
            PlayerStart = PlacementTo(wf?.PlayerStart),
            Towns = townRefs,
        };

        var roads = roadDtos.Select(r => new OverworldRoad
        {
            Id = r.Id, RoadClass = r.RoadClass,
            Nodes = r.Nodes.Select(n => new Vector2(n[0], n[1])).ToArray(),
            FromTown = r.FromTown, ToTown = r.ToTown,
        }).ToList();

        var water = waterDtos.Select(w => new OverworldWater
        {
            Id = w.Id, WaterType = w.WaterType,
            Geometry = w.Geometry.Select(v => new Vector2(v[0], v[1])).ToArray(),
        }).ToList();

        return new OverworldResult { World = world, Roads = roads, Water = water };
    }

    private static PlacementDto PlacementFrom(TilePlacement p) =>
        new() { ChunkX = p.ChunkX, ChunkY = p.ChunkY, LocalTileX = p.LocalTileX, LocalTileY = p.LocalTileY };

    private static TilePlacement PlacementTo(PlacementDto? p) =>
        p is null ? new(0, 0, 0, 0) : new(p.ChunkX, p.ChunkY, p.LocalTileX, p.LocalTileY);
}
