using Oravey2.Core.Survival;

namespace Oravey2.Tests.Survival;

public class SurvivalComponentTests
{
    [Fact]
    public void Default_EnabledAndZero()
    {
        var comp = new SurvivalComponent();
        Assert.True(comp.Enabled);
        Assert.Equal(0f, comp.Hunger);
        Assert.Equal(0f, comp.Thirst);
        Assert.Equal(0f, comp.Fatigue);
    }

    [Fact]
    public void Clamp_CapsAt100()
    {
        var comp = new SurvivalComponent { Hunger = 150, Thirst = 200, Fatigue = 300 };
        comp.Clamp();
        Assert.Equal(100f, comp.Hunger);
        Assert.Equal(100f, comp.Thirst);
        Assert.Equal(100f, comp.Fatigue);
    }

    [Fact]
    public void Clamp_FloorsAtZero()
    {
        var comp = new SurvivalComponent { Hunger = -10, Thirst = -5, Fatigue = -1 };
        comp.Clamp();
        Assert.Equal(0f, comp.Hunger);
        Assert.Equal(0f, comp.Thirst);
        Assert.Equal(0f, comp.Fatigue);
    }

    [Fact]
    public void GetThreshold_025_Satisfied()
    {
        Assert.Equal(SurvivalThreshold.Satisfied, SurvivalComponent.GetThreshold(25f));
    }

    [Fact]
    public void GetThreshold_2650_Normal()
    {
        Assert.Equal(SurvivalThreshold.Normal, SurvivalComponent.GetThreshold(50f));
    }

    [Fact]
    public void GetThreshold_5175_Deprived()
    {
        Assert.Equal(SurvivalThreshold.Deprived, SurvivalComponent.GetThreshold(75f));
    }

    [Fact]
    public void GetThreshold_76100_Critical()
    {
        Assert.Equal(SurvivalThreshold.Critical, SurvivalComponent.GetThreshold(76f));
    }

    [Fact]
    public void GetThreshold_Zero_Satisfied()
    {
        Assert.Equal(SurvivalThreshold.Satisfied, SurvivalComponent.GetThreshold(0f));
    }

    [Fact]
    public void GetThreshold_100_Critical()
    {
        Assert.Equal(SurvivalThreshold.Critical, SurvivalComponent.GetThreshold(100f));
    }
}
