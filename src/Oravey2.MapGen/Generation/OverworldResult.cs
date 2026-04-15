using System.Numerics;
using Oravey2.Contracts.ContentPack;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Output classes for overworld files derived from RegionTemplate data,
/// filtered to features connecting the curated towns.
/// </summary>
public sealed class OverworldResult
{
    public WorldDto World { get; set; } = new();
    public List<OverworldRoad> Roads { get; set; } = [];
    public List<OverworldWater> Water { get; set; } = [];
}

public sealed class OverworldRoad
{
    public string Id { get; set; } = "";
    public string RoadClass { get; set; } = "";
    public Vector2[] Nodes { get; set; } = [];
    public string? FromTown { get; set; }
    public string? ToTown { get; set; }
}

public sealed class OverworldWater
{
    public string Id { get; set; } = "";
    public string WaterType { get; set; } = "";
    public Vector2[] Geometry { get; set; } = [];
}
