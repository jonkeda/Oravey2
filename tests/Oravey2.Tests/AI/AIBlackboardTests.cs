using Oravey2.Core.AI;

namespace Oravey2.Tests.AI;

public class AIBlackboardTests
{
    [Fact]
    public void Defaults_Idle_FullHealth()
    {
        var bb = new AIBlackboard();
        Assert.Equal(AIState.Idle, bb.CurrentState);
        Assert.Equal(1.0f, bb.HealthPercent);
        Assert.True(bb.HasAmmo);
        Assert.Null(bb.CurrentTargetId);
        Assert.Null(bb.LastKnownTargetPosition);
    }

    [Fact]
    public void SetCustom_GetCustom_Roundtrip()
    {
        var bb = new AIBlackboard();
        bb.SetCustom("damage_taken", 42);
        Assert.Equal(42, bb.GetCustom<int>("damage_taken"));
    }

    [Fact]
    public void GetCustom_Missing_ReturnsDefault()
    {
        var bb = new AIBlackboard();
        Assert.Equal(0, bb.GetCustom<int>("nonexistent"));
        Assert.Null(bb.GetCustom<string>("nonexistent"));
    }

    [Fact]
    public void GetCustom_WrongType_ReturnsDefault()
    {
        var bb = new AIBlackboard();
        bb.SetCustom("key", 42);
        Assert.Null(bb.GetCustom<string>("key"));
    }

    [Fact]
    public void ResetTransient_ClearsUnderFire()
    {
        var bb = new AIBlackboard { UnderFire = true };
        bb.ResetTransient();
        Assert.False(bb.UnderFire);
    }
}
