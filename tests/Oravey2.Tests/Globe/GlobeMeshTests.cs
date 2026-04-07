using System.Numerics;
using Oravey2.Core.Globe;

namespace Oravey2.Tests.Globe;

public class GlobeMeshTests
{
    [Fact]
    public void GenerateSphere_CorrectVertexCount()
    {
        var mesh = GlobeMesh.Generate(latSegments: 40, lonSegments: 80);

        int expected = GlobeMesh.ExpectedVertexCount(40, 80);
        Assert.Equal(expected, mesh.VertexCount);
        // (40 - 1) * 80 + 2 = 3122
        Assert.Equal(3122, mesh.VertexCount);
    }

    [Fact]
    public void GenerateSphere_AllNormalsPointOutward()
    {
        var mesh = GlobeMesh.Generate(latSegments: 10, lonSegments: 20, radius: 50f);

        foreach (var v in mesh.Vertices)
        {
            // Normal should point in same direction as position (outward from centre)
            float dot = Vector3.Dot(
                Vector3.Normalize(v.Normal),
                Vector3.Normalize(v.Position));

            Assert.True(dot > 0.99f,
                $"Normal at position {v.Position} does not point outward (dot={dot:F4})");
        }
    }

    [Fact]
    public void HeightDisplacement_OceanAtZero()
    {
        // All vertices at sea level → position length should equal radius
        var mesh = GlobeMesh.Generate(
            latSegments: 4, lonSegments: 8, radius: 100f,
            heightSampler: (_, _) => 0f);

        foreach (var v in mesh.Vertices)
        {
            float length = v.Position.Length();
            Assert.Equal(100f, length, 0.01f);
        }
    }

    [Fact]
    public void HeightDisplacement_MountainAboveZero()
    {
        // All vertices displaced by 10 → position length should equal radius + 10
        var mesh = GlobeMesh.Generate(
            latSegments: 4, lonSegments: 8, radius: 100f,
            heightSampler: (_, _) => 10f);

        foreach (var v in mesh.Vertices)
        {
            float length = v.Position.Length();
            Assert.Equal(110f, length, 0.01f);
        }
    }

    [Fact]
    public void GenerateSphere_CorrectTriangleCount()
    {
        var mesh = GlobeMesh.Generate(latSegments: 10, lonSegments: 20);

        int expected = GlobeMesh.ExpectedTriangleCount(10, 20);
        Assert.Equal(expected, mesh.TriangleCount);
    }

    [Fact]
    public void ColourSampler_AppliedToVertices()
    {
        var red = new Vector4(1, 0, 0, 1);
        var mesh = GlobeMesh.Generate(
            latSegments: 4, lonSegments: 8,
            colourSampler: (_, _) => red);

        foreach (var v in mesh.Vertices)
        {
            Assert.Equal(red, v.Colour);
        }
    }

    [Fact]
    public void MinimalSphere_2Lat3Lon_Generates()
    {
        var mesh = GlobeMesh.Generate(latSegments: 2, lonSegments: 3);

        // (2-1)*3 + 2 = 5 vertices
        Assert.Equal(5, mesh.VertexCount);
        Assert.True(mesh.TriangleCount > 0);
    }
}
