using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Combat;

public sealed class CombatEngine
{
    private readonly DamageResolver _resolver;
    private readonly IEventBus _eventBus;

    public CombatEngine(DamageResolver resolver, IEventBus eventBus)
    {
        _resolver = resolver;
        _eventBus = eventBus;
    }

    public DamageResult? ProcessAttack(
        CombatComponent attackerCombat,
        AttackContext context,
        HealthComponent targetHealth,
        int apCost,
        SkillsComponent? attackerSkills = null,
        SkillType? weaponSkillType = null)
    {
        if (!attackerCombat.Spend(apCost))
            return null;

        var result = _resolver.Resolve(context);

        _eventBus.Publish(new AttackResolvedEvent(
            result.Hit, result.Damage, result.Location, result.Critical));

        if (result.Hit)
        {
            targetHealth.TakeDamage(result.Damage);

            if (attackerSkills != null && weaponSkillType != null)
                attackerSkills.AddXP(weaponSkillType.Value, 1);

            if (!targetHealth.IsAlive)
                _eventBus.Publish(new EntityDiedEvent());
        }

        return result;
    }

    public bool ProcessAction(CombatComponent combat, CombatActionType actionType)
    {
        var cost = CombatFormulas.DefaultAPCost(actionType);
        return combat.Spend(cost);
    }
}
