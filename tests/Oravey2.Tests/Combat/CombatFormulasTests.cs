using Oravey2.Core.Combat;

namespace Oravey2.Tests.Combat;

public class CombatFormulasTests
{
    // ── Cover Multiplier ──

    [Fact]
    public void CoverMultiplier_None_Returns1()
        => Assert.Equal(1.0f, CombatFormulas.CoverMultiplier(CoverLevel.None));

    [Fact]
    public void CoverMultiplier_Half_Returns070()
        => Assert.Equal(0.70f, CombatFormulas.CoverMultiplier(CoverLevel.Half));

    [Fact]
    public void CoverMultiplier_Full_Returns040()
        => Assert.Equal(0.40f, CombatFormulas.CoverMultiplier(CoverLevel.Full));

    // ── Range Multiplier ──

    [Fact]
    public void RangeMultiplier_AtZero_Returns1()
        => Assert.Equal(1.0f, CombatFormulas.RangeMultiplier(0f, 15f));

    [Fact]
    public void RangeMultiplier_AtMaxRange_Positive()
        => Assert.True(CombatFormulas.RangeMultiplier(15f, 15f) > 0.3f);

    [Fact]
    public void RangeMultiplier_BeyondRange_ClampedAt03()
        => Assert.Equal(0.3f, CombatFormulas.RangeMultiplier(100f, 10f));

    // ── Hit Chance ──

    [Fact]
    public void HitChance_HighAccuracy_NoCover_CloseRange()
    {
        // 0.65 × (1 + 20/200) × 1.0 × 1.0 = 0.65 × 1.1 = 0.715
        var result = CombatFormulas.HitChance(0.65f, 20, CoverLevel.None, 0f, 15f);
        Assert.Equal(0.715f, result, 0.01f);
    }

    [Fact]
    public void HitChance_Capped_At_095()
    {
        var result = CombatFormulas.HitChance(1.0f, 200, CoverLevel.None, 0f, 100f);
        Assert.Equal(0.95f, result);
    }

    [Fact]
    public void HitChance_FullCover_Reduces()
    {
        var noCover = CombatFormulas.HitChance(0.65f, 20, CoverLevel.None, 5f, 15f);
        var fullCover = CombatFormulas.HitChance(0.65f, 20, CoverLevel.Full, 5f, 15f);
        Assert.True(fullCover < noCover);
    }

    // ── Damage ──

    [Fact]
    public void Damage_BasicHit_NoArmor()
    {
        // 12 × (1 + 20/100) × 1.0 × 1.0 = 12 × 1.2 = 14.4 → 14
        Assert.Equal(14, CombatFormulas.Damage(12, 20, 2.0f, false, 0, 1.0f));
    }

    [Fact]
    public void Damage_WithArmor_Reduced()
    {
        // 14 - 5 = 9
        Assert.Equal(9, CombatFormulas.Damage(12, 20, 2.0f, false, 5, 1.0f));
    }

    [Fact]
    public void Damage_CriticalHit_Doubled()
    {
        // 12 × 1.2 × 2.0 × 1.0 = 28.8 → 28
        Assert.Equal(28, CombatFormulas.Damage(12, 20, 2.0f, true, 0, 1.0f));
    }

    [Fact]
    public void Damage_HeadshotMultiplier()
    {
        // 12 × 1.2 × 1.0 × 1.5 = 21.6 → 21
        Assert.Equal(21, CombatFormulas.Damage(12, 20, 2.0f, false, 0, 1.5f));
    }

    [Fact]
    public void Damage_MinimumOne()
    {
        // 1 × 1.0 × 1.0 × 0.8 = 0.8 → 0, then 0 - 99 = -99 → clamped to 1
        Assert.Equal(1, CombatFormulas.Damage(1, 0, 1.0f, false, 99, 0.8f));
    }

    // ── Crit Chance ──

    [Fact]
    public void CritChance_Luck5_Returns005()
        => Assert.Equal(0.05f, CombatFormulas.CritChance(5), 0.001f);

    [Fact]
    public void CritChance_Luck0_ReturnsZero()
        => Assert.Equal(0f, CombatFormulas.CritChance(0), 0.001f);

    // ── Hit Location ──

    [Fact]
    public void RollHitLocation_Torso()
    {
        var (loc, mult) = CombatFormulas.RollHitLocation(0.0);
        Assert.Equal(HitLocation.Torso, loc);
        Assert.Equal(1.0f, mult);
    }

    [Fact]
    public void RollHitLocation_Head()
    {
        var (loc, mult) = CombatFormulas.RollHitLocation(0.45);
        Assert.Equal(HitLocation.Head, loc);
        Assert.Equal(1.5f, mult);
    }

    [Fact]
    public void RollHitLocation_Arms()
    {
        var (loc, mult) = CombatFormulas.RollHitLocation(0.60);
        Assert.Equal(HitLocation.Arms, loc);
        Assert.Equal(0.8f, mult);
    }

    [Fact]
    public void RollHitLocation_Legs()
    {
        var (loc, mult) = CombatFormulas.RollHitLocation(0.80);
        Assert.Equal(HitLocation.Legs, loc);
        Assert.Equal(0.8f, mult);
    }

    [Fact]
    public void RollHitLocation_Boundary_Torso()
    {
        var (loc, _) = CombatFormulas.RollHitLocation(0.39);
        Assert.Equal(HitLocation.Torso, loc);
    }

    [Fact]
    public void RollHitLocation_Boundary_Head()
    {
        var (loc, _) = CombatFormulas.RollHitLocation(0.40);
        Assert.Equal(HitLocation.Head, loc);
    }

    // ── AP Costs ──

    [Fact]
    public void DefaultAPCost_MeleeAttack_3()
        => Assert.Equal(3, CombatFormulas.DefaultAPCost(CombatActionType.MeleeAttack));

    [Fact]
    public void DefaultAPCost_RangedAttack_2()
        => Assert.Equal(2, CombatFormulas.DefaultAPCost(CombatActionType.RangedAttack));

    [Fact]
    public void DefaultAPCost_Move_1()
        => Assert.Equal(1, CombatFormulas.DefaultAPCost(CombatActionType.Move));

    [Fact]
    public void DefaultAPCost_TakeCover_1()
        => Assert.Equal(1, CombatFormulas.DefaultAPCost(CombatActionType.TakeCover));

    [Fact]
    public void DefaultAPCost_Reload_2()
        => Assert.Equal(2, CombatFormulas.DefaultAPCost(CombatActionType.Reload));

    [Fact]
    public void DefaultAPCost_UseItem_2()
        => Assert.Equal(2, CombatFormulas.DefaultAPCost(CombatActionType.UseItem));
}
