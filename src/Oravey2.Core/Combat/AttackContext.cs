namespace Oravey2.Core.Combat;

public sealed record AttackContext(
    float WeaponAccuracy,
    int WeaponDamage,
    float WeaponRange,
    float CritMultiplier,
    int SkillLevel,
    int Luck,
    int ArmorDR,
    CoverLevel Cover,
    float Distance);
