namespace Oravey2.Core.Quests;

public sealed class QuestLogComponent
{
    private readonly Dictionary<string, QuestStatus> _quests = new();
    private readonly Dictionary<string, string> _currentStages = new();

    public IReadOnlyDictionary<string, QuestStatus> Quests => _quests;
    public IReadOnlyDictionary<string, string> CurrentStages => _currentStages;

    public QuestStatus GetStatus(string questId)
        => _quests.TryGetValue(questId, out var status) ? status : QuestStatus.NotStarted;

    public string? GetCurrentStage(string questId)
        => _currentStages.TryGetValue(questId, out var stage) ? stage : null;

    public void StartQuest(string questId, string firstStageId)
    {
        if (_quests.ContainsKey(questId) && _quests[questId] != QuestStatus.NotStarted)
            return;

        _quests[questId] = QuestStatus.Active;
        _currentStages[questId] = firstStageId;
    }

    public void AdvanceStage(string questId, string nextStageId)
    {
        if (GetStatus(questId) != QuestStatus.Active) return;
        _currentStages[questId] = nextStageId;
    }

    public void CompleteQuest(string questId)
    {
        _quests[questId] = QuestStatus.Completed;
        _currentStages.Remove(questId);
    }

    public void FailQuest(string questId)
    {
        _quests[questId] = QuestStatus.Failed;
        _currentStages.Remove(questId);
    }
}
