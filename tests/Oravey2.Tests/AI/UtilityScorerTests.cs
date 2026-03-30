using Oravey2.Core.AI;
using Oravey2.Core.AI.Utility;

namespace Oravey2.Tests.AI;

public class UtilityScorerTests
{
    private readonly UtilityScorer _scorer = new();

    [Fact]
    public void ScoreBest_EmptyActions_ReturnsNone()
    {
        var (name, score) = _scorer.ScoreBest(new AIBlackboard(), []);
        Assert.Equal("None", name);
        Assert.Equal(0f, score);
    }

    [Fact]
    public void ScoreBest_SingleAction_ReturnsThat()
    {
        var action = new AIActionDefinition("OnlyAction",
        [
            new("always", _ => 1f, 1.0f)
        ]);
        var (name, _) = _scorer.ScoreBest(new AIBlackboard(), [action]);
        Assert.Equal("OnlyAction", name);
    }

    [Fact]
    public void ScoreBest_HighestScoreWins()
    {
        var low = new AIActionDefinition("Low", [new("c", _ => 0.1f, 1.0f)]);
        var high = new AIActionDefinition("High", [new("c", _ => 0.9f, 1.0f)]);

        var (name, _) = _scorer.ScoreBest(new AIBlackboard(), [low, high]);
        Assert.Equal("High", name);
    }

    [Fact]
    public void ScoreAction_AllWeightsApplied()
    {
        var action = new AIActionDefinition("Test",
        [
            new("a", _ => 1.0f, 0.3f),  // 1.0 × 0.3 = 0.3
            new("b", _ => 0.5f, 0.7f),  // 0.5 × 0.7 = 0.35
        ]);
        var score = _scorer.ScoreAction(new AIBlackboard(), action);
        Assert.Equal(0.65f, score, 0.001f);
    }

    [Fact]
    public void ScoreAction_EvaluatorsClampedTo01()
    {
        var action = new AIActionDefinition("Test",
        [
            new("over", _ => 2.0f, 1.0f) // clamped to 1.0 × 1.0 = 1.0
        ]);
        var score = _scorer.ScoreAction(new AIBlackboard(), action);
        Assert.Equal(1.0f, score, 0.001f);
    }

    [Fact]
    public void ScoreAction_NegativeEvaluator_ClampedToZero()
    {
        var action = new AIActionDefinition("Test",
        [
            new("neg", _ => -1f, 1.0f)
        ]);
        var score = _scorer.ScoreAction(new AIBlackboard(), action);
        Assert.Equal(0f, score, 0.001f);
    }

    [Fact]
    public void ScoreBest_TieBreaker_FirstWins()
    {
        var a = new AIActionDefinition("First", [new("c", _ => 0.5f, 1.0f)]);
        var b = new AIActionDefinition("Second", [new("c", _ => 0.5f, 1.0f)]);

        var (name, _) = _scorer.ScoreBest(new AIBlackboard(), [a, b]);
        Assert.Equal("First", name);
    }
}
