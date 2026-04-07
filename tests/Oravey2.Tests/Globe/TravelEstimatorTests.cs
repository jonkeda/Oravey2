using Oravey2.Core.Globe;

namespace Oravey2.Tests.Globe;

public class TravelEstimatorTests
{
    [Fact]
    public void EstimateTime_100km_OnFoot_Returns20Hours()
    {
        var estimator = new TravelEstimator();

        // 100 km at 5 km/h = 20 hours
        // 100 km = 100_000 m; MetresPerChunk = 32 → chunkDistance = 100_000/32 = 3125
        // To get ~100 km: need chunkDist such that chunkDist * 32 / 1000 ≈ 100
        // chunkDist ≈ 3125 chunks. Use straight-line: (3125, 0)
        int chunkDist = (int)(100f * 1000f / TravelEstimator.MetresPerChunk);
        var result = estimator.Estimate(0, 0, chunkDist, 0, TravelMode.OnFoot);

        Assert.NotNull(result);
        Assert.Equal(100f, result.DistanceKm, 1f);
        Assert.Equal(20f, result.EstimatedHours, 0.5f);
        Assert.Equal(0f, result.FuelCost); // on foot = no fuel
    }

    [Fact]
    public void EstimateTime_100km_Vehicle_Returns2Hours()
    {
        var estimator = new TravelEstimator();

        int chunkDist = (int)(100f * 1000f / TravelEstimator.MetresPerChunk);
        var result = estimator.Estimate(0, 0, chunkDist, 0, TravelMode.Vehicle);

        Assert.NotNull(result);
        Assert.Equal(100f, result.DistanceKm, 1f);
        Assert.Equal(2f, result.EstimatedHours, 0.1f);
    }

    [Fact]
    public void NoKnownRoute_ReturnsNull()
    {
        var estimator = new TravelEstimator();
        var result = estimator.Estimate(0, 0, 100, 100, hasRoute: false);

        Assert.Null(result);
    }

    [Fact]
    public void FuelConsumption_ProportionalToDistance()
    {
        var estimator = new TravelEstimator();

        // 50 km distance
        int chunks50 = (int)(50f * 1000f / TravelEstimator.MetresPerChunk);
        var result50 = estimator.Estimate(0, 0, chunks50, 0, TravelMode.Vehicle);

        // 100 km distance
        int chunks100 = (int)(100f * 1000f / TravelEstimator.MetresPerChunk);
        var result100 = estimator.Estimate(0, 0, chunks100, 0, TravelMode.Vehicle);

        Assert.NotNull(result50);
        Assert.NotNull(result100);

        // Fuel should be roughly double
        Assert.Equal(result50.FuelCost * 2, result100.FuelCost, 1f);

        // Verify fuel = distance * FuelPerKm
        Assert.Equal(result100.DistanceKm * TravelEstimator.FuelPerKm, result100.FuelCost, 0.1f);
    }

    [Fact]
    public void OnFoot_NoFuelConsumed()
    {
        var estimator = new TravelEstimator();
        var result = estimator.Estimate(0, 0, 100, 0, TravelMode.OnFoot);

        Assert.NotNull(result);
        Assert.Equal(0f, result.FuelCost);
    }

    [Fact]
    public void SamePosition_ZeroDistance()
    {
        var estimator = new TravelEstimator();
        var result = estimator.Estimate(5, 5, 5, 5, TravelMode.OnFoot);

        Assert.NotNull(result);
        Assert.Equal(0f, result.DistanceKm);
        Assert.Equal(0f, result.EstimatedHours);
    }
}
