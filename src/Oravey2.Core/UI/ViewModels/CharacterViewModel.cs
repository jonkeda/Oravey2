using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;

namespace Oravey2.Core.UI.ViewModels;

/// <summary>
/// Snapshot of the character sheet: stats, skills, level info, available points.
/// </summary>
public sealed record CharacterViewModel(
    IReadOnlyDictionary<Stat, int> BaseStats,
    IReadOnlyDictionary<Stat, int> EffectiveStats,
    IReadOnlyDictionary<SkillType, int> EffectiveSkills,
    int Level,
    int CurrentXP,
    int XPToNextLevel,
    int StatPointsAvailable,
    int SkillPointsAvailable,
    int PerkPointsAvailable
)
{
    public static CharacterViewModel Create(
        StatsComponent stats,
        SkillsComponent skills,
        LevelComponent level)
    {
        var baseStats = new Dictionary<Stat, int>();
        var effectiveStats = new Dictionary<Stat, int>();
        foreach (Stat s in Enum.GetValues<Stat>())
        {
            baseStats[s] = stats.GetBase(s);
            effectiveStats[s] = stats.GetEffective(s);
        }

        var effectiveSkills = new Dictionary<SkillType, int>();
        foreach (SkillType sk in Enum.GetValues<SkillType>())
            effectiveSkills[sk] = skills.GetEffective(sk);

        return new CharacterViewModel(
            BaseStats: baseStats,
            EffectiveStats: effectiveStats,
            EffectiveSkills: effectiveSkills,
            Level: level.Level,
            CurrentXP: level.CurrentXP,
            XPToNextLevel: level.XPToNextLevel,
            StatPointsAvailable: level.StatPointsAvailable,
            SkillPointsAvailable: level.SkillPointsAvailable,
            PerkPointsAvailable: level.PerkPointsAvailable
        );
    }
}
