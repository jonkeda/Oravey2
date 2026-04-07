using System.Numerics;

namespace Oravey2.Core.World.LinearFeatures;

/// <summary>
/// Catmull-Rom spline math for linear feature rendering.
/// </summary>
public static class SplineMath
{
    /// <summary>
    /// Evaluates a Catmull-Rom spline segment at parameter t ∈ [0,1].
    /// P0,P1,P2,P3 are the four control points; the curve passes through P1 (t=0) and P2 (t=1).
    /// </summary>
    public static Vector2 CatmullRomEvaluate(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        // Standard Catmull-Rom matrix form
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    /// <summary>
    /// Evaluates the tangent (first derivative) of a Catmull-Rom spline segment at parameter t ∈ [0,1].
    /// </summary>
    public static Vector2 CatmullRomTangent(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;

        return 0.5f * (
            (-p0 + p2) +
            (4f * p0 - 10f * p1 + 8f * p2 - 2f * p3) * t +
            (-3f * p0 + 9f * p1 - 9f * p2 + 3f * p3) * t2);
    }

    /// <summary>
    /// Builds a Catmull-Rom spline through N nodes, returning uniformly-spaced samples
    /// along the arc length. Endpoints are duplicated as ghost points.
    /// </summary>
    /// <param name="nodes">The control points the spline passes through (minimum 2).</param>
    /// <param name="samplesPerSegment">Number of samples per segment between adjacent nodes.</param>
    /// <returns>Uniformly arc-length parameterised sample positions and tangents.</returns>
    public static (Vector2 Position, Vector2 Tangent)[] SampleSpline(
        IReadOnlyList<Vector2> nodes,
        int samplesPerSegment = 8)
    {
        if (nodes.Count < 2)
            return nodes.Count == 1
                ? [(nodes[0], Vector2.UnitX)]
                : [];

        // Build spline segments: duplicate first/last for ghost points
        int segmentCount = nodes.Count - 1;

        // Phase 1: Raw parametric samples (dense enough for good arc-length approximation)
        var rawSamples = new List<(Vector2 Position, Vector2 Tangent, float CumulativeArc)>();
        float totalArc = 0f;
        Vector2 prev = nodes[0];

        for (int seg = 0; seg < segmentCount; seg++)
        {
            var p0 = seg > 0 ? nodes[seg - 1] : nodes[0] * 2 - nodes[1]; // ghost
            var p1 = nodes[seg];
            var p2 = nodes[seg + 1];
            var p3 = seg < segmentCount - 1 ? nodes[seg + 2] : nodes[^1] * 2 - nodes[^2]; // ghost

            int steps = samplesPerSegment;
            for (int i = 0; i <= steps; i++)
            {
                // Skip duplicate at segment joins (except first segment's start)
                if (i == 0 && seg > 0) continue;

                float t = (float)i / steps;
                var pos = CatmullRomEvaluate(p0, p1, p2, p3, t);
                var tan = CatmullRomTangent(p0, p1, p2, p3, t);

                float arcStep = Vector2.Distance(prev, pos);
                totalArc += arcStep;
                prev = pos;

                rawSamples.Add((pos, tan, totalArc));
            }
        }

        if (totalArc < 1e-6f)
            return [(nodes[0], Vector2.UnitX)];

        // Phase 2: Resample at uniform arc-length intervals
        int totalSamples = segmentCount * samplesPerSegment + 1;
        var result = new (Vector2 Position, Vector2 Tangent)[totalSamples];
        float arcPerSample = totalArc / (totalSamples - 1);

        result[0] = (rawSamples[0].Position, Vector2.Normalize(rawSamples[0].Tangent));
        int rawIdx = 0;

        for (int i = 1; i < totalSamples; i++)
        {
            float targetArc = i * arcPerSample;

            // Walk raw samples until we bracket the target arc
            while (rawIdx < rawSamples.Count - 1 && rawSamples[rawIdx + 1].CumulativeArc < targetArc)
                rawIdx++;

            if (rawIdx >= rawSamples.Count - 1)
            {
                // Clamp to last sample
                var last = rawSamples[^1];
                result[i] = (last.Position, SafeNormalize(last.Tangent));
            }
            else
            {
                // Lerp between rawIdx and rawIdx+1
                var a = rawSamples[rawIdx];
                var b = rawSamples[rawIdx + 1];
                float segArc = b.CumulativeArc - a.CumulativeArc;
                float localT = segArc > 1e-8f ? (targetArc - a.CumulativeArc) / segArc : 0f;

                var pos = Vector2.Lerp(a.Position, b.Position, localT);
                var tan = Vector2.Lerp(a.Tangent, b.Tangent, localT);
                result[i] = (pos, SafeNormalize(tan));
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the total arc length of a Catmull-Rom spline through the given nodes.
    /// </summary>
    public static float ComputeArcLength(IReadOnlyList<Vector2> nodes, int stepsPerSegment = 16)
    {
        if (nodes.Count < 2) return 0f;

        float totalArc = 0f;
        int segmentCount = nodes.Count - 1;

        for (int seg = 0; seg < segmentCount; seg++)
        {
            var p0 = seg > 0 ? nodes[seg - 1] : nodes[0] * 2 - nodes[1];
            var p1 = nodes[seg];
            var p2 = nodes[seg + 1];
            var p3 = seg < segmentCount - 1 ? nodes[seg + 2] : nodes[^1] * 2 - nodes[^2];

            var prev = p1;
            for (int i = 1; i <= stepsPerSegment; i++)
            {
                float t = (float)i / stepsPerSegment;
                var pos = CatmullRomEvaluate(p0, p1, p2, p3, t);
                totalArc += Vector2.Distance(prev, pos);
                prev = pos;
            }
        }

        return totalArc;
    }

    private static Vector2 SafeNormalize(Vector2 v)
    {
        float len = v.Length();
        return len > 1e-8f ? v / len : Vector2.UnitX;
    }
}
