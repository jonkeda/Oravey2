using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;

namespace Oravey2.Tests.Character;

public class SkillsComponentTests
{
    private static StatsComponent DefaultStats() => new();

    [Fact]
    public void StartingValues_DerivedFromStats()
    {
        var stats = DefaultStats(); // all 5
        var skills = new SkillsComponent(stats);

        // 10 + (5 × 2) = 20
        foreach (var skill in Enum.GetValues<SkillType>())
            Assert.Equal(20, skills.GetBase(skill));
    }

    [Fact]
    public void AllocatePoints_Increases()
    {
        var skills = new SkillsComponent(DefaultStats());
        skills.AllocatePoints(SkillType.Firearms, 10);

        Assert.Equal(30, skills.GetBase(SkillType.Firearms));
    }

    [Fact]
    public void AllocatePoints_ClampsAt100()
    {
        var skills = new SkillsComponent(DefaultStats());
        skills.AllocatePoints(SkillType.Firearms, 200);

        Assert.Equal(100, skills.GetBase(SkillType.Firearms));
    }

    [Fact]
    public void GetEffective_IncludesStatBonus()
    {
        var stats = DefaultStats();
        var skills = new SkillsComponent(stats);

        // Add a Strength modifier → Melee (linked to Str) should increase
        stats.AddModifier(new StatModifier(Stat.Strength, 3, "test"));
        // Effective = base(20) + (effective_stat(8) - base_stat(5)) × 2 = 20 + 6 = 26
        Assert.Equal(26, skills.GetEffective(SkillType.Melee));
    }

    [Fact]
    public void AddXP_BelowThreshold_NoLevelUp()
    {
        var skills = new SkillsComponent(DefaultStats());
        // Skill at 20, threshold = 20 × 5 = 100
        var leveled = skills.AddXP(SkillType.Firearms, 1);

        Assert.False(leveled);
        Assert.Equal(20, skills.GetBase(SkillType.Firearms));
    }

    [Fact]
    public void AddXP_MeetsThreshold_LevelsUp()
    {
        var skills = new SkillsComponent(DefaultStats());
        // Skill at 20, threshold = 100
        var leveled = skills.AddXP(SkillType.Firearms, 100);

        Assert.True(leveled);
        Assert.Equal(21, skills.GetBase(SkillType.Firearms));
    }
}
