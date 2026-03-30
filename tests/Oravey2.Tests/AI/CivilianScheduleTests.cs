using Oravey2.Core.AI.Schedule;

namespace Oravey2.Tests.AI;

public class CivilianScheduleTests
{
    private static CivilianSchedule CreateTestSchedule() => new(
    [
        new ScheduleEntry(8f, 17f, 5, 5, "work"),
        new ScheduleEntry(17f, 20f, 3, 3, "trade"),
        new ScheduleEntry(22f, 6f, 1, 1, "sleep"),
    ]);

    [Fact]
    public void GetCurrentEntry_WithinRange_ReturnsEntry()
    {
        var schedule = CreateTestSchedule();
        var entry = schedule.GetCurrentEntry(9f);
        Assert.NotNull(entry);
        Assert.Equal("work", entry.ActivityTag);
    }

    [Fact]
    public void GetCurrentEntry_OutsideAllRanges_ReturnsNull()
    {
        var schedule = CreateTestSchedule();
        // 7.0 is between sleep (22-6) and work (8-17)
        var entry = schedule.GetCurrentEntry(7f);
        Assert.Null(entry);
    }

    [Fact]
    public void GetCurrentEntry_OvernightWrap()
    {
        var schedule = CreateTestSchedule();
        var entry = schedule.GetCurrentEntry(23f);
        Assert.NotNull(entry);
        Assert.Equal("sleep", entry.ActivityTag);
    }

    [Fact]
    public void GetCurrentEntry_OvernightWrap_EarlyMorning()
    {
        var schedule = CreateTestSchedule();
        var entry = schedule.GetCurrentEntry(3f);
        Assert.NotNull(entry);
        Assert.Equal("sleep", entry.ActivityTag);
    }

    [Fact]
    public void GetCurrentEntry_BoundaryStart()
    {
        var schedule = CreateTestSchedule();
        var entry = schedule.GetCurrentEntry(8f);
        Assert.NotNull(entry);
        Assert.Equal("work", entry.ActivityTag);
    }

    [Fact]
    public void GetCurrentEntry_BoundaryEnd()
    {
        var schedule = CreateTestSchedule();
        // EndHour is exclusive: 17.0 should NOT match work (8-17)
        // It should match trade (17-20)
        var entry = schedule.GetCurrentEntry(17f);
        Assert.NotNull(entry);
        Assert.Equal("trade", entry.ActivityTag);
    }

    [Fact]
    public void GetCurrentEntry_Hour24_WrapsToZero()
    {
        var schedule = CreateTestSchedule();
        // 24.0 % 24 = 0.0, which is in sleep (22-6)
        var entry = schedule.GetCurrentEntry(24f);
        Assert.NotNull(entry);
        Assert.Equal("sleep", entry.ActivityTag);
    }

    [Fact]
    public void EmptySchedule_ReturnsNull()
    {
        var schedule = new CivilianSchedule([]);
        Assert.Null(schedule.GetCurrentEntry(12f));
    }
}
