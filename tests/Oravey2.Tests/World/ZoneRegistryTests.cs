using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class ZoneRegistryTests
{
    private static ZoneDefinition MakeZone(string id, bool fastTravel = true,
        int sx = 4, int sy = 4, int ex = 5, int ey = 5)
        => new(id, id, BiomeType.Settlement, 0f, 1, fastTravel, sx, sy, ex, ey);

    [Fact]
    public void Register_AddsZone()
    {
        var reg = new ZoneRegistry();
        reg.Register(MakeZone("haven"));
        Assert.Single(reg.Zones);
    }

    [Fact]
    public void Register_DuplicateId_Throws()
    {
        var reg = new ZoneRegistry();
        reg.Register(MakeZone("haven"));
        Assert.Throws<InvalidOperationException>(() => reg.Register(MakeZone("haven")));
    }

    [Fact]
    public void GetZoneForChunk_InRange_Found()
    {
        var reg = new ZoneRegistry();
        reg.Register(MakeZone("haven", sx: 4, sy: 4, ex: 5, ey: 5));
        Assert.Equal("haven", reg.GetZoneForChunk(4, 5)?.Id);
    }

    [Fact]
    public void GetZoneForChunk_OutOfRange_Null()
    {
        var reg = new ZoneRegistry();
        reg.Register(MakeZone("haven", sx: 4, sy: 4, ex: 5, ey: 5));
        Assert.Null(reg.GetZoneForChunk(0, 0));
    }

    [Fact]
    public void GetZone_ById_Found()
    {
        var reg = new ZoneRegistry();
        reg.Register(MakeZone("haven"));
        Assert.NotNull(reg.GetZone("haven"));
    }

    [Fact]
    public void GetZone_UnknownId_Null()
    {
        var reg = new ZoneRegistry();
        Assert.Null(reg.GetZone("nope"));
    }

    [Fact]
    public void GetFastTravelZones_FiltersCorrectly()
    {
        var reg = new ZoneRegistry();
        reg.Register(MakeZone("haven", fastTravel: true));
        reg.Register(MakeZone("bunker", fastTravel: false, sx: 0, sy: 0, ex: 1, ey: 1));
        Assert.Single(reg.GetFastTravelZones());
    }
}
