namespace Oravey2.Core.World.Liquids;

/// <summary>
/// Visual and gameplay properties for each liquid type.
/// </summary>
public readonly record struct LiquidPropertySet(
    float Opacity,
    float FlowSpeed,
    bool Emissive,
    float ColorR,
    float ColorG,
    float ColorB,
    float DamagePerSecond);

public static class LiquidProperties
{
    private static readonly Dictionary<LiquidType, LiquidPropertySet> _properties = new()
    {
        [LiquidType.Water]   = new(0.6f,  0.5f,  false, 0.15f, 0.40f, 0.70f, 0f),
        [LiquidType.Toxic]   = new(0.8f,  0.2f,  true,  0.20f, 0.75f, 0.10f, 5f),
        [LiquidType.Acid]    = new(0.7f,  0.3f,  true,  0.50f, 0.80f, 0.05f, 10f),
        [LiquidType.Sewage]  = new(0.9f,  0.1f,  false, 0.30f, 0.25f, 0.15f, 2f),
        [LiquidType.Lava]    = new(1.0f,  0.05f, true,  0.95f, 0.30f, 0.05f, 25f),
        [LiquidType.Oil]     = new(0.95f, 0.0f,  false, 0.10f, 0.08f, 0.05f, 0f),
        [LiquidType.Frozen]  = new(0.5f,  0.0f,  false, 0.70f, 0.85f, 0.95f, 0f),
        [LiquidType.Anomaly] = new(0.4f,  1.0f,  true,  0.60f, 0.10f, 0.80f, 15f),
    };

    public static LiquidPropertySet Get(LiquidType type)
    {
        if (_properties.TryGetValue(type, out var props))
            return props;
        return default;
    }

    public static bool HasProperties(LiquidType type) => _properties.ContainsKey(type);
}
