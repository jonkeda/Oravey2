namespace Oravey2.Core.AI.Sensors;

public sealed class HearingSensor : ISensor
{
    public float Radius { get; set; } = 12f;

    public bool CanDetect(float selfX, float selfY, float selfFacingDeg,
                          float targetX, float targetY)
    {
        var dx = targetX - selfX;
        var dy = targetY - selfY;
        return dx * dx + dy * dy <= Radius * Radius;
    }

    public float GetDetectionScore(float selfX, float selfY, float selfFacingDeg,
                                   float targetX, float targetY)
    {
        var dx = targetX - selfX;
        var dy = targetY - selfY;
        var distSq = dx * dx + dy * dy;
        if (distSq > Radius * Radius) return 0f;

        return 1.0f - MathF.Sqrt(distSq) / Radius;
    }
}
