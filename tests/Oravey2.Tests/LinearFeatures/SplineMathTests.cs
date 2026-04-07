using System.Numerics;
using Oravey2.Core.World.LinearFeatures;

namespace Oravey2.Tests.LinearFeatures;

public class SplineMathTests
{
    [Fact]
    public void CatmullRom_Midpoint_IsAverage()
    {
        // 4 collinear points along X axis
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(1, 0);
        var p2 = new Vector2(2, 0);
        var p3 = new Vector2(3, 0);

        var mid = SplineMath.CatmullRomEvaluate(p0, p1, p2, p3, 0.5f);

        // For collinear points, midpoint should be average of P1 and P2
        Assert.Equal(1.5f, mid.X, 0.01f);
        Assert.Equal(0f, mid.Y, 0.01f);
    }

    [Fact]
    public void CatmullRom_Endpoints_MatchControlPoints()
    {
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(1, 2);
        var p2 = new Vector2(3, 4);
        var p3 = new Vector2(5, 6);

        // t=0 → P1
        var atZero = SplineMath.CatmullRomEvaluate(p0, p1, p2, p3, 0f);
        Assert.Equal(p1.X, atZero.X, 0.001f);
        Assert.Equal(p1.Y, atZero.Y, 0.001f);

        // t=1 → P2
        var atOne = SplineMath.CatmullRomEvaluate(p0, p1, p2, p3, 1f);
        Assert.Equal(p2.X, atOne.X, 0.001f);
        Assert.Equal(p2.Y, atOne.Y, 0.001f);
    }

    [Fact]
    public void CatmullRom_Tangent_IsNonZero()
    {
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(1, 1);
        var p2 = new Vector2(3, 2);
        var p3 = new Vector2(5, 3);

        var tangent = SplineMath.CatmullRomTangent(p0, p1, p2, p3, 0.5f);

        Assert.True(tangent.Length() > 0.01f, "Tangent should be non-zero at t=0.5");
    }

    [Fact]
    public void ArcLengthSampling_UniformSpacing()
    {
        // Create a curved spline
        var nodes = new List<Vector2>
        {
            new(0, 0),
            new(5, 3),
            new(10, 0),
            new(15, 3),
        };

        var samples = SplineMath.SampleSpline(nodes, samplesPerSegment: 8);

        // Check that distances between consecutive samples are approximately equal
        var distances = new List<float>();
        for (int i = 1; i < samples.Length; i++)
            distances.Add(Vector2.Distance(samples[i - 1].Position, samples[i].Position));

        float avgDist = distances.Average();
        foreach (float d in distances)
        {
            // Allow 30% tolerance for arc-length uniformity
            Assert.InRange(d, avgDist * 0.7f, avgDist * 1.3f);
        }
    }

    [Fact]
    public void SampleSpline_TwoNodes_ReturnsLinearSamples()
    {
        var nodes = new List<Vector2>
        {
            new(0, 0),
            new(10, 0),
        };

        var samples = SplineMath.SampleSpline(nodes, samplesPerSegment: 4);

        Assert.Equal(5, samples.Length); // 1 segment × 4 + 1
        // All Y should be ~0 for a horizontal line
        foreach (var (pos, _) in samples)
            Assert.InRange(pos.Y, -0.5f, 0.5f);
    }

    [Fact]
    public void ComputeArcLength_StraightLine_EqualsDistance()
    {
        var nodes = new List<Vector2>
        {
            new(0, 0),
            new(10, 0),
        };

        float arcLength = SplineMath.ComputeArcLength(nodes);

        Assert.Equal(10f, arcLength, 0.1f);
    }

    [Fact]
    public void SampleSpline_SingleNode_ReturnsSingleSample()
    {
        var nodes = new List<Vector2> { new(5, 5) };

        var samples = SplineMath.SampleSpline(nodes);

        Assert.Single(samples);
        Assert.Equal(5f, samples[0].Position.X, 0.001f);
        Assert.Equal(5f, samples[0].Position.Y, 0.001f);
    }
}
