namespace Oravey2.Core.Dialogue;

public sealed class CounterCondition : IDialogueCondition
{
    public string CounterName { get; }
    public int MinValue { get; }

    public CounterCondition(string counterName, int minValue)
    {
        CounterName = counterName;
        MinValue = minValue;
    }

    public bool Evaluate(DialogueContext context)
        => context.WorldState.GetCounter(CounterName) >= MinValue;
}
