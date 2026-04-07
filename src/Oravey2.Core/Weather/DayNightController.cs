using System.Numerics;

namespace Oravey2.Core.Weather;

/// <summary>
/// Colour temperature preset for a time of day.
/// </summary>
public readonly record struct LightingState(
    float SunElevationDeg,
    float SunAzimuthDeg,
    Vector3 SunColour,
    Vector3 AmbientColour,
    float ShadowIntensity);

/// <summary>
/// Computes sun direction, colour temperature, and ambient lighting from the in-game hour.
/// Pure logic — the Stride rendering script reads these values to set DirectionalLight properties.
/// </summary>
public static class DayNightController
{
    // Phase boundaries (matching DayNightCycleProcessor)
    private const float DawnStart = 5f;    // 05:00
    private const float DawnEnd = 7f;      // 07:00
    private const float DuskStart = 17f;   // 17:00
    private const float DuskEnd = 19f;     // 19:00

    // Sun colours
    private static readonly Vector3 DawnColour = new(1.0f, 0.7f, 0.4f);    // warm orange
    private static readonly Vector3 DayColour = new(1.0f, 0.98f, 0.95f);   // cool white
    private static readonly Vector3 DuskColour = new(1.0f, 0.5f, 0.3f);    // red-orange
    private static readonly Vector3 NightSunColour = new(0.2f, 0.25f, 0.4f); // moonlight blue

    // Ambient colours
    private static readonly Vector3 DawnAmbient = new(0.4f, 0.3f, 0.25f);
    private static readonly Vector3 DayAmbient = new(0.5f, 0.5f, 0.5f);
    private static readonly Vector3 DuskAmbient = new(0.35f, 0.25f, 0.2f);
    private static readonly Vector3 NightAmbient = new(0.08f, 0.1f, 0.18f);

    /// <summary>
    /// Computes the full lighting state for a given in-game hour (0–24).
    /// </summary>
    public static LightingState ComputeLighting(float hour)
    {
        hour = ((hour % 24f) + 24f) % 24f; // normalise

        float elevation;
        Vector3 sunColour;
        Vector3 ambient;
        float shadow;

        if (hour >= DawnStart && hour < DawnEnd)
        {
            // Dawn: 05:00 – 07:00
            float t = (hour - DawnStart) / (DawnEnd - DawnStart);
            elevation = Lerp(-5f, 15f, t);
            sunColour = LerpV(NightSunColour, DawnColour, t);
            ambient = LerpV(NightAmbient, DawnAmbient, t);
            shadow = Lerp(0.1f, 0.5f, t);
        }
        else if (hour >= DawnEnd && hour < DuskStart)
        {
            // Day: 07:00 – 17:00
            float t = (hour - DawnEnd) / (DuskStart - DawnEnd);
            // Sun arc: rises to max at noon (t=0.5), descends
            float arc = 1f - MathF.Abs(t - 0.5f) * 2f;
            elevation = Lerp(15f, 75f, arc);
            sunColour = LerpV(DawnColour, DayColour, MathF.Min(t * 2f, 1f));
            ambient = LerpV(DawnAmbient, DayAmbient, MathF.Min(t * 2f, 1f));
            shadow = Lerp(0.5f, 1.0f, arc);
        }
        else if (hour >= DuskStart && hour < DuskEnd)
        {
            // Dusk: 17:00 – 19:00
            float t = (hour - DuskStart) / (DuskEnd - DuskStart);
            elevation = Lerp(15f, -5f, t);
            sunColour = LerpV(DuskColour, NightSunColour, t);
            ambient = LerpV(DuskAmbient, NightAmbient, t);
            shadow = Lerp(0.5f, 0.1f, t);
        }
        else
        {
            // Night
            elevation = -10f;
            sunColour = NightSunColour;
            ambient = NightAmbient;
            shadow = 0.1f;
        }

        // Azimuth: sun moves east→south→west over the day (simplified)
        float azimuth = hour < 12f
            ? Lerp(90f, 180f, hour / 12f)
            : Lerp(180f, 270f, (hour - 12f) / 12f);

        return new LightingState(elevation, azimuth, sunColour, ambient, shadow);
    }

    /// <summary>
    /// Returns the sun elevation in degrees for the given in-game hour.
    /// Positive = above horizon, negative = below.
    /// </summary>
    public static float GetSunElevation(float hour)
        => ComputeLighting(hour).SunElevationDeg;

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static Vector3 LerpV(Vector3 a, Vector3 b, float t) => new(
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t,
        a.Z + (b.Z - a.Z) * t);
}
