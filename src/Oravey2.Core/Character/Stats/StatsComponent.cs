namespace Oravey2.Core.Character.Stats;

public class StatsComponent
{
    private readonly Dictionary<Stat, int> _baseStats = new();
    private readonly List<StatModifier> _modifiers = [];

    public IReadOnlyDictionary<Stat, int> BaseStats => _baseStats;
    public IReadOnlyList<StatModifier> Modifiers => _modifiers;

    public StatsComponent(Dictionary<Stat, int>? initial = null)
    {
        foreach (var stat in Enum.GetValues<Stat>())
            _baseStats[stat] = 5;

        if (initial != null)
        {
            foreach (var (stat, value) in initial)
                _baseStats[stat] = Math.Clamp(value, 1, 10);
        }
    }

    public int GetBase(Stat stat) => _baseStats[stat];

    public void SetBase(Stat stat, int value)
        => _baseStats[stat] = Math.Clamp(value, 1, 10);

    public int GetEffective(Stat stat)
    {
        var total = _baseStats[stat];
        foreach (var mod in _modifiers)
        {
            if (mod.Stat == stat) total += mod.Amount;
        }
        return Math.Clamp(total, 1, 99);
    }

    public void AddModifier(StatModifier mod) => _modifiers.Add(mod);

    public void RemoveModifier(StatModifier mod) => _modifiers.Remove(mod);

    public static bool IsValidAllocation(Dictionary<Stat, int> stats)
    {
        if (stats.Count != 7) return false;
        var total = 0;
        foreach (var (_, value) in stats)
        {
            if (value < 1 || value > 10) return false;
            total += value;
        }
        return total == 28;
    }
}
