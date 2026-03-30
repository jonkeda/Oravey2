namespace Oravey2.Core.Combat;

public sealed class DamageResolver
{
    private readonly Random _random;

    public DamageResolver(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public DamageResult Resolve(AttackContext context)
    {
        var hitChance = CombatFormulas.HitChance(
            context.WeaponAccuracy, context.SkillLevel,
            context.Cover, context.Distance, context.WeaponRange);

        if (_random.NextDouble() >= hitChance)
            return new DamageResult(Hit: false, Damage: 0, HitLocation.Torso, Critical: false);

        var (location, locationMult) = CombatFormulas.RollHitLocation(_random.NextDouble());

        var critChance = CombatFormulas.CritChance(context.Luck);
        var isCritical = _random.NextDouble() < critChance;

        var damage = CombatFormulas.Damage(
            context.WeaponDamage, context.SkillLevel,
            context.CritMultiplier, isCritical,
            context.ArmorDR, locationMult);

        return new DamageResult(Hit: true, damage, location, isCritical);
    }
}
