using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class ZoneManagerTests
{
    [Fact]
    public void ZoneManager_InitialZone_IsNull()
    {
        var manager = new ZoneManager(null!);
        Assert.Null(manager.CurrentZoneId);
    }

    [Fact]
    public void ZoneManager_SetCurrentZone_TracksId()
    {
        var manager = new ZoneManager(null!);
        manager.SetCurrentZone("town");
        Assert.Equal("town", manager.CurrentZoneId);
    }

    [Fact]
    public void ZoneManager_SetZone_Twice_OverwritesPrevious()
    {
        var manager = new ZoneManager(null!);
        manager.SetCurrentZone("town");
        manager.SetCurrentZone("wasteland");
        Assert.Equal("wasteland", manager.CurrentZoneId);
    }

    [Fact]
    public void ZoneManager_CurrentZoneName_MapsCorrectly()
    {
        var manager = new ZoneManager(null!);

        manager.SetCurrentZone("town");
        Assert.Equal("Haven", manager.CurrentZoneName);

        manager.SetCurrentZone("wasteland");
        Assert.Equal("Wasteland", manager.CurrentZoneName);
    }

    [Fact]
    public void ZoneExitTrigger_DefaultRadius_Is1_5()
    {
        var script = new ZoneExitTriggerScript();
        Assert.Equal(1.5f, script.TriggerRadius);
    }
}
