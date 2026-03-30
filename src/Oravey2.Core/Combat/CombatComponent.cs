namespace Oravey2.Core.Combat;

public class CombatComponent
{
    public int MaxAP { get; set; } = CombatFormulas.DefaultMaxAP;
    public float CurrentAP { get; private set; }
    public float APRegenPerSecond { get; set; } = CombatFormulas.DefaultAPRegen;
    public bool InCombat { get; set; }

    public CombatComponent()
    {
        CurrentAP = MaxAP;
    }

    public bool CanAfford(int apCost) => CurrentAP >= apCost;

    public bool Spend(int apCost)
    {
        if (apCost <= 0 || !CanAfford(apCost)) return false;
        CurrentAP -= apCost;
        return true;
    }

    public void Regen(float deltaTime)
    {
        if (!InCombat || deltaTime <= 0) return;
        CurrentAP = Math.Min(MaxAP, CurrentAP + APRegenPerSecond * deltaTime);
    }

    public void ResetAP()
    {
        CurrentAP = MaxAP;
    }
}
