using System.Numerics;

namespace Oravey2.MapGen.RegionTemplates;

public record TownEntry(
    string Name,
    double Latitude,
    double Longitude,
    int Population,
    Vector2 GamePosition,
    TownCategory Category,
    Vector2[]? BoundaryPolygon = null);
