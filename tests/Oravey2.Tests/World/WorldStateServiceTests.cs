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
}
