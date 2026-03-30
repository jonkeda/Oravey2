using Oravey2.Core.Framework.Events;
using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class DayNightCycleProcessorTests
{
    [Fact]
    public void Constructor_DefaultStartHour8_DayPhase()
    {
        var proc = new DayNightCycleProcessor(new EventBus());
        Assert.Equal(DayPhase.Day, proc.CurrentPhase);
        Assert.Equal(8f, proc.InGameHour);
    }

    [Fact]
    public void GetPhase_5_Night()
    {
        Assert.Equal(DayPhase.Night, DayNightCycleProcessor.GetPhase(5f));
    }

    [Fact]
    public void GetPhase_6_Dawn()
    {
        Assert.Equal(DayPhase.Dawn, DayNightCycleProcessor.GetPhase(6f));
    }

    [Fact]
    public void GetPhase_7_Day()
    {
        Assert.Equal(DayPhase.Day, DayNightCycleProcessor.GetPhase(7f));
    }

    [Fact]
    public void GetPhase_20_Dusk()
    {
        Assert.Equal(DayPhase.Dusk, DayNightCycleProcessor.GetPhase(20f));
    }

    [Fact]
    public void GetPhase_21_Night()
    {
        Assert.Equal(DayPhase.Night, DayNightCycleProcessor.GetPhase(21f));
    }

    [Fact]
    public void Tick_AdvancesTime()
    {
        var proc = new DayNightCycleProcessor(new EventBus(), startHour: 8f);
        proc.Tick(120f); // 120 real seconds = 1 in-game hour
        Assert.Equal(9f, proc.InGameHour, 0.01f);
    }

    [Fact]
    public void Tick_PhaseChange_PublishesEvent()
    {
        var bus = new EventBus();
        var proc = new DayNightCycleProcessor(bus, startHour: 6.5f); // Dawn

        DayPhaseChangedEvent? received = null;
        bus.Subscribe<DayPhaseChangedEvent>(e => received = e);

        // Tick 0.5 hours = 60 real seconds → 6.5 + 0.5 = 7.0 = Day
        proc.Tick(60f);

        Assert.NotNull(received);
        Assert.Equal(DayPhase.Dawn, received.Value.OldPhase);
        Assert.Equal(DayPhase.Day, received.Value.NewPhase);
    }

    [Fact]
    public void Tick_NoPhaseChange_NoEvent()
    {
        var bus = new EventBus();
        var proc = new DayNightCycleProcessor(bus, startHour: 10f);

        DayPhaseChangedEvent? received = null;
        bus.Subscribe<DayPhaseChangedEvent>(e => received = e);

        proc.Tick(1f); // tiny tick, stays Day
        Assert.Null(received);
    }

    [Fact]
    public void Tick_WrapsAtMidnight()
    {
        var proc = new DayNightCycleProcessor(new EventBus(), startHour: 23.5f);
        // Tick 1.5 hours = 180 real seconds → 23.5 + 1.5 = 25.0 → wraps to 1.0
        proc.Tick(180f);
        Assert.Equal(1.0f, proc.InGameHour, 0.01f);
    }

    [Fact]
    public void AdvanceHours_SmallStep()
    {
        var proc = new DayNightCycleProcessor(new EventBus(), startHour: 8f);
        proc.AdvanceHours(2f);
        Assert.Equal(10f, proc.InGameHour, 0.1f);
    }

    [Fact]
    public void AdvanceHours_CrossesMultiplePhases()
    {
        var bus = new EventBus();
        var proc = new DayNightCycleProcessor(bus, startHour: 6f); // Dawn

        var phases = new List<DayPhase>();
        bus.Subscribe<DayPhaseChangedEvent>(e => phases.Add(e.NewPhase));

        proc.AdvanceHours(16f); // 6 + 16 = 22 → Night

        // Dawn→Day (at 7), Day→Dusk (at 20), Dusk→Night (at 21)
        Assert.Contains(DayPhase.Day, phases);
        Assert.Contains(DayPhase.Dusk, phases);
        Assert.Contains(DayPhase.Night, phases);
    }

    [Fact]
    public void SetTime_ChangesPhase()
    {
        var bus = new EventBus();
        var proc = new DayNightCycleProcessor(bus, startHour: 8f); // Day

        DayPhaseChangedEvent? received = null;
        bus.Subscribe<DayPhaseChangedEvent>(e => received = e);

        proc.SetTime(22f);
        Assert.Equal(DayPhase.Night, proc.CurrentPhase);
        Assert.NotNull(received);
    }

    [Fact]
    public void SetTime_SamePhase_NoEvent()
    {
        var bus = new EventBus();
        var proc = new DayNightCycleProcessor(bus, startHour: 10f); // Day

        DayPhaseChangedEvent? received = null;
        bus.Subscribe<DayPhaseChangedEvent>(e => received = e);

        proc.SetTime(12f); // still Day
        Assert.Null(received);
    }
}
