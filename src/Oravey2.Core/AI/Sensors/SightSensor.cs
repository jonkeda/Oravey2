namespace Oravey2.Core.AI.Sensors;

public sealed class SightSensor : ISensor
{
    public float Range { get; set; } = 20f;
    public float ConeAngle { get; set; } = 120f;

    public bool CanDetect(float selfX, float selfY, float selfFacingDeg,
                          float targetX, float targetY)
    {
        var dx = targetX - selfX;
        var dy = targetY - selfY;
        var dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist > Range) return false;

        var angleToTarget = MathF.Atan2(dy, dx) * (180f / MathF.PI);
        var delta = NormalizeAngle(angleToTarget - selfFacingDeg);
        return MathF.Abs(delta) <= ConeAngle / 2f;
    }

    public float GetDetectionScore(float selfX, float selfY, float selfFacingDeg,
                                   float targetX, float targetY)
    {
        if (!CanDetect(selfX, selfY, selfFacingDeg, targetX, targetY)) return 0f;

        var dx = targetX - selfX;
        var dy = targetY - selfY;
        var dist = MathF.Sqrt(dx * dx + dy * dy);

        return 1.0f - dist / Range;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}
