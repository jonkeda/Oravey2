using System.Numerics;

namespace Oravey2.MapGen.WorldTemplate;

public enum RoadClass
{
    Motorway,
    Trunk,
    Primary,
    Secondary,
    Tertiary,
    Residential
}

public record RoadSegment(RoadClass RoadClass, Vector2[] Nodes);
