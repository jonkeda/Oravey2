namespace Oravey2.Core.Quests;

public sealed class QuestCounterCondition : IQuestCondition
{
    public string CounterName { get; }
    public int MinValue { get; }

    public QuestCounterCondition(string counterName, int minValue)
    {
        CounterName = counterName;
        MinValue = minValue;
    }

    public bool Evaluate(QuestContext context)
        => context.WorldState.GetCounter(CounterName) >= MinValue;
}
