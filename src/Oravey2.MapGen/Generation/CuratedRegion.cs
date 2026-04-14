using System.Numerics;

namespace Oravey2.MapGen.Generation;

public sealed class CuratedRegion
{
    public string Name { get; set; } = "";
    public Vector2 BoundsMin { get; set; }
    public Vector2 BoundsMax { get; set; }
    public List<CuratedTown> Towns { get; set; } = [];
}
