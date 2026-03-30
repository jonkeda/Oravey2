using Oravey2.Core.Framework.Events;
using Oravey2.Core.UI;

namespace Oravey2.Tests.UI;

public class ScreenManagerTests
{
    private static (ScreenManager mgr, EventBus bus) Setup()
    {
        var bus = new EventBus();
        return (new ScreenManager(bus), bus);
    }

    [Fact]
    public void Push_SetsActiveScreen()
    {
        var (mgr, _) = Setup();
        mgr.Push(ScreenId.Inventory);
        Assert.Equal(ScreenId.Inventory, mgr.ActiveScreen);
    }

    [Fact]
    public void Push_None_Ignored()
    {
        var (mgr, _) = Setup();
        mgr.Push(ScreenId.None);
        Assert.Equal(0, mgr.Count);
    }

    [Fact]
    public void Pop_ReturnsTopmost()
    {
        var (mgr, _) = Setup();
        mgr.Push(ScreenId.Inventory);
        var popped = mgr.Pop();
        Assert.Equal(ScreenId.Inventory, popped);
        Assert.Equal(ScreenId.None, mgr.ActiveScreen);
    }

    [Fact]
    public void Pop_EmptyStack_ReturnsNone()
    {
        var (mgr, _) = Setup();
        Assert.Equal(ScreenId.None, mgr.Pop());
    }

    [Fact]
    public void Replace_SwapsTopmost()
    {
        var (mgr, _) = Setup();
        mgr.Push(ScreenId.Inventory);
        mgr.Replace(ScreenId.Character);
        Assert.Equal(ScreenId.Character, mgr.ActiveScreen);
        Assert.Equal(1, mgr.Count);
    }

    [Fact]
    public void Replace_EmptyStack_Pushes()
    {
        var (mgr, _) = Setup();
        mgr.Replace(ScreenId.Map);
        Assert.Equal(ScreenId.Map, mgr.ActiveScreen);
        Assert.Equal(1, mgr.Count);
    }

    [Fact]
    public void Clear_PopsAll()
    {
        var (mgr, bus) = Setup();
        mgr.Push(ScreenId.Inventory);
        mgr.Push(ScreenId.Character);
        mgr.Push(ScreenId.Map);

        var popCount = 0;
        bus.Subscribe<ScreenPoppedEvent>(_ => popCount++);
        mgr.Clear();

        Assert.Equal(0, mgr.Count);
        Assert.Equal(3, popCount);
    }

    [Fact]
    public void Contains_Found()
    {
        var (mgr, _) = Setup();
        mgr.Push(ScreenId.Inventory);
        mgr.Push(ScreenId.Character);
        Assert.True(mgr.Contains(ScreenId.Inventory));
    }

    [Fact]
    public void Push_PublishesPushedEvent()
    {
        var (mgr, bus) = Setup();
        ScreenPushedEvent? received = null;
        bus.Subscribe<ScreenPushedEvent>(e => received = e);

        mgr.Push(ScreenId.Inventory);
        Assert.NotNull(received);
        Assert.Equal(ScreenId.Inventory, received.Value.Screen);
    }

    [Fact]
    public void Pop_PublishesPoppedEvent()
    {
        var (mgr, bus) = Setup();
        mgr.Push(ScreenId.Inventory);

        ScreenPoppedEvent? received = null;
        bus.Subscribe<ScreenPoppedEvent>(e => received = e);

        mgr.Pop();
        Assert.NotNull(received);
        Assert.Equal(ScreenId.Inventory, received.Value.Screen);
    }
}
