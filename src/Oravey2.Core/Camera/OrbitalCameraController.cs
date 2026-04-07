namespace Oravey2.Core.Camera;

/// <summary>
/// Pure-logic orbital camera controller for the globe view (L4).
/// Manages latitude/longitude orbit, zoom distance, and damped rotation.
/// The Stride SyncScript wrapper reads these values each frame to position the camera entity.
/// </summary>
public sealed class OrbitalCameraController
{
    /// <summary>Minimum distance from the globe centre.</summary>
    public const float MinDistance = 120f;

    /// <summary>Maximum distance from the globe centre.</summary>
    public const float MaxDistance = 500f;

    /// <summary>Default zoom distance.</summary>
    public const float DefaultDistance = 250f;

    /// <summary>Rotation damping factor (0 = no damping, 1 = instant stop).</summary>
    public const float DefaultDamping = 0.9f;

    /// <summary>Latitude of the camera orbit in degrees (−90 = south pole, +90 = north pole).</summary>
    public float Latitude { get; private set; }

    /// <summary>Longitude of the camera orbit in degrees (0–360).</summary>
    public float Longitude { get; private set; }

    /// <summary>Distance from the globe centre.</summary>
    public float Distance { get; private set; } = DefaultDistance;

    /// <summary>Current rotational velocity (degrees/s) for damping.</summary>
    public float LatitudeVelocity { get; private set; }
    public float LongitudeVelocity { get; private set; }

    /// <summary>Damping factor per tick (0–1). Higher = faster deceleration.</summary>
    public float Damping { get; set; } = DefaultDamping;

    /// <summary>
    /// Creates an orbital camera controller at the given initial position.
    /// </summary>
    public OrbitalCameraController(float latitude = 0f, float longitude = 0f, float distance = DefaultDistance)
    {
        Latitude = ClampLatitude(latitude);
        Longitude = WrapLongitude(longitude);
        Distance = Math.Clamp(distance, MinDistance, MaxDistance);
    }

    /// <summary>
    /// Applies a drag rotation (e.g., from mouse drag delta).
    /// </summary>
    /// <param name="deltaLat">Latitude change in degrees.</param>
    /// <param name="deltaLon">Longitude change in degrees.</param>
    public void Rotate(float deltaLat, float deltaLon)
    {
        Latitude = ClampLatitude(Latitude + deltaLat);
        Longitude = WrapLongitude(Longitude + deltaLon);

        // Set velocity for damping continuation
        LatitudeVelocity = deltaLat;
        LongitudeVelocity = deltaLon;
    }

    /// <summary>
    /// Adjusts the orbit distance (scroll wheel zoom).
    /// </summary>
    /// <param name="delta">Positive = zoom out, negative = zoom in.</param>
    public void Zoom(float delta)
    {
        Distance = Math.Clamp(Distance + delta, MinDistance, MaxDistance);
    }

    /// <summary>
    /// Snaps the camera to look at a specific latitude/longitude (e.g., double-click region).
    /// Clears any rotation velocity.
    /// </summary>
    public void SnapTo(float latitude, float longitude)
    {
        Latitude = ClampLatitude(latitude);
        Longitude = WrapLongitude(longitude);
        LatitudeVelocity = 0f;
        LongitudeVelocity = 0f;
    }

    /// <summary>
    /// Advances damping. Call once per frame with the frame delta time.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (MathF.Abs(LatitudeVelocity) > 0.01f || MathF.Abs(LongitudeVelocity) > 0.01f)
        {
            Latitude = ClampLatitude(Latitude + LatitudeVelocity * deltaTime);
            Longitude = WrapLongitude(Longitude + LongitudeVelocity * deltaTime);

            float dampFactor = MathF.Pow(1f - Damping, deltaTime * 60f);
            LatitudeVelocity *= dampFactor;
            LongitudeVelocity *= dampFactor;
        }
        else
        {
            LatitudeVelocity = 0f;
            LongitudeVelocity = 0f;
        }
    }

    /// <summary>
    /// Computes the camera world position from the current orbit parameters.
    /// Globe centre is assumed at the origin.
    /// </summary>
    public (float X, float Y, float Z) GetCameraPosition()
    {
        float latRad = Latitude * MathF.PI / 180f;
        float lonRad = Longitude * MathF.PI / 180f;

        float x = Distance * MathF.Cos(latRad) * MathF.Sin(lonRad);
        float y = Distance * MathF.Sin(latRad);
        float z = Distance * MathF.Cos(latRad) * MathF.Cos(lonRad);

        return (x, y, z);
    }

    private static float ClampLatitude(float lat) => Math.Clamp(lat, -89f, 89f);

    private static float WrapLongitude(float lon)
    {
        lon %= 360f;
        if (lon < 0f) lon += 360f;
        return lon;
    }
}
