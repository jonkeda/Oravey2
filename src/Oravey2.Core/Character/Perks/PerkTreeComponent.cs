using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;

namespace Oravey2.Core.Character.Perks;

public class PerkTreeComponent
{
    private readonly List<PerkDefinition> _allPerks;
    private readonly HashSet<string> _unlocked = [];
    private readonly StatsComponent _stats;
    private readonly LevelComponent _level;

    public IReadOnlyList<PerkDefinition> AllPerks => _allPerks;
    public IReadOnlySet<string> UnlockedPerks => _unlocked;

    public PerkTreeComponent(
        IReadOnlyList<PerkDefinition> perks,
        StatsComponent stats,
        LevelComponent level)
    {
        _allPerks = [.. perks];
        _stats = stats;
        _level = level;
    }

    public bool CanUnlock(string perkId)
    {
        var perk = _allPerks.FirstOrDefault(p => p.Id == perkId);
        if (perk == null) return false;
        if (_unlocked.Contains(perkId)) return false;
        if (_level.PerkPointsAvailable <= 0) return false;
        if (_level.Level < perk.Condition.RequiredLevel) return false;

        if (perk.Condition.RequiredStat is { } stat &&
            perk.Condition.StatThreshold is { } threshold)
        {
            if (_stats.GetEffective(stat) < threshold) return false;
        }

        if (perk.Condition.RequiredPerk is { } reqPerk)
        {
            if (!_unlocked.Contains(reqPerk)) return false;
        }

        if (perk.MutuallyExclusive != null)
        {
            foreach (var excl in perk.MutuallyExclusive)
            {
                if (_unlocked.Contains(excl)) return false;
            }
        }

        return true;
    }

    public bool Unlock(string perkId)
    {
        if (!CanUnlock(perkId)) return false;
        _unlocked.Add(perkId);
        _level.PerkPointsAvailable--;
        return true;
    }

    internal void RestoreFromSave(IEnumerable<string> perkIds)
    {
        _unlocked.Clear();
        foreach (var id in perkIds)
            _unlocked.Add(id);
    }
}
