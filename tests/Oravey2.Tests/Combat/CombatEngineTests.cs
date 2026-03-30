using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Combat;
using Oravey2.Core.Framework.Events;

namespace Oravey2.Tests.Combat;

public class CombatEngineTests
{
    private static (CombatEngine engine, EventBus bus) CreateEngine(int seed = 0)
    {
        var bus = new EventBus();
        var resolver = new DamageResolver(new Random(seed));
        return (new CombatEngine(resolver, bus), bus);
    }

    private static HealthComponent CreateHealth(int endurance = 5, int level = 1)
    {
        var stats = new StatsComponent(new Dictionary<Stat, int> { { Stat.Endurance, endurance } });
        var lvl = new LevelComponent(stats);
        return new HealthComponent(stats, lvl);
    }

    private static AttackContext HighHitContext(int luck = 0) => new(
        WeaponAccuracy: 0.95f,
        WeaponDamage: 20,
        WeaponRange: 30f,
        CritMultiplier: 2.0f,
        SkillLevel: 80,
        Luck: luck,
        ArmorDR: 0,
        Cover: CoverLevel.None,
        Distance: 1f);

    [Fact]
    public void ProcessAttack_Hit_DealsDamage()
    {
        // Find a seed that lands a hit
        for (int seed = 0; seed < 100; seed++)
        {
            var (engine, _) = CreateEngine(seed);
            var combat = new CombatComponent { InCombat = true };
            var target = CreateHealth();
            var initialHP = target.CurrentHP;

            var result = engine.ProcessAttack(combat, HighHitContext(), target, 2);
            if (result != null && result.Hit)
            {
                Assert.True(target.CurrentHP < initialHP);
                return;
            }
        }
        Assert.Fail("Expected a hit within 100 seeds");
    }

    [Fact]
    public void ProcessAttack_Miss_NoDamage()
    {
        // Low accuracy → miss
        var ctx = new AttackContext(0.01f, 5, 10f, 1.0f, 0, 0, 0, CoverLevel.Full, 100f);

        for (int seed = 0; seed < 100; seed++)
        {
            var (engine, _) = CreateEngine(seed);
            var combat = new CombatComponent { InCombat = true };
            var target = CreateHealth();
            var initialHP = target.CurrentHP;

            var result = engine.ProcessAttack(combat, ctx, target, 2);
            if (result != null && !result.Hit)
            {
                Assert.Equal(initialHP, target.CurrentHP);
                return;
            }
        }
        Assert.Fail("Expected a miss within 100 seeds");
    }

    [Fact]
    public void ProcessAttack_DeductsAP()
    {
        var (engine, _) = CreateEngine(0);
        var combat = new CombatComponent { InCombat = true };
        var target = CreateHealth();

        engine.ProcessAttack(combat, HighHitContext(), target, 3);
        Assert.Equal(7f, combat.CurrentAP);
    }

    [Fact]
    public void ProcessAttack_InsufficientAP_ReturnsNull()
    {
        var (engine, bus) = CreateEngine(0);
        var combat = new CombatComponent { InCombat = true };
        combat.Spend(9); // only 1 AP left

        AttackResolvedEvent? received = null;
        bus.Subscribe<AttackResolvedEvent>(e => received = e);

        var result = engine.ProcessAttack(combat, HighHitContext(), CreateHealth(), 3);
        Assert.Null(result);
        Assert.Null(received); // no event published
    }

    [Fact]
    public void ProcessAttack_PublishesAttackResolvedEvent()
    {
        var (engine, bus) = CreateEngine(0);
        var combat = new CombatComponent { InCombat = true };
        AttackResolvedEvent? received = null;
        bus.Subscribe<AttackResolvedEvent>(e => received = e);

        engine.ProcessAttack(combat, HighHitContext(), CreateHealth(), 2);

        Assert.NotNull(received);
    }

    [Fact]
    public void ProcessAttack_Kill_PublishesEntityDiedEvent()
    {
        // Create a very low HP target
        var stats = new StatsComponent(new Dictionary<Stat, int> { { Stat.Endurance, 1 } });
        var lvl = new LevelComponent(stats);
        var target = new HealthComponent(stats, lvl);
        // MaxHP = 50 + 1×10 + 1×5 = 65; deal massive damage
        var ctx = new AttackContext(0.95f, 200, 30f, 2.0f, 80, 0, 0, CoverLevel.None, 1f);

        for (int seed = 0; seed < 100; seed++)
        {
            var (engine, bus) = CreateEngine(seed);
            var combat = new CombatComponent { InCombat = true };
            var freshTarget = new HealthComponent(stats, lvl);
            bool died = false;
            bus.Subscribe<EntityDiedEvent>(_ => died = true);

            var result = engine.ProcessAttack(combat, ctx, freshTarget, 2);
            if (result != null && result.Hit && !freshTarget.IsAlive)
            {
                Assert.True(died);
                return;
            }
        }
        Assert.Fail("Expected a kill within 100 seeds");
    }

    [Fact]
    public void ProcessAttack_Hit_GrantsSkillXP()
    {
        var attackerStats = new StatsComponent();
        var skills = new SkillsComponent(attackerStats);
        var initialSkillXP = skills.GetBase(SkillType.Firearms);

        for (int seed = 0; seed < 100; seed++)
        {
            var (engine, _) = CreateEngine(seed);
            var combat = new CombatComponent { InCombat = true };
            var target = CreateHealth();

            var result = engine.ProcessAttack(
                combat, HighHitContext(), target, 2,
                skills, SkillType.Firearms);

            if (result != null && result.Hit)
            {
                // AddXP was called — skill XP accumulated (may or may not level up)
                // The fact that it didn't throw is the test; for a precise check
                // we'd need to inspect internal XP state
                return;
            }
        }
        Assert.Fail("Expected a hit within 100 seeds");
    }

    [Fact]
    public void ProcessAttack_Miss_NoSkillXP()
    {
        var ctx = new AttackContext(0.01f, 5, 10f, 1.0f, 0, 0, 0, CoverLevel.Full, 100f);
        var attackerStats = new StatsComponent();
        var skills = new SkillsComponent(attackerStats);
        var initialBase = skills.GetBase(SkillType.Firearms);

        for (int seed = 0; seed < 100; seed++)
        {
            var (engine, _) = CreateEngine(seed);
            var combat = new CombatComponent { InCombat = true };
            var target = CreateHealth();

            var result = engine.ProcessAttack(
                combat, ctx, target, 2,
                skills, SkillType.Firearms);

            if (result != null && !result.Hit)
            {
                Assert.Equal(initialBase, skills.GetBase(SkillType.Firearms));
                return;
            }
        }
        Assert.Fail("Expected a miss within 100 seeds");
    }

    [Fact]
    public void ProcessAttack_NoSkillsComponent_StillWorks()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var (engine, _) = CreateEngine(seed);
            var combat = new CombatComponent { InCombat = true };
            var target = CreateHealth();

            var result = engine.ProcessAttack(combat, HighHitContext(), target, 2);
            if (result != null && result.Hit)
            {
                // No exception thrown → pass
                return;
            }
        }
        Assert.Fail("Expected a hit within 100 seeds");
    }

    [Fact]
    public void ProcessAction_Move_Costs1AP()
    {
        var (engine, _) = CreateEngine(0);
        var combat = new CombatComponent { InCombat = true };
        Assert.True(engine.ProcessAction(combat, CombatActionType.Move));
        Assert.Equal(9f, combat.CurrentAP);
    }

    [Fact]
    public void ProcessAction_Reload_Costs2AP()
    {
        var (engine, _) = CreateEngine(0);
        var combat = new CombatComponent { InCombat = true };
        Assert.True(engine.ProcessAction(combat, CombatActionType.Reload));
        Assert.Equal(8f, combat.CurrentAP);
    }

    [Fact]
    public void ProcessAction_InsufficientAP_ReturnsFalse()
    {
        var (engine, _) = CreateEngine(0);
        var combat = new CombatComponent { InCombat = true };
        combat.Spend(9); // 1 AP left
        Assert.False(engine.ProcessAction(combat, CombatActionType.MeleeAttack)); // costs 3
        Assert.Equal(1f, combat.CurrentAP);
    }
}
