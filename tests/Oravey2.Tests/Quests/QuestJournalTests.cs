using Oravey2.Core.Quests;
using Oravey2.Core.World;

namespace Oravey2.Tests.Quests;

public class QuestJournalTests
{
    [Fact]
    public void NoQuests_EmptyLists()
    {
        var log = new QuestLogComponent();

        var (active, completed) = BuildJournalEntries(log, new WorldStateService());

        Assert.Empty(active);
        Assert.Empty(completed);
    }

    [Fact]
    public void ActiveQuest_AppearsInActiveList()
    {
        var log = new QuestLogComponent();
        var quest = QuestChainDefinitions.RatHunt();
        log.StartQuest(quest.Id, quest.FirstStageId);

        var (active, completed) = BuildJournalEntries(log, new WorldStateService());

        Assert.Single(active);
        Assert.Equal("q_rat_hunt", active[0].Id);
        Assert.Equal(quest.Title, active[0].Title);
        Assert.Equal(quest.Description, active[0].Description);
        Assert.Equal(quest.XPReward, active[0].XpReward);
        Assert.Empty(completed);
    }

    [Fact]
    public void CompletedQuest_AppearsInCompletedList()
    {
        var log = new QuestLogComponent();
        var quest = QuestChainDefinitions.RatHunt();
        log.StartQuest(quest.Id, quest.FirstStageId);
        log.CompleteQuest(quest.Id);

        var (active, completed) = BuildJournalEntries(log, new WorldStateService());

        Assert.Empty(active);
        Assert.Single(completed);
        Assert.Equal("q_rat_hunt", completed[0].Id);
    }

    [Fact]
    public void ActiveQuest_ShowsCurrentObjective()
    {
        var log = new QuestLogComponent();
        var quest = QuestChainDefinitions.RatHunt();
        log.StartQuest(quest.Id, quest.FirstStageId);

        var (active, _) = BuildJournalEntries(log, new WorldStateService());

        var entry = active[0];
        Assert.NotNull(entry.CurrentObjective);
        Assert.NotEmpty(entry.CurrentObjective!);
    }

    [Fact]
    public void ActiveQuest_WithCounter_ShowsProgress()
    {
        var world = new WorldStateService();
        world.SetCounter("rats_killed", 1);

        var log = new QuestLogComponent();
        var quest = QuestChainDefinitions.RatHunt();
        log.StartQuest(quest.Id, quest.FirstStageId);

        var (active, _) = BuildJournalEntries(log, world);

        Assert.Equal("(1/3)", active[0].Progress);
    }

    [Fact]
    public void CompletedQuest_NullObjectiveAndProgress()
    {
        var log = new QuestLogComponent();
        var quest = QuestChainDefinitions.RatHunt();
        log.StartQuest(quest.Id, quest.FirstStageId);
        log.CompleteQuest(quest.Id);

        var (_, completed) = BuildJournalEntries(log, new WorldStateService());

        Assert.Null(completed[0].CurrentObjective);
        Assert.Null(completed[0].Progress);
    }

    [Fact]
    public void MultipleQuests_CorrectCategorization()
    {
        var log = new QuestLogComponent();
        var q1 = QuestChainDefinitions.RatHunt();
        var q2 = QuestChainDefinitions.RaiderCamp();

        log.StartQuest(q1.Id, q1.FirstStageId);
        log.CompleteQuest(q1.Id);
        log.StartQuest(q2.Id, q2.FirstStageId);

        var (active, completed) = BuildJournalEntries(log, new WorldStateService());

        Assert.Single(active);
        Assert.Equal("q_raider_camp", active[0].Id);
        Assert.Single(completed);
        Assert.Equal("q_rat_hunt", completed[0].Id);
    }

    [Fact]
    public void ActiveListPreservesDefinitionOrder()
    {
        var log = new QuestLogComponent();
        var q1 = QuestChainDefinitions.RatHunt();
        var q2 = QuestChainDefinitions.RaiderCamp();
        var q3 = QuestChainDefinitions.SafePassage();

        log.StartQuest(q1.Id, q1.FirstStageId);
        log.StartQuest(q2.Id, q2.FirstStageId);
        log.StartQuest(q3.Id, q3.FirstStageId);

        var (active, _) = BuildJournalEntries(log, new WorldStateService());

        Assert.Equal(3, active.Count);
        Assert.Equal("q_rat_hunt", active[0].Id);
        Assert.Equal("q_raider_camp", active[1].Id);
        Assert.Equal("q_safe_passage", active[2].Id);
    }

    /// <summary>
    /// Mirrors the handler's GetQuestJournalState() response building logic.
    /// </summary>
    private static (List<JournalEntry> Active, List<JournalEntry> Completed)
        BuildJournalEntries(QuestLogComponent log, WorldStateService worldState)
    {
        var active = new List<JournalEntry>();
        var completed = new List<JournalEntry>();

        foreach (var def in QuestChainDefinitions.All)
        {
            var status = log.GetStatus(def.Id);
            if (status == QuestStatus.Active)
            {
                var stageId = log.GetCurrentStage(def.Id);
                string? objective = null;
                string? progress = null;
                if (stageId != null && def.Stages.TryGetValue(stageId, out var stage))
                {
                    objective = stage.Description;
                    progress = GetCounterProgress(stage, worldState);
                }
                active.Add(new JournalEntry(def.Id, def.Title, def.Description, objective, progress, def.XPReward));
            }
            else if (status == QuestStatus.Completed)
            {
                completed.Add(new JournalEntry(def.Id, def.Title, def.Description, null, null, def.XPReward));
            }
        }

        return (active, completed);
    }

    private static string? GetCounterProgress(QuestStage stage, WorldStateService worldState)
    {
        foreach (var condition in stage.Conditions)
        {
            if (condition is QuestCounterCondition counter)
                return $"({worldState.GetCounter(counter.CounterName)}/{counter.MinValue})";
        }
        return null;
    }

    private record JournalEntry(string Id, string Title, string Description, string? CurrentObjective, string? Progress, int XpReward);
}
