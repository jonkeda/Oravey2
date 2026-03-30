using Oravey2.Core.Survival;

namespace Oravey2.Tests.Survival;

public class RadiationComponentTests
{
    [Fact]
    public void Default_Zero()
    {
        var rad = new RadiationComponent();
        Assert.Equal(0, rad.Level);
    }

    [Fact]
    public void Expose_IncreasesLevel()
    {
        var rad = new RadiationComponent();
        rad.Expose(50);
        Assert.Equal(50, rad.Level);
    }

    [Fact]
    public void Expose_CappedAt1000()
    {
        var rad = new RadiationComponent();
        rad.Expose(2000);
        Assert.Equal(1000, rad.Level);
    }

    [Fact]
    public void Reduce_DecreasesLevel()
    {
        var rad = new RadiationComponent { Level = 100 };
        rad.Reduce(30);
        Assert.Equal(70, rad.Level);
    }

    [Fact]
    public void Reduce_FloorsAtZero()
    {
        var rad = new RadiationComponent { Level = 20 };
        rad.Reduce(50);
        Assert.Equal(0, rad.Level);
    }
}
