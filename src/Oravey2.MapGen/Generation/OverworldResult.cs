using System.Numerics;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Output records for overworld files derived from RegionTemplate data,
/// filtered to features connecting the curated towns.
/// </summary>
public sealed record OverworldResult(
    OverworldInfo World,
    List<OverworldRoad> Roads,
    List<OverworldWater> Water);

public sealed record OverworldInfo(
    string Name,
    string Description,
    string Source,
    int ChunksWide,
    int ChunksHigh,
    int TileSize,
    TilePlacement PlayerStart,
    List<OverworldTownRef> Towns);

public sealed record OverworldTownRef(
    string GameName,
    string RealName,
    float GameX,
    float GameY,
    string Role,
    int ThreatLevel);

public sealed record OverworldRoad(
    string Id,
    string RoadClass,
    Vector2[] Nodes,
    string? FromTown,
    string? ToTown);

public sealed record OverworldWater(
    string Id,
    string WaterType,
    Vector2[] Geometry);
