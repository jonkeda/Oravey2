using System.Numerics;

namespace Oravey2.Core.World.LinearFeatures;

/// <summary>
/// Maps linear feature types and styles to visual properties (colour, surface type overrides).
/// </summary>
public static class LinearFeatureStyles
{
    /// <summary>
    /// Returns an RGBA colour for the given feature type and style.
    /// Used by the renderer to create per-feature materials.
    /// </summary>
    public static (float R, float G, float B, float A) GetColor(LinearFeatureType type, string style)
    {
        return type switch
        {
            LinearFeatureType.Path => (0.55f, 0.40f, 0.25f, 1f),        // dirt path
            LinearFeatureType.Residential => (0.50f, 0.35f, 0.18f, 1f), // residential road
            LinearFeatureType.Tertiary => (0.45f, 0.42f, 0.38f, 1f),    // tertiary road
            LinearFeatureType.Secondary => style switch
            {
                "concrete" => (0.90f, 0.90f, 0.80f, 1f),               // bright concrete
                _ => (0.35f, 0.35f, 0.38f, 1f),                        // asphalt grey
            },
            LinearFeatureType.Primary => (0.35f, 0.35f, 0.38f, 1f),    // asphalt grey
            LinearFeatureType.Trunk => (0.32f, 0.32f, 0.35f, 1f),      // dark asphalt
            LinearFeatureType.Motorway => (0.30f, 0.30f, 0.33f, 1f),   // dark asphalt
            LinearFeatureType.Rail => (0.50f, 0.45f, 0.35f, 1f),       // gravel ballast
            LinearFeatureType.River => (0.20f, 0.35f, 0.55f, 1f),      // murky water
            LinearFeatureType.Stream => (0.25f, 0.40f, 0.55f, 1f),     // shallow water
            LinearFeatureType.Canal => (0.18f, 0.30f, 0.50f, 1f),      // canal water
            LinearFeatureType.Pipeline => (0.45f, 0.47f, 0.50f, 1f),   // metal grey
            _ => (0.50f, 0.50f, 0.50f, 1f),
        };
    }

    /// <summary>
    /// Returns the surface type that a road-class feature stamps onto the splat map.
    /// Returns null for features that don't override surface type (rivers, rails, etc.).
    /// </summary>
    public static SurfaceType? GetSplatOverride(LinearFeatureType type, string style)
    {
        return type switch
        {
            LinearFeatureType.Secondary => style == "concrete" ? SurfaceType.Concrete : SurfaceType.Asphalt,
            LinearFeatureType.Primary or LinearFeatureType.Trunk => SurfaceType.Asphalt,
            LinearFeatureType.Motorway => SurfaceType.Asphalt,
            LinearFeatureType.Residential => SurfaceType.Dirt,
            LinearFeatureType.Path => SurfaceType.Dirt,
            _ => null,
        };
    }

    /// <summary>
    /// Returns true if the feature type represents water and should use a water-surface shader.
    /// </summary>
    public static bool IsWaterFeature(LinearFeatureType type)
        => type is LinearFeatureType.River or LinearFeatureType.Stream or LinearFeatureType.Canal;

    /// <summary>
    /// Returns true if the feature type is a rail feature (needs ballast + optional sleepers/rails).
    /// </summary>
    public static bool IsRailFeature(LinearFeatureType type)
        => type is LinearFeatureType.Rail;
}
