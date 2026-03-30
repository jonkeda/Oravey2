namespace Oravey2.Core.AI.Schedule;

public sealed class CivilianSchedule
{
    private readonly List<ScheduleEntry> _entries;

    public IReadOnlyList<ScheduleEntry> Entries => _entries;

    public CivilianSchedule(IEnumerable<ScheduleEntry> entries)
    {
        _entries = [.. entries.OrderBy(e => e.StartHour)];
    }

    public ScheduleEntry? GetCurrentEntry(float inGameHour)
    {
        var hour = inGameHour % 24f;
        foreach (var entry in _entries)
        {
            if (entry.StartHour <= entry.EndHour)
            {
                if (hour >= entry.StartHour && hour < entry.EndHour)
                    return entry;
            }
            else
            {
                if (hour >= entry.StartHour || hour < entry.EndHour)
                    return entry;
            }
        }
        return null;
    }
}
