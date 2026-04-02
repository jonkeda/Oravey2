using Oravey2.Core.Quests;
using Oravey2.Core.World;

namespace Oravey2.Tests.Quests;

public class QuestTrackerTests
{
    [Fact]
    public void ActiveQuest_TrackedQuestId_ReturnsFirstActive()
    {
        var log = new QuestLogComponent();
        var quest = QuestChainDefinitions.RatHunt();
        log.StartQuest(quest.Id, quest.FirstStageId);

        // Simulates what QuestTrackerScript.Update() does
        string? trackedId = null;
        foreach (var def in QuestChainDefinitions.All)
        {
            if (log.GetStatus(def.Id) == QuestStatus.Active)
            {
                trackedId = def.Id;
                break;
            }
        }

        Assert.Equal("q_rat_hunt", trackedId);
    }

    [Fact]
    public void NoActiveQuest_TrackedQuestId_ReturnsNull()
    {
        var log = new QuestLogComponent();

        string? trackedId = null;
        foreach (var def in QuestChainDefinitions.All)
        {
            if (log.GetStatus(def.Id) == QuestStatus.Active)
            {
                trackedId = def.Id;
                break;
            }
        }

        Assert.Null(trackedId);
    }

    [Fact]
    public void CompletedQuest_NotTracked()
    {
        var log = new QuestLogComponent();
        var quest = QuestChainDefinitions.RatHunt();
        log.StartQuest(quest.Id, quest.FirstStageId);
        log.CompleteQuest(quest.Id);

        string? trackedId = null;
        foreach (var def in QuestChainDefinitions.All)
        {
            if (log.GetStatus(def.Id) == QuestStatus.Active)
            {
                trackedId = def.Id;
                break;
            }
        }

        Assert.Null(trackedId);
    }

    [Fact]
    public void CounterProgress_ShowsCurrentOverTarget()
    {
        var world = new WorldStateService();
        world.SetCounter("rats_killed", 2);

        var quest = QuestChainDefinitions.RatHunt();
        var stage = quest.Stages[quest.FirstStageId];

        var progress = GetProgressText(stage, world);

        Assert.Equal("(2/3)", progress);
    }

    [Fact]
    public void CounterProgress_ZeroKills_ShowsZero()
    {
        var world = new WorldStateService();
        var quest = QuestChainDefinitions.RatHunt();
        var stage = quest.Stages[quest.FirstStageId];

        var progress = GetProgressText(stage, world);

        Assert.Equal("(0/3)", progress);
    }

    [Fact]
    public void NoCounterCondition_EmptyProgress()
    {
        var world = new WorldStateService();
        // report_1 stage uses a flag condition, not a counter
        var quest = QuestChainDefinitions.RatHunt();
        var stage = quest.Stages["report_1"];

        var progress = GetProgressText(stage, world);

        Assert.Equal("", progress);
    }

    [Fact]
    public void StageDescription_MatchesDefinition()
    {
        var quest = QuestChainDefinitions.RatHunt();
        var stage = quest.Stages[quest.FirstStageId];

        Assert.NotNull(stage.Description);
        Assert.NotEmpty(stage.Description);
    }

    /// <summary>
    /// Mirrors QuestTrackerScript.GetProgressText() logic.
    /// </summary>
    private static string GetProgressText(QuestStage stage, WorldStateService worldState)
    {
        foreach (var condition in stage.Conditions)
        {
            if (condition is QuestCounterCondition counter)
            {
                var current = worldState.GetCounter(counter.CounterName);
                var target = counter.MinValue;
                return $"({current}/{target})";
            }
        }
        return "";
    }
}
