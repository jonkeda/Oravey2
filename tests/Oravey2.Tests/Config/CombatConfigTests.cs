using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Combat;
using Oravey2.Core.Inventory.Items;
using Xunit;

namespace Oravey2.Tests.Config;

public class CombatConfigTests
{
    [Fact]
    public void PipeWrench_HasExpectedStats()
    {
        var wrench = M0Items.PipeWrench();
        Assert.NotNull(wrench.Weapon);
        Assert.Equal(14, wrench.Weapon.Damage);
        Assert.Equal(0.80f, wrench.Weapon.Accuracy);
        Assert.Equal(2f, wrench.Weapon.Range);
        Assert.Equal(3, wrench.Weapon.ApCost);
    }

    [Fact]
    public void RustyShiv_HasExpectedStats()
    {
        var shiv = M0Items.RustyShiv();
        Assert.NotNull(shiv.Weapon);
        Assert.Equal(4, shiv.Weapon.Damage);
        Assert.Equal(0.50f, shiv.Weapon.Accuracy);
    }

    [Fact]
    public void EnemyWeapon_LowerDamageThanPlayer()
    {
        var wrench = M0Items.PipeWrench();
        var shiv = M0Items.RustyShiv();
        Assert.True(shiv.Weapon!.Damage < wrench.Weapon!.Damage);
    }

    [Fact]
    public void EnemyHp_WithEndurance1_Is65()
    {
        // Enemy stats: Endurance=1, Level=1
        var stats = new StatsComponent(new Dictionary<Stat, int> { { Stat.Endurance, 1 } });
        var level = new LevelComponent(stats);
        Assert.Equal(65, LevelFormulas.MaxHP(stats.GetEffective(Stat.Endurance), level.Level));
    }

    [Fact]
    public void DefaultMaxAP_Is10()
    {
        Assert.Equal(10, CombatFormulas.DefaultMaxAP);
    }

    [Fact]
    public void CombatComponent_StartsAtMaxAP()
    {
        var combat = new CombatComponent();
        Assert.Equal(combat.MaxAP, (int)combat.CurrentAP);
    }
}
