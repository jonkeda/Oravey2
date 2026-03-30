namespace Oravey2.Core.Quests;

using Oravey2.Core.Framework.Events;

public interface IQuestAction
{
    void Execute(QuestContext context);
}

public sealed class QuestSetFlagAction : IQuestAction
{
    public string Flag { get; }
    public bool Value { get; }

    public QuestSetFlagAction(string flag, bool value = true)
    {
        Flag = flag;
        Value = value;
    }

    public void Execute(QuestContext context)
        => context.WorldState.SetFlag(Flag, Value);
}

public sealed class QuestGiveXPAction : IQuestAction
{
    public int Amount { get; }

    public QuestGiveXPAction(int amount)
    {
        Amount = amount;
    }

    public void Execute(QuestContext context)
        => context.Level.GainXP(Amount);
}

public sealed class UpdateJournalAction : IQuestAction
{
    public string QuestId { get; }
    public string Text { get; }

    public UpdateJournalAction(string questId, string text)
    {
        QuestId = questId;
        Text = text;
    }

    public void Execute(QuestContext context)
        => context.EventBus.Publish(new JournalUpdatedEvent(QuestId, Text));
}
