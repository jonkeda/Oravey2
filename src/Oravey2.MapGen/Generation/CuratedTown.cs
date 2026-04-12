using System.Numerics;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

public sealed record CuratedTown(
    string GameName,
    string RealName,
    double Latitude,
    double Longitude,
    Vector2 GamePosition,
    string Description,
    TownCategory Size,
    int Inhabitants,
    DestructionLevel Destruction,
    Vector2[]? BoundaryPolygon = null);
