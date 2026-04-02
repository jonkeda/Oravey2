using Oravey2.Core.Framework.Events;
using Oravey2.Core.Save;

namespace Oravey2.Tests.Save;

public class AutoSaveTrackerTests
{
    private readonly EventBus _bus = new();

    [Fact]
    public void DefaultEnabled()
    {
        var tracker = new AutoSaveTracker(_bus);
        Assert.True(tracker.Enabled);
    }

    [Fact]
    public void ShouldSaveFalseInitially()
    {
        var tracker = new AutoSaveTracker(_bus);
        Assert.False(tracker.ShouldSave);
    }

    [Fact]
    public void TickBelowIntervalNoTrigger()
    {
        var tracker = new AutoSaveTracker(_bus, intervalSeconds: 60f);
        tracker.Tick(30f);

        Assert.False(tracker.ShouldSave);
    }

    [Fact]
    public void TickPastIntervalTriggers()
    {
        var tracker = new AutoSaveTracker(_bus, intervalSeconds: 10f);
        tracker.Tick(11f);

        Assert.True(tracker.ShouldSave);
    }

    [Fact]
    public void AutoSaveTriggeredEventPublished()
    {
        var tracker = new AutoSaveTracker(_bus, intervalSeconds: 10f);
        AutoSaveTriggeredEvent? received = null;
        _bus.Subscribe<AutoSaveTriggeredEvent>(e => received = e);

        tracker.Tick(11f);

        Assert.NotNull(received);
    }

    [Fact]
    public void AcknowledgeResetsTimerAndFlag()
    {
        var tracker = new AutoSaveTracker(_bus, intervalSeconds: 10f);
        tracker.Tick(11f);
        Assert.True(tracker.ShouldSave);

        tracker.Acknowledge();

        Assert.False(tracker.ShouldSave);
        Assert.Equal(0f, tracker.Elapsed);
    }

    [Fact]
    public void TriggerNowSetsPending()
    {
        var tracker = new AutoSaveTracker(_bus);
        tracker.TriggerNow();

        Assert.True(tracker.ShouldSave);
    }

    [Fact]
    public void TriggerNowPublishesEvent()
    {
        var tracker = new AutoSaveTracker(_bus);
        AutoSaveTriggeredEvent? received = null;
        _bus.Subscribe<AutoSaveTriggeredEvent>(e => received = e);

        tracker.TriggerNow();

        Assert.NotNull(received);
    }

    [Fact]
    public void DisabledPreventsTick()
    {
        var tracker = new AutoSaveTracker(_bus, intervalSeconds: 10f);
        tracker.Enabled = false;
        tracker.Tick(20f);

        Assert.False(tracker.ShouldSave);
    }

    [Fact]
    public void DisabledPreventsTriggerNow()
    {
        var tracker = new AutoSaveTracker(_bus);
        tracker.Enabled = false;
        tracker.TriggerNow();

        Assert.False(tracker.ShouldSave);
    }

    [Fact]
    public void NegativeDeltaIgnored()
    {
        var tracker = new AutoSaveTracker(_bus);
        tracker.Tick(-5f);

        Assert.Equal(0f, tracker.Elapsed);
    }

    [Fact]
    public void MultipleTicksAccumulate()
    {
        var tracker = new AutoSaveTracker(_bus, intervalSeconds: 100f);
        tracker.Tick(30f);
        tracker.Tick(25f);

        Assert.Equal(55f, tracker.Elapsed, 1);
    }

    [Fact]
    public void PausedPreventsTick()
    {
        var tracker = new AutoSaveTracker(_bus, intervalSeconds: 10f);
        tracker.Paused = true;
        tracker.Tick(20f);

        Assert.False(tracker.ShouldSave);
        Assert.Equal(0f, tracker.Elapsed);
    }

    [Fact]
    public void PausedPreventsTriggerNow()
    {
        var tracker = new AutoSaveTracker(_bus);
        tracker.Paused = true;
        tracker.TriggerNow();

        Assert.False(tracker.ShouldSave);
    }
}
