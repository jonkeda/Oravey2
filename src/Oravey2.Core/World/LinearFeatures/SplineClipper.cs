using System.Numerics;

namespace Oravey2.Core.World.LinearFeatures;

/// <summary>
/// Clips linear feature splines to a rectangular chunk bounding area.
/// Only the portion of the spline inside the chunk gets meshed.
/// </summary>
public static class SplineClipper
{
    /// <summary>
    /// Clips a linear feature's nodes to a chunk bounding rectangle.
    /// Returns the clipped feature, or null if the feature is entirely outside the chunk.
    /// </summary>
    /// <param name="feature">The full-region linear feature.</param>
    /// <param name="chunkMinX">Chunk left edge in local coordinates.</param>
    /// <param name="chunkMinY">Chunk top edge in local coordinates.</param>
    /// <param name="chunkMaxX">Chunk right edge in local coordinates.</param>
    /// <param name="chunkMaxY">Chunk bottom edge in local coordinates.</param>
    /// <returns>A clipped feature with only the nodes inside (plus entry/exit at boundaries), or null.</returns>
    public static LinearFeature? Clip(
        LinearFeature feature,
        float chunkMinX, float chunkMinY,
        float chunkMaxX, float chunkMaxY)
    {
        if (feature.Nodes.Count < 2)
            return null;

        // Expand the clip rect slightly by the feature width so edge ribbons aren't cut off
        float margin = feature.Width;
        float minX = chunkMinX - margin;
        float minY = chunkMinY - margin;
        float maxX = chunkMaxX + margin;
        float maxY = chunkMaxY + margin;

        var clippedNodes = new List<LinearFeatureNode>();
        bool anyInside = false;

        for (int i = 0; i < feature.Nodes.Count; i++)
        {
            var node = feature.Nodes[i];
            bool inside = IsInside(node.Position, minX, minY, maxX, maxY);

            if (inside)
            {
                // If this is the first inside node and previous was outside, add intersection
                if (!anyInside && i > 0)
                {
                    var prev = feature.Nodes[i - 1];
                    var entry = ClipEdge(prev.Position, node.Position, minX, minY, maxX, maxY);
                    if (entry.HasValue)
                        clippedNodes.Add(new LinearFeatureNode { Position = entry.Value, OverrideHeight = InterpolateOverride(prev, node, entry.Value) });
                }

                clippedNodes.Add(node);
                anyInside = true;
            }
            else if (anyInside)
            {
                // Just exited the chunk — add exit point
                var prev = feature.Nodes[i - 1];
                var exit = ClipEdge(prev.Position, node.Position, minX, minY, maxX, maxY);
                if (exit.HasValue)
                    clippedNodes.Add(new LinearFeatureNode { Position = exit.Value, OverrideHeight = InterpolateOverride(prev, node, exit.Value) });

                // Feature may re-enter the chunk later, so keep checking
                anyInside = false;
            }
            else if (i > 0)
            {
                // Both outside — but segment might cross the chunk
                var prev = feature.Nodes[i - 1];
                var crossings = FindCrossings(prev.Position, node.Position, minX, minY, maxX, maxY);
                if (crossings.Count >= 2)
                {
                    clippedNodes.Add(new LinearFeatureNode { Position = crossings[0], OverrideHeight = InterpolateOverride(prev, node, crossings[0]) });
                    clippedNodes.Add(new LinearFeatureNode { Position = crossings[1], OverrideHeight = InterpolateOverride(prev, node, crossings[1]) });
                }
            }
        }

        if (clippedNodes.Count < 2)
            return null;

        return new LinearFeature { Type = feature.Type, Style = feature.Style, Width = feature.Width, Nodes = clippedNodes };
    }

    private static bool IsInside(Vector2 pos, float minX, float minY, float maxX, float maxY)
        => pos.X >= minX && pos.X <= maxX && pos.Y >= minY && pos.Y <= maxY;

    private static Vector2? ClipEdge(Vector2 outside, Vector2 inside, float minX, float minY, float maxX, float maxY)
    {
        // Find intersection of segment from outside to inside with the clip rect
        var dir = inside - outside;
        float tMin = 0f;
        float tMax = 1f;

        if (!ClipAxis(outside.X, dir.X, minX, maxX, ref tMin, ref tMax)) return null;
        if (!ClipAxis(outside.Y, dir.Y, minY, maxY, ref tMin, ref tMax)) return null;

        return outside + dir * tMin;
    }

    private static List<Vector2> FindCrossings(Vector2 a, Vector2 b, float minX, float minY, float maxX, float maxY)
    {
        var dir = b - a;
        float tMin = 0f;
        float tMax = 1f;

        if (!ClipAxis(a.X, dir.X, minX, maxX, ref tMin, ref tMax))
            return [];
        if (!ClipAxis(a.Y, dir.Y, minY, maxY, ref tMin, ref tMax))
            return [];

        var result = new List<Vector2>(2);
        if (tMin > 0f && tMin < 1f)
            result.Add(a + dir * tMin);
        if (tMax > 0f && tMax < 1f && MathF.Abs(tMax - tMin) > 1e-6f)
            result.Add(a + dir * tMax);
        return result;
    }

    /// <summary>
    /// Cohen-Sutherland-style axis clipping.
    /// </summary>
    private static bool ClipAxis(float origin, float dir, float min, float max, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(dir) < 1e-8f)
        {
            return origin >= min && origin <= max;
        }

        float t1 = (min - origin) / dir;
        float t2 = (max - origin) / dir;

        if (t1 > t2) (t1, t2) = (t2, t1);

        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);

        return tMin <= tMax;
    }

    private static float? InterpolateOverride(LinearFeatureNode nodeA, LinearFeatureNode nodeB, Vector2 clipPoint)
    {
        if (!nodeA.OverrideHeight.HasValue && !nodeB.OverrideHeight.HasValue)
            return null;
        if (nodeA.OverrideHeight.HasValue && !nodeB.OverrideHeight.HasValue)
            return nodeA.OverrideHeight;
        if (!nodeA.OverrideHeight.HasValue && nodeB.OverrideHeight.HasValue)
            return nodeB.OverrideHeight;

        // Both have overrides — interpolate by distance
        float totalDist = Vector2.Distance(nodeA.Position, nodeB.Position);
        if (totalDist < 1e-6f)
            return nodeA.OverrideHeight;
        float t = Vector2.Distance(nodeA.Position, clipPoint) / totalDist;
        return nodeA.OverrideHeight!.Value + t * (nodeB.OverrideHeight!.Value - nodeA.OverrideHeight.Value);
    }
}
