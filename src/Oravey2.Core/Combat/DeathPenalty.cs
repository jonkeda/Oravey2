namespace Oravey2.Core.Combat;

public static class DeathPenalty
{
    public const float CapsLossPercent = 0.10f;

    public static int CalculateCapsLoss(int currentCaps)
        => currentCaps <= 0 ? 0 : (int)(currentCaps * CapsLossPercent);
}
