using System.Numerics;

namespace Oravey2.MapGen.WorldTemplate;

public enum WaterType
{
    Lake,
    River,
    Canal,
    Sea
}

public record WaterBody(WaterType Type, Vector2[] Geometry);
