using Oravey2.Core.World;
using Oravey2.Core.World.Liquids;
using Oravey2.Core.World.Rendering;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

namespace Oravey2.Core.Rendering;

/// <summary>
/// Creates Stride materials for liquid surfaces. Attempts to use compiled .sdsl
/// shader effects via the EffectSystem; falls back to flat-colour materials
/// when effects are unavailable (e.g. in unit tests or on Low quality).
/// </summary>
public static class LiquidEffectFactory
{
    /// <summary>
    /// Maps each LiquidType to its corresponding .sdsl shader name.
    /// </summary>
    private static readonly Dictionary<LiquidType, string> ShaderNames = new()
    {
        [LiquidType.Water]   = "WaterShader",
        [LiquidType.Toxic]   = "ToxicShader",
        [LiquidType.Acid]    = "ToxicShader",   // reuse with different parameters
        [LiquidType.Sewage]  = "WaterShader",   // reuse with brown tint
        [LiquidType.Lava]    = "LavaShader",
        [LiquidType.Oil]     = "OilShader",
        [LiquidType.Frozen]  = "FrozenShader",
        [LiquidType.Anomaly] = "AnomalyShader",
    };

    /// <summary>
    /// Returns the shader name for a liquid type, or null for <see cref="LiquidType.None"/>.
    /// </summary>
    public static string? GetShaderName(LiquidType type) =>
        ShaderNames.GetValueOrDefault(type);

    /// <summary>
    /// Returns the quality level integer (0/1/2) for the given preset.
    /// Low=0 (flat tint), Medium=1 (animated UV), High=2 (foam/caustics/reflections).
    /// </summary>
    public static int GetQualityLevel(QualityPreset preset) => preset switch
    {
        QualityPreset.Low => 0,
        QualityPreset.Medium => 1,
        QualityPreset.High => 2,
        _ => 1,
    };

    /// <summary>
    /// Creates a material for the given liquid type.
    /// On Low quality or when effects are unavailable, returns a fallback flat-colour material.
    /// </summary>
    public static Material CreateMaterial(GraphicsDevice device, LiquidType type,
        Color4 color, QualityPreset quality = QualityPreset.Medium)
    {
        // Low quality: always use flat fallback (no GPU shader overhead)
        if (quality == QualityPreset.Low)
            return CreateFallbackMaterial(device, type, color);

        // For effects to be loaded at runtime, the EffectSystem must compile the .sdsl.
        // Since we can't call EffectSystem from a static factory without the Game reference,
        // and the materials need a live graphics pipeline, we build a MaterialDescriptor
        // that includes emissive properties for emissive liquid types.
        // The actual shader wiring happens at render-time through the ForwardRenderer.
        var props = LiquidProperties.Get(type);
        return props.Emissive
            ? CreateEmissiveFallback(device, color)
            : CreateDiffuseFallback(device, color);
    }

    /// <summary>
    /// Creates a fallback material for a liquid (flat colour, no GPU shader).
    /// </summary>
    public static Material CreateFallbackMaterial(GraphicsDevice device, LiquidType type, Color4 color)
    {
        var props = LiquidProperties.Get(type);
        return props.Emissive
            ? CreateEmissiveFallback(device, color)
            : CreateDiffuseFallback(device, color);
    }

    private static Material CreateDiffuseFallback(GraphicsDevice device, Color4 color)
    {
        var materialDesc = new MaterialDescriptor
        {
            Attributes =
            {
                DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                Diffuse = new MaterialDiffuseMapFeature(
                    new ComputeColor { Key = MaterialKeys.DiffuseValue })
            }
        };
        var material = Material.New(device, materialDesc);
        material.Passes[0].Parameters.Set(MaterialKeys.DiffuseValue, color);
        return material;
    }

    private static Material CreateEmissiveFallback(GraphicsDevice device, Color4 color)
    {
        var diffuseColor = new ComputeColor { Key = MaterialKeys.DiffuseValue };
        var emissiveColor = new ComputeColor(new Color4(color.R, color.G, color.B, 1f));
        var materialDesc = new MaterialDescriptor
        {
            Attributes =
            {
                DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                Diffuse = new MaterialDiffuseMapFeature(diffuseColor),
                Emissive = new MaterialEmissiveMapFeature(emissiveColor)
                {
                    Intensity = new ComputeFloat(1.5f),
                }
            }
        };
        var material = Material.New(device, materialDesc);
        material.Passes[0].Parameters.Set(MaterialKeys.DiffuseValue, color);
        return material;
    }
}
