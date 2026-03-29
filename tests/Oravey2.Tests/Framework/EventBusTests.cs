using Oravey2.Core.Framework.Events;
using Xunit;

namespace Oravey2.Tests.Framework;

public class EventBusTests
{
    [Fact]
    public void Subscribe_and_Publish_delivers_event()
    {
        var bus = new EventBus();
        TestEvent? received = null;

        bus.Subscribe<TestEvent>(e => received = e);
        bus.Publish(new TestEvent("hello"));

        Assert.NotNull(received);
        Assert.Equal("hello", received.Value.Message);
    }

    [Fact]
    public void Unsubscribe_prevents_delivery()
    {
        var bus = new EventBus();
        int callCount = 0;
        Action<TestEvent> handler = _ => callCount++;

        bus.Subscribe(handler);
        bus.Publish(new TestEvent("first"));
        Assert.Equal(1, callCount);

        bus.Unsubscribe(handler);
        bus.Publish(new TestEvent("second"));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Multiple_subscribers_all_receive_event()
    {
        var bus = new EventBus();
        var received = new List<string>();

        bus.Subscribe<TestEvent>(e => received.Add("A:" + e.Message));
        bus.Subscribe<TestEvent>(e => received.Add("B:" + e.Message));
        bus.Publish(new TestEvent("test"));

        Assert.Equal(2, received.Count);
        Assert.Contains("A:test", received);
        Assert.Contains("B:test", received);
    }

    [Fact]
    public void Publish_with_no_subscribers_does_not_throw()
    {
        var bus = new EventBus();
        var ex = Record.Exception(() => bus.Publish(new TestEvent("orphan")));
        Assert.Null(ex);
    }

    [Fact]
    public void Different_event_types_are_isolated()
    {
        var bus = new EventBus();
        int testCount = 0;
        int otherCount = 0;

        bus.Subscribe<TestEvent>(_ => testCount++);
        bus.Subscribe<OtherEvent>(_ => otherCount++);

        bus.Publish(new TestEvent("a"));

        Assert.Equal(1, testCount);
        Assert.Equal(0, otherCount);
    }

    private readonly record struct TestEvent(string Message) : IGameEvent;
    private readonly record struct OtherEvent(int Value) : IGameEvent;
}
