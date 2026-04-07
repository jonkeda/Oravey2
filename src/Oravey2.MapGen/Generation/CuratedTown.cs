using System.Numerics;

namespace Oravey2.MapGen.Generation;

public sealed record CuratedTown(
    string GameName,
    string RealName,
    double Latitude,
    double Longitude,
    Vector2 GamePosition,
    string Role,
    string Faction,
    int ThreatLevel,
    string Description,
    Vector2[]? BoundaryPolygon = null);
