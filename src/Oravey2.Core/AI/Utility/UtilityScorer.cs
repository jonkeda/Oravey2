namespace Oravey2.Core.AI.Utility;

public sealed class UtilityScorer
{
    public (string ActionName, float Score) ScoreBest(
        AIBlackboard blackboard,
        IReadOnlyList<AIActionDefinition> actions)
    {
        if (actions.Count == 0)
            return ("None", 0f);

        string bestAction = actions[0].Name;
        float bestScore = float.MinValue;

        foreach (var action in actions)
        {
            var score = ScoreAction(blackboard, action);
            if (score > bestScore)
            {
                bestScore = score;
                bestAction = action.Name;
            }
        }

        return (bestAction, bestScore);
    }

    public float ScoreAction(AIBlackboard blackboard, AIActionDefinition action)
    {
        float total = 0f;
        foreach (var c in action.Considerations)
        {
            var raw = Math.Clamp(c.Evaluate(blackboard), 0f, 1f);
            total += raw * c.Weight;
        }
        return total;
    }
}
