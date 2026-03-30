using Oravey2.Core.AI;
using Oravey2.Core.AI.Utility;

namespace Oravey2.Tests.AI;

public class CombatConsiderationsTests
{
    private readonly UtilityScorer _scorer = new();

    [Fact]
    public void Attack_HealthyWithTarget_HighScore()
    {
        var bb = new AIBlackboard
        {
            HasAmmo = true,
            CurrentTargetId = "enemy1",
            HealthPercent = 0.80f,
        };
        var score = _scorer.ScoreAction(bb, CombatConsiderations.Attack());
        Assert.Equal(1.0f, score, 0.001f); // 0.3 + 0.4 + 0.3 = 1.0
    }

    [Fact]
    public void Attack_NoTarget_LowScore()
    {
        var bb = new AIBlackboard
        {
            HasAmmo = true,
            CurrentTargetId = null,
            HealthPercent = 0.80f,
        };
        var score = _scorer.ScoreAction(bb, CombatConsiderations.Attack());
        Assert.Equal(0.6f, score, 0.001f); // 0.3 + 0 + 0.3 = 0.6
    }

    [Fact]
    public void Attack_LowHealth_LowScore()
    {
        var bb = new AIBlackboard
        {
            HasAmmo = true,
            CurrentTargetId = "enemy1",
            HealthPercent = 0.20f, // below 30%
        };
        var score = _scorer.ScoreAction(bb, CombatConsiderations.Attack());
        Assert.Equal(0.7f, score, 0.001f); // 0.3 + 0.4 + 0 = 0.7
    }

    [Fact]
    public void Flee_LowHealth_NoAmmo_Outnumbered_HighScore()
    {
        var bb = new AIBlackboard
        {
            HealthPercent = 0.10f,
            HasAmmo = false,
            EnemyCount = 3,
            AllyCount = 1,
        };
        var score = _scorer.ScoreAction(bb, CombatConsiderations.Flee());
        Assert.Equal(1.0f, score, 0.001f); // 0.5 + 0.3 + 0.2 = 1.0
    }

    [Fact]
    public void Flee_HealthyWithAmmo_ZeroScore()
    {
        var bb = new AIBlackboard
        {
            HealthPercent = 0.80f,
            HasAmmo = true,
            EnemyCount = 1,
            AllyCount = 3,
        };
        var score = _scorer.ScoreAction(bb, CombatConsiderations.Flee());
        Assert.Equal(0f, score, 0.001f);
    }

    [Fact]
    public void TakeCover_UnderFire_CoverNearby_HighScore()
    {
        var bb = new AIBlackboard
        {
            UnderFire = true,
            CoverNearby = true,
            HealthPercent = 0.30f, // below 50%
        };
        var score = _scorer.ScoreAction(bb, CombatConsiderations.TakeCover());
        Assert.Equal(1.0f, score, 0.001f); // 0.4 + 0.4 + 0.2 = 1.0
    }

    [Fact]
    public void Patrol_NoThreats_HighScore()
    {
        var bb = new AIBlackboard
        {
            ThreatLevel = 0f,
            CurrentTargetId = null,
        };
        var score = _scorer.ScoreAction(bb, CombatConsiderations.Patrol());
        Assert.Equal(1.0f, score, 0.001f); // 0.6 + 0.4 = 1.0
    }

    [Fact]
    public void Investigate_HeardNoise_LostTarget()
    {
        var bb = new AIBlackboard
        {
            ThreatLevel = 0.5f,
            CurrentTargetId = null, // heard noise but no target
        };
        var score = _scorer.ScoreAction(bb, CombatConsiderations.Investigate());
        Assert.Equal(0.5f, score, 0.001f); // 0.5 + 0 = 0.5
    }

    [Fact]
    public void AllCombatActions_Returns5()
    {
        Assert.Equal(5, CombatConsiderations.AllCombatActions().Length);
    }
}
