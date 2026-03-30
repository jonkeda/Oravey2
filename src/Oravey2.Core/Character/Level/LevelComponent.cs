using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Character.Level;

public class LevelComponent
{
    private readonly StatsComponent _stats;
    private readonly IEventBus? _eventBus;

    public int Level { get; private set; } = 1;
    public int CurrentXP { get; private set; }
    public int XPToNextLevel => LevelFormulas.XPRequired(Level + 1);
    public int StatPointsAvailable { get; private set; }
    public int SkillPointsAvailable { get; private set; }
    public int PerkPointsAvailable { get; internal set; }

    public const int MaxLevel = 30;

    public LevelComponent(StatsComponent stats, IEventBus? eventBus = null)
    {
        _stats = stats;
        _eventBus = eventBus;
    }

    public void GainXP(int amount)
    {
        if (amount <= 0 || Level >= MaxLevel) return;

        CurrentXP += amount;
        _eventBus?.Publish(new XPGainedEvent(amount));

        while (CurrentXP >= XPToNextLevel && Level < MaxLevel)
        {
            CurrentXP -= XPToNextLevel;
            var oldLevel = Level;
            Level++;

            StatPointsAvailable += 1;
            SkillPointsAvailable += LevelFormulas.SkillPointsPerLevel(
                _stats.GetEffective(Stat.Intelligence));

            if (Level % 2 == 0)
                PerkPointsAvailable++;

            _eventBus?.Publish(new LevelUpEvent(oldLevel, Level));
        }
    }

    internal void SetFromSave(int level, int xp)
    {
        Level = Math.Clamp(level, 1, MaxLevel);
        CurrentXP = Math.Max(0, xp);
    }

    public bool SpendStatPoint(Stat stat)
    {
        if (StatPointsAvailable <= 0) return false;
        if (_stats.GetBase(stat) >= 10) return false;
        _stats.SetBase(stat, _stats.GetBase(stat) + 1);
        StatPointsAvailable--;
        return true;
    }

    public bool SpendSkillPoints(Skills.SkillType skill, int points, Skills.SkillsComponent skills)
    {
        if (points <= 0 || points > SkillPointsAvailable) return false;
        skills.AllocatePoints(skill, points);
        SkillPointsAvailable -= points;
        return true;
    }
}
