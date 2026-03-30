namespace Oravey2.Core.World;

public sealed class WorldStateService
{
    private readonly Dictionary<string, bool> _flags = new();

    public IReadOnlyDictionary<string, bool> Flags => _flags;

    public void SetFlag(string name, bool value) => _flags[name] = value;

    public bool GetFlag(string name)
        => _flags.TryGetValue(name, out var value) && value;
}
