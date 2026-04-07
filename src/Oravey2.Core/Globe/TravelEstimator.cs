namespace Oravey2.Core.Globe;

/// <summary>
/// Mode of travel affecting speed and fuel consumption.
/// </summary>
public enum TravelMode
{
    OnFoot,
    Vehicle,
}

/// <summary>
/// Result of a travel estimation.
/// </summary>
public sealed record TravelEstimate(
    float DistanceKm,
    float EstimatedHours,
    float FuelCost,
    TravelMode Mode);

/// <summary>
/// Estimates travel time, distance, and fuel consumption between two world positions.
/// Used by the globe travel dialog (L4) and potentially by the fast-travel system.
/// </summary>
public sealed class TravelEstimator
{
    /// <summary>Walking speed in km/h.</summary>
    public const float WalkSpeedKmh = 5f;

    /// <summary>Vehicle speed in km/h.</summary>
    public const float VehicleSpeedKmh = 50f;

    /// <summary>Fuel consumed per km when using a vehicle.</summary>
    public const float FuelPerKm = 0.5f;

    /// <summary>World-space metres per chunk tile (tile size × chunk size).</summary>
    public const float MetresPerChunk = 32f; // 16 tiles × 2 m per tile

    /// <summary>
    /// Estimates travel between two chunk positions.
    /// </summary>
    /// <param name="fromChunkX">Origin chunk X.</param>
    /// <param name="fromChunkY">Origin chunk Y.</param>
    /// <param name="toChunkX">Destination chunk X.</param>
    /// <param name="toChunkY">Destination chunk Y.</param>
    /// <param name="mode">Travel mode (on foot or vehicle).</param>
    /// <param name="hasRoute">Whether a known route exists between the two points.</param>
    /// <returns>Travel estimate, or null if no route and travel is not possible.</returns>
    public TravelEstimate? Estimate(
        int fromChunkX, int fromChunkY,
        int toChunkX, int toChunkY,
        TravelMode mode = TravelMode.OnFoot,
        bool hasRoute = true)
    {
        if (!hasRoute)
            return null;

        float dx = toChunkX - fromChunkX;
        float dy = toChunkY - fromChunkY;
        float chunkDistance = MathF.Sqrt(dx * dx + dy * dy);
        float distanceKm = chunkDistance * MetresPerChunk / 1000f;

        float speed = mode switch
        {
            TravelMode.OnFoot => WalkSpeedKmh,
            TravelMode.Vehicle => VehicleSpeedKmh,
            _ => WalkSpeedKmh,
        };

        float hours = distanceKm / speed;

        float fuel = mode == TravelMode.Vehicle
            ? distanceKm * FuelPerKm
            : 0f;

        return new TravelEstimate(distanceKm, hours, fuel, mode);
    }
}
