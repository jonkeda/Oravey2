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

public sealed class QuestStage
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public IQuestCondition[] Conditions { get; set; } = [];
    public IQuestAction[] OnCompleteActions { get; set; } = [];
    public string? NextStageId { get; set; }
    public IQuestCondition[] FailConditions { get; set; } = [];
    public IQuestAction[] OnFailActions { get; set; } = [];
}

public sealed class QuestDefinition
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public QuestType Type { get; set; }
    public string FirstStageId { get; set; } = "";
    public IReadOnlyDictionary<string, QuestStage> Stages { get; set; } = new Dictionary<string, QuestStage>();
    public int XPReward { get; set; }
}
