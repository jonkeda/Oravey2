using Oravey2.Core.Framework.Events;
using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class FastTravelServiceTests
{
    private static (FastTravelService svc, EventBus bus) Setup()
    {
        var bus = new EventBus();
        return (new FastTravelService(bus), bus);
    }

    private static DiscoveredLocation Loc(string id, int cx = 0, int cy = 0)
        => new(id, id, cx, cy);

    [Fact]
    public void Discover_NewLocation_ReturnsTrue()
    {
        var (svc, _) = Setup();
        Assert.True(svc.Discover(Loc("haven")));
        Assert.Single(svc.Locations);
    }

    [Fact]
    public void Discover_Duplicate_ReturnsFalse()
    {
        var (svc, _) = Setup();
        svc.Discover(Loc("haven"));
        Assert.False(svc.Discover(Loc("haven")));
    }

    [Fact]
    public void Discover_PublishesEvent()
    {
        var (svc, bus) = Setup();
        LocationDiscoveredEvent? received = null;
        bus.Subscribe<LocationDiscoveredEvent>(e => received = e);

        svc.Discover(Loc("haven"));
        Assert.NotNull(received);
        Assert.Equal("haven", received.Value.LocationId);
    }

    [Fact]
    public void IsDiscovered_Known_True()
    {
        var (svc, _) = Setup();
        svc.Discover(Loc("haven"));
        Assert.True(svc.IsDiscovered("haven"));
    }

    [Fact]
    public void IsDiscovered_Unknown_False()
    {
        var (svc, _) = Setup();
        Assert.False(svc.IsDiscovered("nope"));
    }

    [Fact]
    public void GetLocation_Found()
    {
        var (svc, _) = Setup();
        var loc = Loc("haven", 4, 5);
        svc.Discover(loc);
        Assert.Equal(loc, svc.GetLocation("haven"));
    }

    [Fact]
    public void GetLocation_NotFound_Null()
    {
        var (svc, _) = Setup();
        Assert.Null(svc.GetLocation("nope"));
    }

    [Fact]
    public void CanTravel_BothDiscovered_True()
    {
        var (svc, _) = Setup();
        svc.Discover(Loc("a"));
        svc.Discover(Loc("b", 5, 5));
        Assert.True(svc.CanTravel("a", "b"));
    }

    [Fact]
    public void CanTravel_SameLocation_False()
    {
        var (svc, _) = Setup();
        svc.Discover(Loc("a"));
        Assert.False(svc.CanTravel("a", "a"));
    }

    [Fact]
    public void CanTravel_UndiscoveredDest_False()
    {
        var (svc, _) = Setup();
        svc.Discover(Loc("a"));
        Assert.False(svc.CanTravel("a", "b"));
    }

    [Fact]
    public void GetTravelTime_ManhattanDistance()
    {
        var (svc, _) = Setup();
        svc.Discover(Loc("a", 0, 0));
        svc.Discover(Loc("b", 3, 4));
        // Manhattan = |3-0| + |4-0| = 7, time = 7/10 = 0.7
        Assert.Equal(0.7f, svc.GetTravelTime("a", "b"), 0.001f);
    }

    [Fact]
    public void Travel_Success_PublishesEvent()
    {
        var (svc, bus) = Setup();
        svc.Discover(Loc("a", 0, 0));
        svc.Discover(Loc("b", 3, 4));

        FastTravelEvent? received = null;
        bus.Subscribe<FastTravelEvent>(e => received = e);

        var result = svc.Travel("a", "b");
        Assert.NotNull(result);
        Assert.Equal("b", result.Value.destination.Id);
        Assert.Equal(0.7f, result.Value.hoursCost, 0.001f);
        Assert.NotNull(received);
        Assert.Equal(0.7f, received.Value.InGameHoursCost, 0.001f);
    }

    [Fact]
    public void Travel_CannotTravel_ReturnsNull()
    {
        var (svc, _) = Setup();
        svc.Discover(Loc("a"));
        Assert.Null(svc.Travel("a", "b"));
    }
}
