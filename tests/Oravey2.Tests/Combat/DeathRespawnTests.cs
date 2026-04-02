using Oravey2.Core.Combat;

namespace Oravey2.Tests.Combat;

public class DeathRespawnTests
{
    [Fact]
    public void DeathRespawnScript_InitialState_IsIdle()
    {
        var script = new DeathRespawnScript();

        Assert.Equal(DeathRespawnScript.RespawnState.Idle, script.CurrentRespawnState);
        Assert.False(script.IsDead);
        Assert.Equal(0f, script.RespawnTimer);
        Assert.Equal(0, script.CapsLost);
    }

    [Fact]
    public void DeathRespawnScript_DefaultProperties()
    {
        var script = new DeathRespawnScript();

        Assert.Equal(2.0f, script.ShowDeathDuration);
        Assert.Equal(3.0f, script.RespawnDelay);
    }

    [Fact]
    public void DeathRespawnScript_CustomTimings_Accepted()
    {
        var script = new DeathRespawnScript
        {
            ShowDeathDuration = 1.5f,
            RespawnDelay = 4.0f,
        };

        Assert.Equal(1.5f, script.ShowDeathDuration);
        Assert.Equal(4.0f, script.RespawnDelay);
    }

    [Fact]
    public void RespawnState_HasExpectedValues()
    {
        Assert.Equal(0, (int)DeathRespawnScript.RespawnState.Idle);
        Assert.Equal(1, (int)DeathRespawnScript.RespawnState.ShowDeath);
        Assert.Equal(2, (int)DeathRespawnScript.RespawnState.Respawning);
        Assert.Equal(3, (int)DeathRespawnScript.RespawnState.Complete);
    }
}
