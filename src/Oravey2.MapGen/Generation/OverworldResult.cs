using System.Numerics;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Output classes for overworld files derived from RegionTemplate data,
/// filtered to features connecting the curated towns.
/// </summary>
public sealed class OverworldResult
{
    public OverworldInfo World { get; set; } = new();
    public List<OverworldRoad> Roads { get; set; } = [];
    public List<OverworldWater> Water { get; set; } = [];
}

public sealed class OverworldInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Source { get; set; } = "";
    public int ChunksWide { get; set; }
    public int ChunksHigh { get; set; }
    public int TileSize { get; set; }
    public TilePlacement PlayerStart { get; set; } = new(0, 0, 0, 0);
    public List<OverworldTownRef> Towns { get; set; } = [];
}

public sealed class OverworldTownRef
{
    public string GameName { get; set; } = "";
    public string RealName { get; set; } = "";
    public float GameX { get; set; }
    public float GameY { get; set; }
    public string Description { get; set; } = "";
    public string Size { get; set; } = "";
    public int Inhabitants { get; set; }
    public string Destruction { get; set; } = "";
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
