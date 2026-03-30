using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;

namespace Oravey2.Tests.Character;

public class LevelComponentTests
{
    private static (StatsComponent stats, LevelComponent level, EventBus bus) Create()
    {
        var stats = new StatsComponent(); // Int=5
        var bus = new EventBus();
        var level = new LevelComponent(stats, bus);
        return (stats, level, bus);
    }

    [Fact]
    public void StartsAtLevel1()
    {
        var (_, level, _) = Create();
        Assert.Equal(1, level.Level);
        Assert.Equal(0, level.CurrentXP);
    }

    [Fact]
    public void GainXP_NoLevelUp()
    {
        var (_, level, _) = Create();
        // XP to level 2 = 100 × 2² = 400
        level.GainXP(100);

        Assert.Equal(1, level.Level);
        Assert.Equal(100, level.CurrentXP);
    }

    [Fact]
    public void GainXP_LevelUp()
    {
        var (_, level, _) = Create();
        level.GainXP(400); // exactly XP to level 2

        Assert.Equal(2, level.Level);
        Assert.Equal(1, level.StatPointsAvailable);
        // SkillPoints = 5 + 5/2 = 7
        Assert.Equal(7, level.SkillPointsAvailable);
    }

    [Fact]
    public void GainXP_MultipleLevels()
    {
        var (_, level, _) = Create();
        // Level 2 = 400, Level 3 = 900 → cumulative = 1300
        level.GainXP(1300);

        Assert.Equal(3, level.Level);
        Assert.Equal(2, level.StatPointsAvailable);
    }

    [Fact]
    public void GainXP_PerkPointEvery2Levels()
    {
        var (_, level, _) = Create();
        // Level up to 4: need 400 + 900 + 1600 = 2900
        level.GainXP(2900);

        Assert.Equal(4, level.Level);
        // Perks at level 2 and 4
        Assert.Equal(2, level.PerkPointsAvailable);
    }

    [Fact]
    public void GainXP_CapsAtMaxLevel()
    {
        var (_, level, _) = Create();
        level.GainXP(10_000_000); // huge XP

        Assert.Equal(LevelComponent.MaxLevel, level.Level);
    }

    [Fact]
    public void SpendStatPoint_IncreasesBase()
    {
        var (stats, level, _) = Create();
        level.GainXP(400); // level 2 → 1 stat point
        var result = level.SpendStatPoint(Stat.Strength);

        Assert.True(result);
        Assert.Equal(6, stats.GetBase(Stat.Strength));
        Assert.Equal(0, level.StatPointsAvailable);
    }

    [Fact]
    public void SpendStatPoint_FailsWhenNone()
    {
        var (_, level, _) = Create();
        Assert.False(level.SpendStatPoint(Stat.Strength));
    }

    [Fact]
    public void SpendStatPoint_FailsAtMax10()
    {
        var (stats, level, _) = Create();
        stats.SetBase(Stat.Strength, 10);
        level.GainXP(400);

        Assert.False(level.SpendStatPoint(Stat.Strength));
    }

    [Fact]
    public void GainXP_PublishesEvents()
    {
        var (_, level, bus) = Create();
        var xpEvents = new List<XPGainedEvent>();
        var levelEvents = new List<LevelUpEvent>();
        bus.Subscribe<XPGainedEvent>(e => xpEvents.Add(e));
        bus.Subscribe<LevelUpEvent>(e => levelEvents.Add(e));

        level.GainXP(400);

        Assert.Single(xpEvents);
        Assert.Equal(400, xpEvents[0].Amount);
        Assert.Single(levelEvents);
        Assert.Equal(1, levelEvents[0].OldLevel);
        Assert.Equal(2, levelEvents[0].NewLevel);
    }
}
