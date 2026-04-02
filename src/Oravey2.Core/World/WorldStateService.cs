namespace Oravey2.Core.World;

public sealed class WorldStateService
{
    private readonly Dictionary<string, bool> _flags = new();
    private readonly Dictionary<string, int> _counters = new();

    public IReadOnlyDictionary<string, bool> Flags => _flags;
    public IReadOnlyDictionary<string, int> Counters => _counters;

    public void SetFlag(string name, bool value) => _flags[name] = value;

    public bool GetFlag(string name)
        => _flags.TryGetValue(name, out var value) && value;

    public void SetCounter(string name, int value) => _counters[name] = value;

    public int GetCounter(string name)
        => _counters.TryGetValue(name, out var value) ? value : 0;

    public void IncrementCounter(string name)
        => _counters[name] = GetCounter(name) + 1;

    public void RestoreFromSave(Dictionary<string, bool> flags, Dictionary<string, int> counters)
    {
        _flags.Clear();
        _counters.Clear();
        foreach (var (k, v) in flags)
            _flags[k] = v;
        foreach (var (k, v) in counters)
            _counters[k] = v;
    }
}
