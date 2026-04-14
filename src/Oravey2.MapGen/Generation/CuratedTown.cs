using System.Numerics;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

public sealed class CuratedTown
{
    public string GameName { get; set; } = "";
    public string RealName { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public Vector2 GamePosition { get; set; }
    public string Description { get; set; } = "";
    public TownCategory Size { get; set; }
    public int Inhabitants { get; set; }
    public DestructionLevel Destruction { get; set; }
    public Vector2[]? BoundaryPolygon { get; set; }
}
