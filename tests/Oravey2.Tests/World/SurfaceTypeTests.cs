using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class SurfaceTypeTests
{
    [Fact]
    public void SurfaceType_Has8Values()
    {
        var values = Enum.GetValues<SurfaceType>();
        Assert.Equal(8, values.Length);
    }

    [Theory]
    [InlineData(SurfaceType.Dirt, 0)]
    [InlineData(SurfaceType.Asphalt, 1)]
    [InlineData(SurfaceType.Concrete, 2)]
    [InlineData(SurfaceType.Grass, 3)]
    [InlineData(SurfaceType.Sand, 4)]
    [InlineData(SurfaceType.Mud, 5)]
    [InlineData(SurfaceType.Rock, 6)]
    [InlineData(SurfaceType.Metal, 7)]
    public void SurfaceType_ByteValues_Sequential(SurfaceType surface, byte expected)
    {
        Assert.Equal(expected, (byte)surface);
    }
}
