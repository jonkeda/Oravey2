using Oravey2.Core.Framework.Events;
using Oravey2.Core.UI.ViewModels;
using Oravey2.Core.World;

namespace Oravey2.Tests.UI;

public class MapViewModelTests
{
    private static (FastTravelService ft, DayNightCycleProcessor dn) Setup()
    {
        var bus = new EventBus();
        return (new FastTravelService(bus), new DayNightCycleProcessor(bus, startHour: 14f));
    }

    [Fact]
    public void Create_MapsLocations()
    {
        var (ft, dn) = Setup();
        ft.Discover(new DiscoveredLocation("a", "A", 0, 0));
        ft.Discover(new DiscoveredLocation("b", "B", 3, 4));
        var vm = MapViewModel.Create(ft, dn, "a");
        Assert.Equal(2, vm.Locations.Count);
    }

    [Fact]
    public void Create_CanTravelTo_BothDiscovered()
    {
        var (ft, dn) = Setup();
        ft.Discover(new DiscoveredLocation("a", "A", 0, 0));
        ft.Discover(new DiscoveredLocation("b", "B", 3, 4));
        var vm = MapViewModel.Create(ft, dn, "a");
        var locB = vm.Locations.First(l => l.Id == "b");
        Assert.True(locB.CanTravelTo);
    }

    [Fact]
    public void Create_CanTravelTo_Self_False()
    {
        var (ft, dn) = Setup();
        ft.Discover(new DiscoveredLocation("a", "A", 0, 0));
        var vm = MapViewModel.Create(ft, dn, "a");
        var locA = vm.Locations.First(l => l.Id == "a");
        Assert.False(locA.CanTravelTo);
    }

    [Fact]
    public void Create_NullCurrentLocation_AllFalse()
    {
        var (ft, dn) = Setup();
        ft.Discover(new DiscoveredLocation("a", "A", 0, 0));
        var vm = MapViewModel.Create(ft, dn, null);
        Assert.All(vm.Locations, l => Assert.False(l.CanTravelTo));
    }

    [Fact]
    public void Create_MapsTime()
    {
        var (ft, dn) = Setup();
        var vm = MapViewModel.Create(ft, dn, null);
        Assert.Equal(14f, vm.InGameHour);
        Assert.Equal(DayPhase.Day, vm.Phase);
    }

    [Fact]
    public void Create_MapsCurrentLocationId()
    {
        var (ft, dn) = Setup();
        ft.Discover(new DiscoveredLocation("haven", "Haven", 4, 4));
        var vm = MapViewModel.Create(ft, dn, "haven");
        Assert.Equal("haven", vm.CurrentLocationId);
    }
}
