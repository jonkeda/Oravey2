using Oravey2.Core.Character.Stats;

namespace Oravey2.Core.Character.Skills;

public class SkillsComponent
{
    private readonly Dictionary<SkillType, int> _baseSkills = new();
    private readonly Dictionary<SkillType, int> _skillXP = new();
    private readonly StatsComponent _stats;

    public IReadOnlyDictionary<SkillType, int> BaseSkills => _baseSkills;

    private static readonly Dictionary<SkillType, Stat> SkillStatLinks = new()
    {
        { SkillType.Firearms, Stat.Perception },
        { SkillType.Melee, Stat.Strength },
        { SkillType.Survival, Stat.Endurance },
        { SkillType.Science, Stat.Intelligence },
        { SkillType.Speech, Stat.Charisma },
        { SkillType.Stealth, Stat.Agility },
        { SkillType.Mechanics, Stat.Intelligence },
    };

    public SkillsComponent(StatsComponent stats)
    {
        _stats = stats;
        foreach (var skill in Enum.GetValues<SkillType>())
        {
            var linked = SkillStatLinks[skill];
            _baseSkills[skill] = 10 + stats.GetBase(linked) * 2;
            _skillXP[skill] = 0;
        }
    }

    public int GetBase(SkillType skill) => _baseSkills[skill];

    public int GetEffective(SkillType skill)
    {
        var linked = SkillStatLinks[skill];
        var statBonus = (_stats.GetEffective(linked) - _stats.GetBase(linked)) * 2;
        return Math.Clamp(_baseSkills[skill] + statBonus, 0, 100);
    }

    public void AllocatePoints(SkillType skill, int points)
    {
        _baseSkills[skill] = Math.Clamp(_baseSkills[skill] + points, 0, 100);
    }

    internal void SetBase(SkillType skill, int value)
    {
        _baseSkills[skill] = Math.Clamp(value, 0, 100);
    }

    public bool AddXP(SkillType skill, int amount)
    {
        _skillXP[skill] += amount;
        var threshold = _baseSkills[skill] * 5;
        if (_skillXP[skill] >= threshold && _baseSkills[skill] < 100)
        {
            _baseSkills[skill]++;
            _skillXP[skill] -= threshold;
            return true;
        }
        return false;
    }

    public static Stat GetLinkedStat(SkillType skill) => SkillStatLinks[skill];
}
