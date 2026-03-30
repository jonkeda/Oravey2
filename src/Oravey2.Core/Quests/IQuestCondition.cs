namespace Oravey2.Core.Quests;

public interface IQuestCondition
{
    bool Evaluate(QuestContext context);
}

public sealed class HasItemCondition : IQuestCondition
{
    public string ItemId { get; }
    public int Count { get; }

    public HasItemCondition(string itemId, int count = 1)
    {
        ItemId = itemId;
        Count = count;
    }

    public bool Evaluate(QuestContext context)
        => context.Inventory.Contains(ItemId, Count);
}

public sealed class QuestFlagCondition : IQuestCondition
{
    public string Flag { get; }
    public bool Expected { get; }

    public QuestFlagCondition(string flag, bool expected = true)
    {
        Flag = flag;
        Expected = expected;
    }

    public bool Evaluate(QuestContext context)
        => context.WorldState.GetFlag(Flag) == Expected;
}

public sealed class QuestLevelCondition : IQuestCondition
{
    public int MinLevel { get; }

    public QuestLevelCondition(int minLevel)
    {
        MinLevel = minLevel;
    }

    public bool Evaluate(QuestContext context)
        => context.Level.Level >= MinLevel;
}

public sealed class QuestCompleteCondition : IQuestCondition
{
    public string QuestId { get; }

    public QuestCompleteCondition(string questId)
    {
        QuestId = questId;
    }

    public bool Evaluate(QuestContext context)
        => context.QuestLog.GetStatus(QuestId) == QuestStatus.Completed;
}
