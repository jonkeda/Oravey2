using System.Numerics;
using Oravey2.Core.World.Terrain;

namespace Oravey2.Core.World.LinearFeatures;

/// <summary>
/// Output mesh data for a single linear feature ribbon (road, rail, river, bridge deck).
/// Uses the same VertexData as the terrain pipeline for consistency.
/// </summary>
public sealed class RibbonMesh
{
    public VertexData[] Vertices { get; }
    public int[] Indices { get; }
    public LinearFeatureType Type { get; }
    public string Style { get; }

    public RibbonMesh(VertexData[] vertices, int[] indices, LinearFeatureType type, string style)
    {
        Vertices = vertices;
        Indices = indices;
        Type = type;
        Style = style;
    }
}

/// <summary>
/// Builds triangle-strip ribbon meshes from spline-sampled linear features.
/// For each sample point, left/right vertices are extruded perpendicular to the tangent.
/// Y is projected onto the heightmap (or overridden for bridge segments).
/// </summary>
public static class RibbonMeshBuilder
{
    /// <summary>
    /// Y offset above terrain to prevent z-fighting.
    /// </summary>
    public const float TerrainOffset = 0.05f;

    /// <summary>
    /// Builds a ribbon mesh for a linear feature within a chunk.
    /// </summary>
    /// <param name="feature">The linear feature to render.</param>
    /// <param name="heights">Height grid from the terrain pipeline (for Y projection).</param>
    /// <param name="vertsPerSide">Vertex grid resolution.</param>
    /// <param name="chunkWorldSize">World size of the chunk (e.g. 32m).</param>
    /// <param name="samplesPerSegment">Spline sampling resolution.</param>
    public static RibbonMesh Build(
        LinearFeature feature,
        float[,] heights,
        int vertsPerSide,
        float chunkWorldSize,
        int samplesPerSegment = 8)
    {
        var nodePositions = new List<Vector2>(feature.Nodes.Count);
        foreach (var node in feature.Nodes)
            nodePositions.Add(node.Position);

        var samples = SplineMath.SampleSpline(nodePositions, samplesPerSegment);
        if (samples.Length < 2)
            return new RibbonMesh([], [], feature.Type, feature.Style);

        float halfWidth = feature.Width * 0.5f;
        int sampleCount = samples.Length;
        var vertices = new VertexData[sampleCount * 2];
        float cumulativeArc = 0f;

        // Build override-height lookup from nodes
        var overrideHeights = BuildOverrideMap(feature.Nodes, nodePositions, samples);

        for (int i = 0; i < sampleCount; i++)
        {
            var (pos2d, tangent) = samples[i];

            // Perpendicular to tangent in XZ plane (rotate 90° CCW)
            var perp = new Vector2(-tangent.Y, tangent.X);

            var leftPos2d = pos2d - perp * halfWidth;
            var rightPos2d = pos2d + perp * halfWidth;

            float centreY;
            if (overrideHeights[i].HasValue)
            {
                centreY = overrideHeights[i]!.Value;
            }
            else
            {
                centreY = SampleHeightAt(pos2d, heights, vertsPerSide, chunkWorldSize) + TerrainOffset;
            }

            float leftY = overrideHeights[i].HasValue
                ? centreY
                : SampleHeightAt(leftPos2d, heights, vertsPerSide, chunkWorldSize) + TerrainOffset;
            float rightY = overrideHeights[i].HasValue
                ? centreY
                : SampleHeightAt(rightPos2d, heights, vertsPerSide, chunkWorldSize) + TerrainOffset;

            // Cumulative arc for V texture coordinate
            if (i > 0)
                cumulativeArc += Vector2.Distance(samples[i - 1].Position, pos2d);

            var normal = new Vector3(0, 1, 0); // Ribbon faces up

            vertices[i * 2] = new VertexData(
                new Vector3(leftPos2d.X, leftY, leftPos2d.Y),
                normal,
                new Vector2(0f, cumulativeArc));

            vertices[i * 2 + 1] = new VertexData(
                new Vector3(rightPos2d.X, rightY, rightPos2d.Y),
                normal,
                new Vector2(1f, cumulativeArc));
        }

        // Build triangle strip as indexed triangle list
        int quadCount = sampleCount - 1;
        var indices = new int[quadCount * 6];
        for (int i = 0; i < quadCount; i++)
        {
            int bl = i * 2;
            int br = i * 2 + 1;
            int tl = (i + 1) * 2;
            int tr = (i + 1) * 2 + 1;

            // CW winding matching terrain convention (tl→bl→tr pattern)
            // After Stride's isLeftHanded flip this becomes CCW = front face from above
            indices[i * 6 + 0] = bl;
            indices[i * 6 + 1] = br;
            indices[i * 6 + 2] = tl;

            indices[i * 6 + 3] = tl;
            indices[i * 6 + 4] = br;
            indices[i * 6 + 5] = tr;
        }

        return new RibbonMesh(vertices, indices, feature.Type, feature.Style);
    }

    /// <summary>
    /// Samples terrain height at a world-space XZ position using bilinear interpolation on the height grid.
    /// </summary>
    internal static float SampleHeightAt(Vector2 worldXZ, float[,] heights, int vertsPerSide, float chunkWorldSize)
    {
        // Convert world XZ to grid coordinates
        float gx = worldXZ.X / chunkWorldSize * (vertsPerSide - 1);
        float gy = worldXZ.Y / chunkWorldSize * (vertsPerSide - 1);

        int x0 = Math.Clamp((int)MathF.Floor(gx), 0, vertsPerSide - 2);
        int y0 = Math.Clamp((int)MathF.Floor(gy), 0, vertsPerSide - 2);
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        float fx = Math.Clamp(gx - x0, 0f, 1f);
        float fy = Math.Clamp(gy - y0, 0f, 1f);

        float h00 = heights[x0, y0];
        float h10 = heights[x1, y0];
        float h01 = heights[x0, y1];
        float h11 = heights[x1, y1];

        return h00 * (1 - fx) * (1 - fy)
             + h10 * fx * (1 - fy)
             + h01 * (1 - fx) * fy
             + h11 * fx * fy;
    }

    /// <summary>
    /// Maps each spline sample to a nullable override height based on proximity to nodes with OverrideHeight set.
    /// </summary>
    private static float?[] BuildOverrideMap(
        IReadOnlyList<LinearFeatureNode> nodes,
        List<Vector2> nodePositions,
        (Vector2 Position, Vector2 Tangent)[] samples)
    {
        var overrides = new float?[samples.Length];

        // For each sample, check if closest node has an override height
        for (int i = 0; i < samples.Length; i++)
        {
            var samplePos = samples[i].Position;
            float minDist = float.MaxValue;
            int closestNode = -1;

            for (int n = 0; n < nodePositions.Count; n++)
            {
                float dist = Vector2.Distance(samplePos, nodePositions[n]);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestNode = n;
                }
            }

            if (closestNode >= 0 && nodes[closestNode].OverrideHeight.HasValue)
            {
                // Check if between two override nodes or near one
                overrides[i] = InterpolateOverrideHeight(samplePos, closestNode, nodes, nodePositions);
            }
        }

        return overrides;
    }

    private static float? InterpolateOverrideHeight(
        Vector2 samplePos,
        int closestNodeIdx,
        IReadOnlyList<LinearFeatureNode> nodes,
        List<Vector2> nodePositions)
    {
        var node = nodes[closestNodeIdx];
        if (!node.OverrideHeight.HasValue)
            return null;

        // Simple: use closest node's override height
        // For bridge segments spanning multiple override nodes, this produces a flat deck
        return node.OverrideHeight.Value;
    }
}
