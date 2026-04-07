using System.Numerics;

namespace Oravey2.MapGen.Generation;

public sealed record CuratedRegion(
    string Name,
    Vector2 BoundsMin,
    Vector2 BoundsMax,
    List<CuratedTown> Towns);
