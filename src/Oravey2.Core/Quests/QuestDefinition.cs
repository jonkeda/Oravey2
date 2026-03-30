namespace Oravey2.Core.Quests;

public enum QuestStatus
{
    NotStarted,
    Active,
    Completed,
    Failed
}

public enum QuestType
{
    Main,
    Faction,
    Side,
    Radiant
}

public sealed record QuestStage(
    string Id,
    string Description,
    IQuestCondition[] Conditions,
    IQuestAction[] OnCompleteActions,
    string? NextStageId,
    IQuestCondition[] FailConditions,
    IQuestAction[] OnFailActions);

public sealed record QuestDefinition(
    string Id,
    string Title,
    string Description,
    QuestType Type,
    string FirstStageId,
    IReadOnlyDictionary<string, QuestStage> Stages,
    int XPReward = 0);
