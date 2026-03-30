using Oravey2.Core.Quests;

namespace Oravey2.Core.UI.ViewModels;

/// <summary>
/// Snapshot of quest log for the quest screen. Separates quests by status.
/// </summary>
public sealed record QuestEntry(
    string QuestId,
    QuestStatus Status,
    string? CurrentStageId
);

public sealed record QuestLogViewModel(
    IReadOnlyList<QuestEntry> Active,
    IReadOnlyList<QuestEntry> Completed,
    IReadOnlyList<QuestEntry> Failed
)
{
    public static QuestLogViewModel Create(QuestLogComponent questLog)
    {
        var active = new List<QuestEntry>();
        var completed = new List<QuestEntry>();
        var failed = new List<QuestEntry>();

        foreach (var (questId, status) in questLog.Quests)
        {
            var entry = new QuestEntry(questId, status, questLog.GetCurrentStage(questId));
            switch (status)
            {
                case QuestStatus.Active:
                    active.Add(entry);
                    break;
                case QuestStatus.Completed:
                    completed.Add(entry);
                    break;
                case QuestStatus.Failed:
                    failed.Add(entry);
                    break;
            }
        }

        return new QuestLogViewModel(active, completed, failed);
    }
}
