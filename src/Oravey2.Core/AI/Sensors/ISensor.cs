namespace Oravey2.Core.AI.Sensors;

public interface ISensor
{
    bool CanDetect(float selfX, float selfY, float selfFacingDeg,
                   float targetX, float targetY);

    float GetDetectionScore(float selfX, float selfY, float selfFacingDeg,
                            float targetX, float targetY);
}
