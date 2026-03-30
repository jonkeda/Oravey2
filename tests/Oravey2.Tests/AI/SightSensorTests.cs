using Oravey2.Core.AI.Sensors;

namespace Oravey2.Tests.AI;

public class SightSensorTests
{
    [Fact]
    public void CanDetect_InRangeInCone_True()
    {
        var sensor = new SightSensor { Range = 20f, ConeAngle = 120f };
        Assert.True(sensor.CanDetect(0, 0, 0, 10, 0));
    }

    [Fact]
    public void CanDetect_OutOfRange_False()
    {
        var sensor = new SightSensor { Range = 20f, ConeAngle = 120f };
        Assert.False(sensor.CanDetect(0, 0, 0, 25, 0));
    }

    [Fact]
    public void CanDetect_OutOfCone_False()
    {
        var sensor = new SightSensor { Range = 20f, ConeAngle = 60f };
        // Target at 90° off facing (0°), cone is ±30°
        Assert.False(sensor.CanDetect(0, 0, 0, 0, 10));
    }

    [Fact]
    public void CanDetect_BehindSelf_False()
    {
        var sensor = new SightSensor { Range = 20f, ConeAngle = 120f };
        // Facing 0° (east), target at (-5, 0) = 180° away
        Assert.False(sensor.CanDetect(0, 0, 0, -5, 0));
    }

    [Fact]
    public void CanDetect_EdgeOfCone_True()
    {
        var sensor = new SightSensor { Range = 20f, ConeAngle = 120f };
        // Facing 0°, cone ±60°; target at exactly 60°
        var x = 10f * MathF.Cos(60f * MathF.PI / 180f);
        var y = 10f * MathF.Sin(60f * MathF.PI / 180f);
        Assert.True(sensor.CanDetect(0, 0, 0, x, y));
    }

    [Fact]
    public void GetDetectionScore_CloseTarget_HighScore()
    {
        var sensor = new SightSensor { Range = 20f, ConeAngle = 120f };
        var score = sensor.GetDetectionScore(0, 0, 0, 2, 0);
        Assert.True(score > 0.8f, $"Expected >0.8, got {score}");
    }

    [Fact]
    public void GetDetectionScore_FarTarget_LowScore()
    {
        var sensor = new SightSensor { Range = 20f, ConeAngle = 120f };
        var score = sensor.GetDetectionScore(0, 0, 0, 18, 0);
        Assert.True(score < 0.2f, $"Expected <0.2, got {score}");
    }

    [Fact]
    public void GetDetectionScore_OutOfRange_Zero()
    {
        var sensor = new SightSensor { Range = 20f, ConeAngle = 120f };
        Assert.Equal(0f, sensor.GetDetectionScore(0, 0, 0, 25, 0));
    }
}
