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

        File.WriteAllText(
            Path.Combine(overworldDir, "world.json"),
            JsonSerializer.Serialize(result.World, ContentPackSerializer.WriteOptions));

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
        var world = JsonSerializer.Deserialize<WorldDto>(worldJson, ContentPackSerializer.ReadOptions) ?? new();

        var roadsJson = File.ReadAllText(Path.Combine(overworldDir, "roads.json"));
        var roadDtos = JsonSerializer.Deserialize<List<RoadDto>>(roadsJson, ContentPackSerializer.ReadOptions) ?? [];

        var waterJson = File.ReadAllText(Path.Combine(overworldDir, "water.json"));
        var waterDtos = JsonSerializer.Deserialize<List<WaterDto>>(waterJson, ContentPackSerializer.ReadOptions) ?? [];

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
}
