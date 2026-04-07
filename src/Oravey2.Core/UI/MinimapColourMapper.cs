using Oravey2.Core.World;

namespace Oravey2.Core.UI;

/// <summary>
/// Maps <see cref="SurfaceType"/> values to ARGB minimap colours and applies
/// simple hillshade shading for a topographic feel.
/// </summary>
public static class MinimapColourMapper
{
    /// <summary>ARGB colour for each surface type.</summary>
    private static readonly Dictionary<SurfaceType, uint> _colours = new()
    {
        [SurfaceType.Grass]    = 0xFF4CAF50, // green
        [SurfaceType.Concrete] = 0xFF9E9E9E, // grey
        [SurfaceType.Sand]     = 0xFFD2B48C, // tan
        [SurfaceType.Dirt]     = 0xFF8B4513, // brown
        [SurfaceType.Rock]     = 0xFF757575, // dark grey (mapped as Gravel-like)
        [SurfaceType.Mud]      = 0xFF6B8E23, // olive (Swamp-like)
        [SurfaceType.Asphalt]  = 0xFF424242, // dark grey (roads)
        [SurfaceType.Metal]    = 0xFFB0BEC5, // light grey
    };

    /// <summary>Water colour (used when a tile is flagged as water).</summary>
    public static uint WaterColour => 0xFF2196F3; // blue

    /// <summary>Snow colour overlay.</summary>
    public static uint SnowColour => 0xFFFFFFFF; // white

    /// <summary>
    /// Returns the ARGB minimap colour for a given surface type.
    /// </summary>
    public static uint GetColour(SurfaceType surface)
        => _colours.TryGetValue(surface, out var c) ? c : 0xFFFF00FF; // magenta fallback

    /// <summary>
    /// Returns whether every <see cref="SurfaceType"/> value has a mapped colour.
    /// </summary>
    public static bool AllTypesMapped()
    {
        foreach (SurfaceType st in Enum.GetValues<SurfaceType>())
        {
            if (!_colours.ContainsKey(st))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Applies a simple NW hillshade modifier to a base colour.
    /// <paramref name="slope"/> ranges from -1 (facing away from light) to +1 (facing light).
    /// Returns the shaded ARGB colour.
    /// </summary>
    public static uint ApplyHillshade(uint baseColour, float slope)
    {
        // Clamp slope to [-1, 1]
        slope = Math.Clamp(slope, -1f, 1f);

        // Map slope to brightness multiplier: 0.6 (shadow) to 1.2 (highlight)
        float brightness = 0.9f + 0.3f * slope;

        byte a = (byte)((baseColour >> 24) & 0xFF);
        byte r = (byte)Math.Clamp((int)(((baseColour >> 16) & 0xFF) * brightness), 0, 255);
        byte g = (byte)Math.Clamp((int)(((baseColour >> 8) & 0xFF) * brightness), 0, 255);
        byte b = (byte)Math.Clamp((int)((baseColour & 0xFF) * brightness), 0, 255);

        return (uint)(a << 24 | r << 16 | g << 8 | b);
    }

    /// <summary>
    /// Computes a simple NW hillshade slope from a 3×3 height neighbourhood.
    /// Heights: [NW, N, NE, W, C, E, SW, S, SE].
    /// Returns slope in [-1, 1] range.
    /// </summary>
    public static float ComputeSlope(float[] heights)
    {
        if (heights.Length < 9)
            return 0f;

        // Sobel-style NW illumination: dz/dx and dz/dy
        float dzdx = ((heights[2] + 2 * heights[5] + heights[8])
                     - (heights[0] + 2 * heights[3] + heights[6])) / 8f;
        float dzdy = ((heights[6] + 2 * heights[7] + heights[8])
                     - (heights[0] + 2 * heights[1] + heights[2])) / 8f;

        // NW light direction: azimuth 315°, elevation 45°
        const float azimuth = 315f * MathF.PI / 180f;
        const float altitude = 45f * MathF.PI / 180f;

        float slopeRad = MathF.Atan(MathF.Sqrt(dzdx * dzdx + dzdy * dzdy));
        float aspect = MathF.Atan2(dzdy, -dzdx);

        float hillshade = MathF.Sin(altitude) * MathF.Cos(slopeRad)
                        + MathF.Cos(altitude) * MathF.Sin(slopeRad) * MathF.Cos(azimuth - aspect);

        return Math.Clamp(hillshade * 2f - 1f, -1f, 1f);
    }
}
