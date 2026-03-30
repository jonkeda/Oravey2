using Oravey2.Core.Combat;

namespace Oravey2.Tests.Combat;

public class CombatComponentTests
{
    [Fact]
    public void Defaults_MaxAP10_RegenRate2()
    {
        var cc = new CombatComponent();
        Assert.Equal(10, cc.MaxAP);
        Assert.Equal(2f, cc.APRegenPerSecond);
        Assert.Equal(10f, cc.CurrentAP);
    }

    [Fact]
    public void CanAfford_Sufficient_ReturnsTrue()
    {
        var cc = new CombatComponent();
        Assert.True(cc.CanAfford(3));
    }

    [Fact]
    public void CanAfford_Insufficient_ReturnsFalse()
    {
        var cc = new CombatComponent();
        Assert.False(cc.CanAfford(11));
    }

    [Fact]
    public void Spend_DeductsAP()
    {
        var cc = new CombatComponent();
        Assert.True(cc.Spend(3));
        Assert.Equal(7f, cc.CurrentAP);
    }

    [Fact]
    public void Spend_Insufficient_ReturnsFalse()
    {
        var cc = new CombatComponent();
        Assert.False(cc.Spend(11));
        Assert.Equal(10f, cc.CurrentAP);
    }

    [Fact]
    public void Spend_ZeroOrNegative_ReturnsFalse()
    {
        var cc = new CombatComponent();
        Assert.False(cc.Spend(0));
        Assert.False(cc.Spend(-1));
        Assert.Equal(10f, cc.CurrentAP);
    }

    [Fact]
    public void Regen_IncreasesAP()
    {
        var cc = new CombatComponent { InCombat = true };
        cc.Spend(5);
        cc.Regen(0.5f); // 2/s × 0.5s = 1 AP
        Assert.Equal(6f, cc.CurrentAP);
    }

    [Fact]
    public void Regen_CapsAtMax()
    {
        var cc = new CombatComponent { InCombat = true };
        cc.Regen(100f);
        Assert.Equal(10f, cc.CurrentAP);
    }

    [Fact]
    public void Regen_NotInCombat_NoEffect()
    {
        var cc = new CombatComponent();
        cc.Spend(5);
        cc.Regen(1f);
        Assert.Equal(5f, cc.CurrentAP);
    }

    [Fact]
    public void Regen_NegativeDelta_NoEffect()
    {
        var cc = new CombatComponent { InCombat = true };
        cc.Spend(5);
        cc.Regen(-1f);
        Assert.Equal(5f, cc.CurrentAP);
    }

    [Fact]
    public void ResetAP_RestoresToMax()
    {
        var cc = new CombatComponent();
        cc.Spend(5);
        cc.ResetAP();
        Assert.Equal(10f, cc.CurrentAP);
    }

    [Fact]
    public void CustomMaxAP_Respected()
    {
        var cc = new CombatComponent { MaxAP = 15 };
        cc.ResetAP();
        Assert.Equal(15f, cc.CurrentAP);
    }
}
