using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class SurfaceTypeTests
{
    [Fact]
    public void SurfaceType_Has9Values()
    {
        var values = Enum.GetValues<SurfaceType>();
        Assert.Equal(9, values.Length);
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
    [InlineData(SurfaceType.Gravel, 8)]
    public void SurfaceType_ByteValues_AreCorrect(SurfaceType surface, byte expected)
    {
        Assert.Equal(expected, (byte)surface);
    }

    [Fact]
    public void SurfaceType_AllValues_AreUnique()
    {
        var values = Enum.GetValues<SurfaceType>();
        var byteValues = values.Select(v => (byte)v).ToArray();
        Assert.Equal(byteValues.Length, byteValues.Distinct().Count());
    }

    [Fact]
    public void SurfaceType_Gravel_Equals8()
    {
        Assert.Equal((byte)8, (byte)SurfaceType.Gravel);
    }
}
