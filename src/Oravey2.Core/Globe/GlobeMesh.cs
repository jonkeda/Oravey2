using System.Numerics;

namespace Oravey2.Core.Globe;

/// <summary>
/// Generates a UV sphere mesh with optional height displacement and per-vertex biome colours.
/// Used for the L4 globe view.
/// </summary>
public static class GlobeMesh
{
    public const int DefaultLatSegments = 40;
    public const int DefaultLonSegments = 80;
    public const float DefaultRadius = 100f;

    /// <summary>
    /// Generates a UV sphere mesh.
    /// </summary>
    /// <param name="latSegments">Number of latitude bands (top to bottom).</param>
    /// <param name="lonSegments">Number of longitude slices.</param>
    /// <param name="radius">Base radius of the sphere.</param>
    /// <param name="heightSampler">Optional callback: (lat, lon) → height displacement (metres above sea level). Null = no displacement.</param>
    /// <param name="colourSampler">Optional callback: (lat, lon) → RGBA colour. Null = white.</param>
    /// <returns>Vertices and triangle indices for the sphere.</returns>
    public static GlobeMeshData Generate(
        int latSegments = DefaultLatSegments,
        int lonSegments = DefaultLonSegments,
        float radius = DefaultRadius,
        Func<float, float, float>? heightSampler = null,
        Func<float, float, Vector4>? colourSampler = null)
    {
        if (latSegments < 2) throw new ArgumentOutOfRangeException(nameof(latSegments));
        if (lonSegments < 3) throw new ArgumentOutOfRangeException(nameof(lonSegments));

        // Vertex count: (latSegments - 1) rings of (lonSegments) verts + 2 poles
        int vertexCount = (latSegments - 1) * lonSegments + 2;
        var vertices = new GlobeVertex[vertexCount];

        int vi = 0;

        // North pole
        var northNormal = new Vector3(0, 1, 0);
        float northHeight = heightSampler?.Invoke(0f, 0f) ?? 0f;
        var northColour = colourSampler?.Invoke(0f, 0f) ?? new Vector4(1, 1, 1, 1);
        vertices[vi++] = new GlobeVertex(
            northNormal * (radius + northHeight),
            northNormal,
            northColour);

        // Interior rings
        for (int lat = 1; lat < latSegments; lat++)
        {
            float theta = MathF.PI * lat / latSegments; // 0 at pole, PI at south
            float sinTheta = MathF.Sin(theta);
            float cosTheta = MathF.Cos(theta);

            for (int lon = 0; lon < lonSegments; lon++)
            {
                float phi = 2f * MathF.PI * lon / lonSegments;
                float sinPhi = MathF.Sin(phi);
                float cosPhi = MathF.Cos(phi);

                var normal = new Vector3(
                    sinTheta * cosPhi,
                    cosTheta,
                    sinTheta * sinPhi);

                float latDeg = 90f - (theta * 180f / MathF.PI);
                float lonDeg = phi * 180f / MathF.PI;

                float h = heightSampler?.Invoke(latDeg, lonDeg) ?? 0f;
                var colour = colourSampler?.Invoke(latDeg, lonDeg) ?? new Vector4(1, 1, 1, 1);

                vertices[vi++] = new GlobeVertex(
                    normal * (radius + h),
                    normal,
                    colour);
            }
        }

        // South pole
        var southNormal = new Vector3(0, -1, 0);
        float southHeight = heightSampler?.Invoke(-90f, 0f) ?? 0f;
        var southColour = colourSampler?.Invoke(-90f, 0f) ?? new Vector4(1, 1, 1, 1);
        vertices[vi++] = new GlobeVertex(
            southNormal * (radius + southHeight),
            southNormal,
            southColour);

        // Indices
        int triangleCount = lonSegments * 2 + (latSegments - 2) * lonSegments * 2;
        var indices = new int[triangleCount * 3];
        int ii = 0;

        // North pole cap: connect pole (0) to first ring (1 .. lonSegments)
        for (int lon = 0; lon < lonSegments; lon++)
        {
            int current = 1 + lon;
            int next = 1 + (lon + 1) % lonSegments;
            indices[ii++] = 0;
            indices[ii++] = current;
            indices[ii++] = next;
        }

        // Body quads (each split into 2 triangles)
        for (int lat = 0; lat < latSegments - 2; lat++)
        {
            int ringStart = 1 + lat * lonSegments;
            int nextRingStart = 1 + (lat + 1) * lonSegments;

            for (int lon = 0; lon < lonSegments; lon++)
            {
                int a = ringStart + lon;
                int b = ringStart + (lon + 1) % lonSegments;
                int c = nextRingStart + lon;
                int d = nextRingStart + (lon + 1) % lonSegments;

                indices[ii++] = a;
                indices[ii++] = c;
                indices[ii++] = b;

                indices[ii++] = b;
                indices[ii++] = c;
                indices[ii++] = d;
            }
        }

        // South pole cap: connect south pole to last ring
        int southPoleIdx = vertexCount - 1;
        int lastRingStart = 1 + (latSegments - 2) * lonSegments;

        for (int lon = 0; lon < lonSegments; lon++)
        {
            int current = lastRingStart + lon;
            int next = lastRingStart + (lon + 1) % lonSegments;
            indices[ii++] = southPoleIdx;
            indices[ii++] = next;
            indices[ii++] = current;
        }

        return new GlobeMeshData(vertices, indices);
    }

    /// <summary>
    /// Calculates the expected vertex count for a UV sphere with the given parameters.
    /// </summary>
    public static int ExpectedVertexCount(int latSegments, int lonSegments)
        => (latSegments - 1) * lonSegments + 2;

    /// <summary>
    /// Calculates the expected triangle count for a UV sphere with the given parameters.
    /// </summary>
    public static int ExpectedTriangleCount(int latSegments, int lonSegments)
        => lonSegments * 2 + (latSegments - 2) * lonSegments * 2;
}

/// <summary>
/// Per-vertex data for the globe mesh.
/// </summary>
public readonly record struct GlobeVertex(Vector3 Position, Vector3 Normal, Vector4 Colour);

/// <summary>
/// Complete mesh data for the globe.
/// </summary>
public sealed record GlobeMeshData(GlobeVertex[] Vertices, int[] Indices)
{
    public int VertexCount => Vertices.Length;
    public int TriangleCount => Indices.Length / 3;
}
