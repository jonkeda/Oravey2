namespace Oravey2.Core.Combat;

public static class CombatFormulas
{
    public const int DefaultMaxAP = 10;
    public const float DefaultAPRegen = 2f;

    public static float HitChance(
        float weaponAccuracy, int skillLevel,
        CoverLevel cover, float distance, float weaponRange)
    {
        var skillMod = 1.0f + skillLevel / 200f;
        var coverMod = CoverMultiplier(cover);
        var rangeMod = RangeMultiplier(distance, weaponRange);
        return Math.Clamp(weaponAccuracy * skillMod * coverMod * rangeMod, 0f, 0.95f);
    }

    public static float CoverMultiplier(CoverLevel cover) => cover switch
    {
        CoverLevel.Half => 0.70f,
        CoverLevel.Full => 0.40f,
        _ => 1.0f
    };

    public static float RangeMultiplier(float distance, float weaponRange)
        => Math.Max(0.3f, 1.0f - distance / (weaponRange * 1.5f));

    public static int Damage(
        int weaponDamage, int skillLevel,
        float critMultiplier, bool isCritical,
        int armorDR, float locationMultiplier)
    {
        var skillMod = 1.0f + skillLevel / 100f;
        var crit = isCritical ? critMultiplier : 1.0f;
        var raw = (int)(weaponDamage * skillMod * crit * locationMultiplier);
        return Math.Max(1, raw - armorDR);
    }

    public static float CritChance(int luck) => luck * 0.01f;

    public static (HitLocation Location, float DamageMultiplier) RollHitLocation(double roll)
    {
        if (roll < 0.40) return (HitLocation.Torso, 1.0f);
        if (roll < 0.50) return (HitLocation.Head, 1.5f);
        if (roll < 0.75) return (HitLocation.Arms, 0.8f);
        return (HitLocation.Legs, 0.8f);
    }

    public static int DefaultAPCost(CombatActionType action) => action switch
    {
        CombatActionType.MeleeAttack => 3,
        CombatActionType.RangedAttack => 2,
        CombatActionType.Reload => 2,
        CombatActionType.UseItem => 2,
        CombatActionType.Move => 1,
        CombatActionType.TakeCover => 1,
        _ => 0
    };
}
