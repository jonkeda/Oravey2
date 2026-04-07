using System.Numerics;

namespace Oravey2.Core.World.Terrain;

/// <summary>
/// Applies terrain modifiers (flatten strips, channel cuts, level rects, craters) to a height grid.
/// Modifiers run after base height sampling but before subdivision and normal calculation.
/// </summary>
public static class TerrainModifierApplicator
{
    /// <summary>
    /// Applies all modifiers to the height grid in order.
    /// Heights are indexed [vx, vy] and cover the chunk from (0,0) to (chunkWorldSize, chunkWorldSize).
    /// </summary>
    public static void Apply(
        float[,] heights,
        int vertsPerSide,
        float chunkWorldSize,
        IReadOnlyList<TerrainModifier> modifiers)
    {
        foreach (var mod in modifiers)
        {
            switch (mod)
            {
                case FlattenStrip fs:
                    ApplyFlattenStrip(heights, vertsPerSide, chunkWorldSize, fs);
                    break;
                case ChannelCut cc:
                    ApplyChannelCut(heights, vertsPerSide, chunkWorldSize, cc);
                    break;
                case LevelRect lr:
                    ApplyLevelRect(heights, vertsPerSide, chunkWorldSize, lr);
                    break;
                case Crater cr:
                    ApplyCrater(heights, vertsPerSide, chunkWorldSize, cr);
                    break;
            }
        }
    }

    private static void ApplyFlattenStrip(
        float[,] heights, int vertsPerSide, float chunkWorldSize, FlattenStrip strip)
    {
        float halfWidth = strip.Width * 0.5f;

        for (int vy = 0; vy < vertsPerSide; vy++)
        {
            for (int vx = 0; vx < vertsPerSide; vx++)
            {
                var pos = VertexToWorld(vx, vy, vertsPerSide, chunkWorldSize);
                float dist = DistanceToPolyline(pos, strip.CentreLine);

                if (dist <= halfWidth)
                    heights[vx, vy] = strip.TargetHeight;
            }
        }
    }

    private static void ApplyChannelCut(
        float[,] heights, int vertsPerSide, float chunkWorldSize, ChannelCut channel)
    {
        float halfWidth = channel.Width * 0.5f;

        for (int vy = 0; vy < vertsPerSide; vy++)
        {
            for (int vx = 0; vx < vertsPerSide; vx++)
            {
                var pos = VertexToWorld(vx, vy, vertsPerSide, chunkWorldSize);
                float dist = DistanceToPolyline(pos, channel.CentreLine);

                if (dist <= halfWidth)
                    heights[vx, vy] -= channel.Depth;
            }
        }
    }

    private static void ApplyLevelRect(
        float[,] heights, int vertsPerSide, float chunkWorldSize, LevelRect rect)
    {
        for (int vy = 0; vy < vertsPerSide; vy++)
        {
            for (int vx = 0; vx < vertsPerSide; vx++)
            {
                var pos = VertexToWorld(vx, vy, vertsPerSide, chunkWorldSize);

                if (pos.X >= rect.Min.X && pos.X <= rect.Max.X &&
                    pos.Y >= rect.Min.Y && pos.Y <= rect.Max.Y)
                {
                    heights[vx, vy] = rect.TargetHeight;
                }
            }
        }
    }

    private static void ApplyCrater(
        float[,] heights, int vertsPerSide, float chunkWorldSize, Crater crater)
    {
        float radiusSq = crater.Radius * crater.Radius;

        for (int vy = 0; vy < vertsPerSide; vy++)
        {
            for (int vx = 0; vx < vertsPerSide; vx++)
            {
                var pos = VertexToWorld(vx, vy, vertsPerSide, chunkWorldSize);
                float dx = pos.X - crater.Centre.X;
                float dy = pos.Y - crater.Centre.Y;
                float distSq = dx * dx + dy * dy;

                if (distSq < radiusSq)
                {
                    float dist = MathF.Sqrt(distSq);
                    float t = dist / crater.Radius;
                    float depression = crater.Depth * (1f - t) * (1f - t);
                    heights[vx, vy] -= depression;
                }
            }
        }
    }

    private static Vector2 VertexToWorld(int vx, int vy, int vertsPerSide, float chunkWorldSize)
    {
        float u = (float)vx / (vertsPerSide - 1);
        float v = (float)vy / (vertsPerSide - 1);
        return new Vector2(u * chunkWorldSize, v * chunkWorldSize);
    }

    /// <summary>
    /// Computes the distance from a point to the nearest segment of a polyline.
    /// </summary>
    private static float DistanceToPolyline(Vector2 point, IReadOnlyList<Vector2> polyline)
    {
        if (polyline.Count == 0) return float.MaxValue;
        if (polyline.Count == 1) return Vector2.Distance(point, polyline[0]);

        float minDist = float.MaxValue;
        for (int i = 0; i < polyline.Count - 1; i++)
        {
            float d = DistanceToSegment(point, polyline[i], polyline[i + 1]);
            if (d < minDist)
                minDist = d;
        }

        return minDist;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float lenSq = Vector2.Dot(ab, ab);
        if (lenSq < 1e-8f)
            return Vector2.Distance(point, a);

        float t = Math.Clamp(Vector2.Dot(point - a, ab) / lenSq, 0f, 1f);
        var projection = a + t * ab;
        return Vector2.Distance(point, projection);
    }
}
