namespace Oravey2.Core.Quests;

using Oravey2.Core.Framework.Events;

public sealed class QuestProcessor
{
    private readonly IEventBus _eventBus;

    public QuestProcessor(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void StartQuest(QuestLogComponent log, QuestDefinition quest)
    {
        if (log.GetStatus(quest.Id) != QuestStatus.NotStarted)
            return;

        log.StartQuest(quest.Id, quest.FirstStageId);
        _eventBus.Publish(new QuestUpdatedEvent(quest.Id, QuestStatus.Active));
    }

    public bool EvaluateQuest(QuestLogComponent log, QuestDefinition quest, QuestContext context)
    {
        if (log.GetStatus(quest.Id) != QuestStatus.Active)
            return false;

        var stageId = log.GetCurrentStage(quest.Id);
        if (stageId == null || !quest.Stages.TryGetValue(stageId, out var stage))
            return false;

        // Check fail conditions (OR — any true = quest fails)
        if (stage.FailConditions.Length > 0)
        {
            foreach (var failCondition in stage.FailConditions)
            {
                if (failCondition.Evaluate(context))
                {
                    foreach (var action in stage.OnFailActions)
                        action.Execute(context);

                    log.FailQuest(quest.Id);
                    _eventBus.Publish(new QuestUpdatedEvent(quest.Id, QuestStatus.Failed));
                    return true;
                }
            }
        }

        // Check completion conditions (AND — all must be true)
        foreach (var condition in stage.Conditions)
        {
            if (!condition.Evaluate(context))
                return false;
        }

        // All conditions met — execute on-complete actions
        foreach (var action in stage.OnCompleteActions)
            action.Execute(context);

        _eventBus.Publish(new QuestStageCompletedEvent(quest.Id, stageId));

        // Advance or complete
        if (stage.NextStageId != null)
        {
            log.AdvanceStage(quest.Id, stage.NextStageId);
        }
        else
        {
            if (quest.XPReward > 0)
                context.Level.GainXP(quest.XPReward);

            log.CompleteQuest(quest.Id);
            _eventBus.Publish(new QuestUpdatedEvent(quest.Id, QuestStatus.Completed));
        }

        return true;
    }
}
