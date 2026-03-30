using Oravey2.Core.AI.Sensors;

namespace Oravey2.Tests.AI;

public class HearingSensorTests
{
    [Fact]
    public void CanDetect_InRadius_True()
    {
        var sensor = new HearingSensor { Radius = 12f };
        Assert.True(sensor.CanDetect(0, 0, 0, 5, 0));
    }

    [Fact]
    public void CanDetect_OutOfRadius_False()
    {
        var sensor = new HearingSensor { Radius = 12f };
        Assert.False(sensor.CanDetect(0, 0, 0, 15, 0));
    }

    [Fact]
    public void CanDetect_IgnoresFacing()
    {
        var sensor = new HearingSensor { Radius = 12f };
        // Facing 0° (east), target behind at (-5, 0)
        Assert.True(sensor.CanDetect(0, 0, 0, -5, 0));
    }

    [Fact]
    public void GetDetectionScore_Close_High()
    {
        var sensor = new HearingSensor { Radius = 12f };
        var score = sensor.GetDetectionScore(0, 0, 0, 1, 0);
        Assert.True(score > 0.9f, $"Expected >0.9, got {score}");
    }

    [Fact]
    public void GetDetectionScore_Edge_Low()
    {
        var sensor = new HearingSensor { Radius = 12f };
        var score = sensor.GetDetectionScore(0, 0, 0, 11.5f, 0);
        Assert.True(score < 0.1f, $"Expected <0.1, got {score}");
    }

    [Fact]
    public void GetDetectionScore_OutOfRadius_Zero()
    {
        var sensor = new HearingSensor { Radius = 12f };
        Assert.Equal(0f, sensor.GetDetectionScore(0, 0, 0, 15, 0));
    }
}
