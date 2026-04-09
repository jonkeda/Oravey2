using System.Numerics;
using Oravey2.MapGen.RegionTemplates;
using Xunit;

namespace Oravey2.Tests.RegionTemplates;

public class GeoMapperTests
{
    private readonly GeoMapper _mapper = new(originLatitude: 52.50, originLongitude: 4.95);

    [Fact]
    public void LatLonToXZ_Origin_ReturnsZero()
    {
        var result = _mapper.LatLonToGameXZ(52.50, 4.95);

        Assert.Equal(0f, result.X, 0.01f);
        Assert.Equal(0f, result.Y, 0.01f);
    }

    [Fact]
    public void LatLonToXZ_KnownDistance_IsAccurate()
    {
        // Purmerend (52.50, 4.95) to Amsterdam (52.37, 4.90)
        // Straight-line distance is approximately 15 km
        var result = _mapper.LatLonToGameXZ(52.37, 4.90);

        double distance = Math.Sqrt(result.X * result.X + result.Y * result.Y);

        // Should be roughly 14-15 km; allow 50 m error
        Assert.InRange(distance, 14_000, 15_500);
    }

    [Fact]
    public void RoundTrip_LatLonToXZToLatLon_MatchesOriginal()
    {
        double testLat = 52.63;
        double testLon = 5.12;

        var xz = _mapper.LatLonToGameXZ(testLat, testLon);
        var (roundLat, roundLon) = _mapper.GameXZToLatLon(xz.X, xz.Y);

        // Error < 1 m at regional scale. 1 m ≈ 0.000009° latitude
        Assert.Equal(testLat, roundLat, 5); // 5 decimal places ≈ ~1 m
        Assert.Equal(testLon, roundLon, 5);
    }

    [Fact]
    public void LatLonToXZ_FarFromOrigin_StillReasonable()
    {
        // ~200 km north: roughly 1.8° latitude
        double farLat = 54.30;
        double farLon = 4.95;

        var result = _mapper.LatLonToGameXZ(farLat, farLon);

        // Expected Z ≈ 200 km
        double distanceMetres = Math.Abs(result.Y);
        Assert.InRange(distanceMetres, 195_000, 205_000);

        // Round-trip error < 500 m
        var (roundLat, roundLon) = _mapper.GameXZToLatLon(result.X, result.Y);
        double latError = Math.Abs(roundLat - farLat) * 111_320; // degrees to metres
        double lonError = Math.Abs(roundLon - farLon) * 111_320 * Math.Cos(farLat * Math.PI / 180);
        double totalError = Math.Sqrt(latError * latError + lonError * lonError);
        Assert.True(totalError < 500, $"Round-trip error {totalError:F0} m exceeds 500 m threshold");
    }
}
