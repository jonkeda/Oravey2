namespace Oravey2.Core.AI.Schedule;

public sealed record ScheduleEntry(
    float StartHour,
    float EndHour,
    int WaypointX,
    int WaypointY,
    string? ActivityTag = null);
