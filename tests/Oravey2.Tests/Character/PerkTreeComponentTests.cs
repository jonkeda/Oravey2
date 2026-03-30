using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Perks;
using Oravey2.Core.Character.Stats;

namespace Oravey2.Tests.Character;

public class PerkTreeComponentTests
{
    private static readonly PerkDefinition IronFist1 = new(
        "iron_fist_1", "Iron Fist I", "Melee +15%",
        new PerkCondition(2, Stat.Strength, 3),
        ["damage:melee:+0.15"]);

    private static readonly PerkDefinition IronFist2 = new(
        "iron_fist_2", "Iron Fist II", "Melee +15% more",
        new PerkCondition(7, Stat.Strength, 5, "iron_fist_1"),
        ["damage:melee:+0.15"]);

    private static readonly PerkDefinition PerkA = new(
        "perk_a", "Perk A", "A",
        new PerkCondition(2),
        ["special:a"],
        MutuallyExclusive: ["perk_b"]);

    private static readonly PerkDefinition PerkB = new(
        "perk_b", "Perk B", "B",
        new PerkCondition(2),
        ["special:b"],
        MutuallyExclusive: ["perk_a"]);

    private static (PerkTreeComponent perks, StatsComponent stats, LevelComponent level) Create(
        params PerkDefinition[] perks)
    {
        var stats = new StatsComponent(); // all 5
        var level = new LevelComponent(stats);
        var tree = new PerkTreeComponent(perks, stats, level);
        return (tree, stats, level);
    }

    [Fact]
    public void CanUnlock_MeetsConditions()
    {
        var (perks, _, level) = Create(IronFist1);
        level.GainXP(400); // level 2, 1 perk point
        Assert.True(perks.CanUnlock("iron_fist_1"));
    }

    [Fact]
    public void CanUnlock_LevelTooLow()
    {
        var (perks, _, level) = Create(IronFist1);
        level.PerkPointsAvailable = 1; // give a perk point but stay level 1
        Assert.False(perks.CanUnlock("iron_fist_1"));
    }

    [Fact]
    public void CanUnlock_StatTooLow()
    {
        var highReq = new PerkDefinition(
            "high_str", "High Str", "Needs Str 8",
            new PerkCondition(2, Stat.Strength, 8),
            ["stat:Strength:+1"]);
        var (perks, _, level) = Create(highReq);
        level.GainXP(400); // level 2
        Assert.False(perks.CanUnlock("high_str"));
    }

    [Fact]
    public void CanUnlock_NoPerkPoints()
    {
        var (perks, _, _) = Create(IronFist1);
        // Level 1, 0 perk points
        Assert.False(perks.CanUnlock("iron_fist_1"));
    }

    [Fact]
    public void CanUnlock_AlreadyUnlocked()
    {
        var (perks, _, level) = Create(IronFist1);
        level.GainXP(400);
        perks.Unlock("iron_fist_1");

        level.PerkPointsAvailable = 1; // give another point
        Assert.False(perks.CanUnlock("iron_fist_1"));
    }

    [Fact]
    public void CanUnlock_RequiredPerkMissing()
    {
        var (perks, _, level) = Create(IronFist1, IronFist2);
        // Level up to 7 with enough perk points
        level.GainXP(100_000);
        Assert.False(perks.CanUnlock("iron_fist_2"));
    }

    [Fact]
    public void CanUnlock_MutuallyExclusive()
    {
        var (perks, _, level) = Create(PerkA, PerkB);
        level.GainXP(400);
        perks.Unlock("perk_a");

        level.PerkPointsAvailable = 1;
        Assert.False(perks.CanUnlock("perk_b"));
    }

    [Fact]
    public void Unlock_DecrementsPerkPoints()
    {
        var (perks, _, level) = Create(IronFist1);
        level.GainXP(400); // level 2, 1 perk point
        Assert.Equal(1, level.PerkPointsAvailable);

        perks.Unlock("iron_fist_1");
        Assert.Equal(0, level.PerkPointsAvailable);
        Assert.Contains("iron_fist_1", perks.UnlockedPerks);
    }
}
