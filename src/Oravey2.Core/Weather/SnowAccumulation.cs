using Oravey2.Core.Audio;

namespace Oravey2.Core.Weather;

/// <summary>
/// Tracks snow coverage level on terrain. Coverage increases while snowing,
/// decreases (melts) when weather clears, and can be reduced locally by
/// character/vehicle movement (tracks).
/// </summary>
public sealed class SnowAccumulation
{
    /// <summary>Accumulation rate per real second while snowing (scaled by intensity).</summary>
    public const float AccumulationRate = 0.02f;

    /// <summary>Melt rate per real second when not snowing.</summary>
    public const float MeltRate = 0.005f;

    /// <summary>Amount of coverage removed per track event.</summary>
    public const float TrackReduction = 0.15f;

    private float _coverage;

    /// <summary>Current snow coverage (0 = no snow, 1 = fully covered).</summary>
    public float Coverage => _coverage;

    /// <summary>
    /// Updates snow coverage based on current weather.
    /// </summary>
    /// <param name="currentWeather">Active weather type.</param>
    /// <param name="weatherIntensity">Weather intensity (0–1).</param>
    /// <param name="deltaSec">Real delta time in seconds.</param>
    public void Tick(WeatherState currentWeather, float weatherIntensity, float deltaSec)
    {
        if (deltaSec <= 0) return;

        if (currentWeather == WeatherState.Snow)
        {
            _coverage += AccumulationRate * weatherIntensity * deltaSec;
        }
        else
        {
            _coverage -= MeltRate * deltaSec;
        }

        _coverage = Math.Clamp(_coverage, 0f, 1f);
    }

    /// <summary>
    /// Reduces local snow coverage when a character or vehicle moves over terrain.
    /// </summary>
    public void ApplyTrack()
    {
        _coverage = MathF.Max(0f, _coverage - TrackReduction);
    }

    /// <summary>
    /// Resets coverage to zero (e.g., on zone change or season reset).
    /// </summary>
    public void Reset() => _coverage = 0f;
}
