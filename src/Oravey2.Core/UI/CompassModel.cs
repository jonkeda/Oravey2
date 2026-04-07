namespace Oravey2.Core.UI;

/// <summary>
/// Pure-logic compass model. Given a camera yaw (degrees), produces the horizontal
/// position of each cardinal/ordinal direction and can compute POI bearing offsets.
///
/// Convention: yaw 0° = North, increases clockwise (90° = East).
/// The compass strip spans a configurable angular width (default 180°).
/// Positions are normalised to [-1, 1] where 0 = centre of the strip.
/// </summary>
public sealed class CompassModel
{
    /// <summary>Angular width of the visible compass strip in degrees.</summary>
    public float StripWidth { get; }

    private static readonly (string Label, float Bearing)[] _directions =
    [
        ("N",  0f),
        ("NE", 45f),
        ("E",  90f),
        ("SE", 135f),
        ("S",  180f),
        ("SW", 225f),
        ("W",  270f),
        ("NW", 315f),
    ];

    public CompassModel(float stripWidth = 180f)
    {
        if (stripWidth <= 0f || stripWidth > 360f)
            throw new ArgumentOutOfRangeException(nameof(stripWidth));
        StripWidth = stripWidth;
    }

    /// <summary>
    /// Returns all cardinal/ordinal directions that are visible on the compass strip
    /// for the given <paramref name="cameraYaw"/> (0–360°).
    /// Each result has a normalised position in [-1, 1].
    /// </summary>
    public IReadOnlyList<CompassEntry> GetVisibleDirections(float cameraYaw)
    {
        var result = new List<CompassEntry>();
        float half = StripWidth / 2f;

        foreach (var (label, bearing) in _directions)
        {
            float delta = NormaliseDelta(bearing - cameraYaw);
            if (MathF.Abs(delta) <= half)
                result.Add(new CompassEntry(label, bearing, delta / half));
        }

        return result;
    }

    /// <summary>
    /// Computes the normalised position of a point-of-interest on the compass strip.
    /// Returns null if the POI is outside the visible strip.
    /// <paramref name="poiBearing"/> is the world bearing to the POI (0–360°).
    /// </summary>
    public float? GetPoiBearing(float cameraYaw, float poiBearing)
    {
        float delta = NormaliseDelta(poiBearing - cameraYaw);
        float half = StripWidth / 2f;
        if (MathF.Abs(delta) > half)
            return null;
        return delta / half;
    }

    /// <summary>
    /// Computes the world bearing from (<paramref name="fromX"/>, <paramref name="fromZ"/>)
    /// to (<paramref name="toX"/>, <paramref name="toZ"/>). Returns degrees 0–360.
    /// Assumes +X = East, +Z = South (Stride convention).
    /// </summary>
    public static float ComputeBearing(float fromX, float fromZ, float toX, float toZ)
    {
        float dx = toX - fromX;
        float dz = toZ - fromZ;
        // atan2(dx, -dz) gives angle from north clockwise
        float rad = MathF.Atan2(dx, -dz);
        float deg = rad * 180f / MathF.PI;
        return (deg + 360f) % 360f;
    }

    /// <summary>Normalise a degree difference to [-180, 180].</summary>
    private static float NormaliseDelta(float delta)
    {
        delta %= 360f;
        if (delta > 180f) delta -= 360f;
        if (delta < -180f) delta += 360f;
        return delta;
    }
}

/// <summary>
/// A single entry on the compass strip.
/// </summary>
/// <param name="Label">Cardinal/ordinal label (N, NE, E, …).</param>
/// <param name="Bearing">World bearing in degrees.</param>
/// <param name="NormalisedPosition">Position on the strip: -1 = left edge, 0 = centre, +1 = right edge.</param>
public readonly record struct CompassEntry(string Label, float Bearing, float NormalisedPosition);
