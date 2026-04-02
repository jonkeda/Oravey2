using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class WorldStateServiceTests
{
    [Fact]
    public void SetFlag_GetFlag_Roundtrip()
    {
        var svc = new WorldStateService();
        svc.SetFlag("x", true);
        Assert.True(svc.GetFlag("x"));
    }

    [Fact]
    public void GetFlag_Unknown_ReturnsFalse()
    {
        var svc = new WorldStateService();
        Assert.False(svc.GetFlag("never_set"));
    }

    [Fact]
    public void SetFlag_Overwrite()
    {
        var svc = new WorldStateService();
        svc.SetFlag("x", true);
        svc.SetFlag("x", false);
        Assert.False(svc.GetFlag("x"));
    }

    [Fact]
    public void SetFlag_FalseExplicitly()
    {
        var svc = new WorldStateService();
        svc.SetFlag("x", false);
        Assert.False(svc.GetFlag("x"));
    }

    [Fact]
    public void Flags_ReturnsAllEntries()
    {
        var svc = new WorldStateService();
        svc.SetFlag("a", true);
        svc.SetFlag("b", false);
        svc.SetFlag("c", true);
        Assert.Equal(3, svc.Flags.Count);
    }

    [Fact]
    public void GetFlag_EmptyStore_ReturnsFalse()
    {
        var svc = new WorldStateService();
        Assert.False(svc.GetFlag("anything"));
    }

    [Fact]
    public void GetCounter_Unknown_ReturnsZero()
    {
        var svc = new WorldStateService();
        Assert.Equal(0, svc.GetCounter("never_set"));
    }

    [Fact]
    public void SetCounter_GetCounter_Roundtrip()
    {
        var svc = new WorldStateService();
        svc.SetCounter("kills", 5);
        Assert.Equal(5, svc.GetCounter("kills"));
    }

    [Fact]
    public void IncrementCounter_FromZero()
    {
        var svc = new WorldStateService();
        svc.IncrementCounter("kills");
        Assert.Equal(1, svc.GetCounter("kills"));
    }

    [Fact]
    public void IncrementCounter_Accumulates()
    {
        var svc = new WorldStateService();
        svc.IncrementCounter("kills");
        svc.IncrementCounter("kills");
        svc.IncrementCounter("kills");
        Assert.Equal(3, svc.GetCounter("kills"));
    }

    [Fact]
    public void Counters_ReturnsAllEntries()
    {
        var svc = new WorldStateService();
        svc.SetCounter("a", 1);
        svc.SetCounter("b", 2);
        Assert.Equal(2, svc.Counters.Count);
    }
}
