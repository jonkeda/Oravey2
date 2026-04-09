using System.Numerics;

namespace Oravey2.MapGen.RegionTemplates;

public enum LandUseType
{
    Forest,
    Farmland,
    Residential,
    Industrial,
    Commercial,
    Meadow,
    Orchard,
    Cemetery,
    Military,
    Other
}

public record LandUseZone(LandUseType Type, Vector2[] Polygon);
