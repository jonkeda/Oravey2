namespace Oravey2.Core.Dialogue;

using Oravey2.Core.Framework.Events;

public interface IConsequenceAction
{
    void Execute(DialogueContext context);
}

public sealed class SetFlagAction : IConsequenceAction
{
    public string Flag { get; }
    public bool Value { get; }

    public SetFlagAction(string flag, bool value = true)
    {
        Flag = flag;
        Value = value;
    }

    public void Execute(DialogueContext context)
        => context.WorldState.SetFlag(Flag, Value);
}

public sealed class GiveXPAction : IConsequenceAction
{
    public int Amount { get; }

    public GiveXPAction(int amount)
    {
        Amount = amount;
    }

    public void Execute(DialogueContext context)
        => context.Level.GainXP(Amount);
}

public sealed class StartQuestAction : IConsequenceAction
{
    public string QuestId { get; }

    public StartQuestAction(string questId)
    {
        QuestId = questId;
    }

    public void Execute(DialogueContext context)
        => context.EventBus.Publish(new QuestStartRequestedEvent(QuestId));
}
