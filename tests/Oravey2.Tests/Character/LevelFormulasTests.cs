using Oravey2.Core.Character.Level;

namespace Oravey2.Tests.Character;

public class LevelFormulasTests
{
    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 400)]
    [InlineData(10, 10_000)]
    [InlineData(30, 90_000)]
    public void XPRequired_ReturnsCorrectValue(int level, int expected)
    {
        Assert.Equal(expected, LevelFormulas.XPRequired(level));
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(5, 7)]
    [InlineData(10, 10)]
    public void SkillPointsPerLevel_ReturnsCorrectValue(int intelligence, int expected)
    {
        Assert.Equal(expected, LevelFormulas.SkillPointsPerLevel(intelligence));
    }

    [Theory]
    [InlineData(5, 1, 105)]
    [InlineData(10, 30, 300)]
    [InlineData(1, 1, 65)]
    public void MaxHP_ReturnsCorrectValue(int endurance, int level, int expected)
    {
        Assert.Equal(expected, LevelFormulas.MaxHP(endurance, level));
    }

    [Theory]
    [InlineData(5, 100f)]
    [InlineData(1, 60f)]
    [InlineData(10, 150f)]
    public void CarryWeight_ReturnsCorrectValue(int strength, float expected)
    {
        Assert.Equal(expected, LevelFormulas.CarryWeight(strength));
    }
}
