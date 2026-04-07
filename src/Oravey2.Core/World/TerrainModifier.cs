using System.Numerics;

namespace Oravey2.Core.World;

public abstract record TerrainModifier;

public sealed record FlattenStrip(
    IReadOnlyList<Vector2> CentreLine,
    float Width,
    float TargetHeight) : TerrainModifier;

public sealed record ChannelCut(
    IReadOnlyList<Vector2> CentreLine,
    float Width,
    float Depth) : TerrainModifier;

public sealed record LevelRect(
    Vector2 Min,
    Vector2 Max,
    float TargetHeight) : TerrainModifier;

public sealed record Crater(
    Vector2 Centre,
    float Radius,
    float Depth) : TerrainModifier;
