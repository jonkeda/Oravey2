using Oravey2.Core.Combat;

namespace Oravey2.Tests.Combat;

public class DeathPenaltyTests
{
    [Fact]
    public void DeathPenalty_50Caps_Loses5()
    {
        Assert.Equal(5, DeathPenalty.CalculateCapsLoss(50));
    }

    [Fact]
    public void DeathPenalty_100Caps_Loses10()
    {
        Assert.Equal(10, DeathPenalty.CalculateCapsLoss(100));
    }

    [Fact]
    public void DeathPenalty_3Caps_LosesZero()
    {
        Assert.Equal(0, DeathPenalty.CalculateCapsLoss(3));
    }

    [Fact]
    public void DeathPenalty_0Caps_LosesZero()
    {
        Assert.Equal(0, DeathPenalty.CalculateCapsLoss(0));
    }

    [Fact]
    public void DeathPenalty_1000Caps_Loses100()
    {
        Assert.Equal(100, DeathPenalty.CalculateCapsLoss(1000));
    }
}
