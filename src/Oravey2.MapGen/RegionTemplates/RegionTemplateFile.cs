using System.Numerics;

namespace Oravey2.MapGen.RegionTemplates;

public class RegionTemplateFile
{
    public const int FormatVersion = 2;

    public required string Name { get; init; }
    public required double OriginLatitude { get; init; }
    public required double OriginLongitude { get; init; }
    public List<Vector2[]> ContinentOutlines { get; init; } = [];
    public List<RegionTemplate> Regions { get; init; } = [];
}
