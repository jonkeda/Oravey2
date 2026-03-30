namespace Oravey2.Core.Survival;

public enum SurvivalThreshold
{
    Satisfied,   // 0–25: buff active
    Normal,      // 26–50: no effects
    Deprived,    // 51–75: stat debuff
    Critical     // 76–100: HP drain + debuff
}
