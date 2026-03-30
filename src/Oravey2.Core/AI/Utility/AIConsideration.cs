namespace Oravey2.Core.AI.Utility;

public sealed record AIConsideration(
    string Name,
    Func<AIBlackboard, float> Evaluate,
    float Weight);
