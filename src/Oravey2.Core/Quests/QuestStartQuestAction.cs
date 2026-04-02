namespace Oravey2.Core.Quests;

using Oravey2.Core.Framework.Events;

public sealed class QuestStartQuestAction : IQuestAction
{
    public string QuestId { get; }

    public QuestStartQuestAction(string questId)
    {
        QuestId = questId;
    }

    public void Execute(QuestContext context)
        => context.EventBus.Publish(new QuestStartRequestedEvent(QuestId));
}
