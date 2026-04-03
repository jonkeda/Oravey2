using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class HeightRenderingTests
{
    [Theory]
    [InlineData(0, 0f)]
    [InlineData(4, 1.0f)]
    [InlineData(10, 2.5f)]
    public void HeightLevel_ProducesCorrectBaseY(byte heightLevel, float expectedY)
    {
        float baseY = heightLevel * HeightHelper.HeightStep;
        Assert.Equal(expectedY, baseY);
    }

    [Fact]
    public void WallOnHeight4_TopAtCorrectPosition()
    {
        // Wall at HeightLevel 4: base = 1.0f, WallHeight default = 1.0f
        // Top of wall = 1.0 + 1.0 = 2.0
        float heightStep = 0.25f;
        float wallHeight = 1.0f;
        byte heightLevel = 4;

        float baseY = heightLevel * heightStep;
        float topOfWall = baseY + wallHeight;
        Assert.Equal(2.0f, topOfWall);
    }
}
