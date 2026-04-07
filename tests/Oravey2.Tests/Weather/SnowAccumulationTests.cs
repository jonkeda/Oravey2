using Oravey2.Core.Audio;
using Oravey2.Core.Weather;

namespace Oravey2.Tests.Weather;

public class SnowAccumulationTests
{
    [Fact]
    public void Snowing_AccumulationIncreases()
    {
        var snow = new SnowAccumulation();

        snow.Tick(WeatherState.Snow, 1.0f, deltaSec: 10f);

        Assert.True(snow.Coverage > 0f,
            $"Expected coverage > 0 after snowing, got {snow.Coverage:F4}");
        // 0.02 * 1.0 * 10 = 0.2
        Assert.Equal(0.2f, snow.Coverage, 0.01f);
    }

    [Fact]
    public void ClearAfterSnow_Melts()
    {
        var snow = new SnowAccumulation();

        // Accumulate some snow
        snow.Tick(WeatherState.Snow, 1.0f, deltaSec: 20f);
        float afterSnow = snow.Coverage;
        Assert.True(afterSnow > 0f);

        // Clear weather → melting
        snow.Tick(WeatherState.Clear, 0f, deltaSec: 10f);
        Assert.True(snow.Coverage < afterSnow,
            $"Coverage should decrease when clear; was {afterSnow:F3}, now {snow.Coverage:F3}");
    }

    [Fact]
    public void TrackReducesCoverage()
    {
        var snow = new SnowAccumulation();

        // Build up coverage
        snow.Tick(WeatherState.Snow, 1.0f, deltaSec: 30f);
        float before = snow.Coverage;
        Assert.True(before > 0f);

        snow.ApplyTrack();
        Assert.True(snow.Coverage < before,
            $"Track should reduce coverage; was {before:F3}, now {snow.Coverage:F3}");
    }

    [Fact]
    public void Coverage_ClampsAt1()
    {
        var snow = new SnowAccumulation();

        // Snow for a long time
        snow.Tick(WeatherState.Snow, 1.0f, deltaSec: 1000f);

        Assert.Equal(1f, snow.Coverage);
    }

    [Fact]
    public void Coverage_ClampsAt0()
    {
        var snow = new SnowAccumulation();

        // Melt without any snow
        snow.Tick(WeatherState.Clear, 0f, deltaSec: 1000f);

        Assert.Equal(0f, snow.Coverage);
    }

    [Fact]
    public void IntensityAffectsRate()
    {
        var snow1 = new SnowAccumulation();
        var snow2 = new SnowAccumulation();

        snow1.Tick(WeatherState.Snow, 0.5f, deltaSec: 10f);
        snow2.Tick(WeatherState.Snow, 1.0f, deltaSec: 10f);

        Assert.True(snow2.Coverage > snow1.Coverage,
            "Higher intensity should accumulate faster");
        // 0.02 * 0.5 * 10 = 0.1 vs 0.02 * 1.0 * 10 = 0.2
        Assert.Equal(0.1f, snow1.Coverage, 0.01f);
        Assert.Equal(0.2f, snow2.Coverage, 0.01f);
    }

    [Fact]
    public void Reset_ClearsCoverage()
    {
        var snow = new SnowAccumulation();
        snow.Tick(WeatherState.Snow, 1.0f, deltaSec: 20f);
        Assert.True(snow.Coverage > 0f);

        snow.Reset();
        Assert.Equal(0f, snow.Coverage);
    }
}
