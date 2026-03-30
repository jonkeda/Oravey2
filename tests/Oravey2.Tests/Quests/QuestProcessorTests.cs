using Oravey2.Core.Character.Stats;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Quests;
using Oravey2.Core.World;

namespace Oravey2.Tests.Quests;

public class QuestProcessorTests
{
    private static (QuestProcessor proc, QuestLogComponent log, QuestContext ctx, EventBus bus) Setup()
    {
        var stats = new StatsComponent();
        var inv = new InventoryComponent(stats);
        var world = new WorldStateService();
        var bus = new EventBus();
        var level = new LevelComponent(stats, bus);
        var log = new QuestLogComponent();
        var ctx = new QuestContext(inv, world, level, log, bus);
        var proc = new QuestProcessor(bus);
        return (proc, log, ctx, bus);
    }

    private static QuestDefinition MakeTwoStageQuest(
        IQuestCondition[]? stage1Conditions = null,
        IQuestAction[]? stage1OnComplete = null,
        IQuestCondition[]? stage1FailConditions = null,
        IQuestAction[]? stage1OnFail = null,
        IQuestCondition[]? stage2Conditions = null,
        int xpReward = 0)
    {
        var stage1 = new QuestStage(
            "s1", "Stage 1",
            stage1Conditions ?? [],
            stage1OnComplete ?? [],
            "s2",
            stage1FailConditions ?? [],
            stage1OnFail ?? []);

        var stage2 = new QuestStage(
            "s2", "Stage 2",
            stage2Conditions ?? [],
            [],
            null, // last stage
            [],
            []);

        return new QuestDefinition(
            "test_quest", "Test Quest", "A test quest",
            QuestType.Side, "s1",
            new Dictionary<string, QuestStage> { ["s1"] = stage1, ["s2"] = stage2 },
            xpReward);
    }

    private static QuestDefinition MakeSingleStageQuest(
        IQuestCondition[]? conditions = null,
        IQuestAction[]? onComplete = null,
        IQuestCondition[]? failConditions = null,
        IQuestAction[]? onFail = null,
        int xpReward = 0)
    {
        var stage = new QuestStage(
            "s1", "Only stage",
            conditions ?? [],
            onComplete ?? [],
            null,
            failConditions ?? [],
            onFail ?? []);

        return new QuestDefinition(
            "test_quest", "Test", "Test",
            QuestType.Side, "s1",
            new Dictionary<string, QuestStage> { ["s1"] = stage },
            xpReward);
    }

    // --- StartQuest ---

    [Fact]
    public void StartQuest_AddsToLog_PublishesEvent()
    {
        var (proc, log, _, bus) = Setup();
        QuestUpdatedEvent? received = null;
        bus.Subscribe<QuestUpdatedEvent>(e => received = e);

        var quest = MakeSingleStageQuest();
        proc.StartQuest(log, quest);

        Assert.Equal(QuestStatus.Active, log.GetStatus("test_quest"));
        Assert.NotNull(received);
        Assert.Equal(QuestStatus.Active, received.Value.NewStatus);
    }

    [Fact]
    public void StartQuest_AlreadyActive_NoOp()
    {
        var (proc, log, _, bus) = Setup();
        var quest = MakeSingleStageQuest();
        proc.StartQuest(log, quest);

        int eventCount = 0;
        bus.Subscribe<QuestUpdatedEvent>(_ => eventCount++);
        proc.StartQuest(log, quest);

        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void StartQuest_Completed_NoOp()
    {
        var (proc, log, _, bus) = Setup();
        var quest = MakeSingleStageQuest();
        log.StartQuest("test_quest", "s1");
        log.CompleteQuest("test_quest");

        int eventCount = 0;
        bus.Subscribe<QuestUpdatedEvent>(_ => eventCount++);
        proc.StartQuest(log, quest);

        Assert.Equal(0, eventCount);
    }

    // --- EvaluateQuest ---

    [Fact]
    public void EvaluateQuest_NotActive_ReturnsFalse()
    {
        var (proc, log, ctx, _) = Setup();
        var quest = MakeSingleStageQuest();
        Assert.False(proc.EvaluateQuest(log, quest, ctx));
    }

    [Fact]
    public void EvaluateQuest_ConditionsMet_AdvancesStage()
    {
        var (proc, log, ctx, _) = Setup();
        ctx.WorldState.SetFlag("stage1_done", true);

        var quest = MakeTwoStageQuest(
            stage1Conditions: [new QuestFlagCondition("stage1_done")]);

        proc.StartQuest(log, quest);
        var changed = proc.EvaluateQuest(log, quest, ctx);

        Assert.True(changed);
        Assert.Equal("s2", log.GetCurrentStage("test_quest"));
    }

    [Fact]
    public void EvaluateQuest_ConditionsNotMet_ReturnsFalse()
    {
        var (proc, log, ctx, _) = Setup();
        var quest = MakeTwoStageQuest(
            stage1Conditions: [new QuestFlagCondition("not_set")]);

        proc.StartQuest(log, quest);
        Assert.False(proc.EvaluateQuest(log, quest, ctx));
        Assert.Equal("s1", log.GetCurrentStage("test_quest"));
    }

    [Fact]
    public void EvaluateQuest_LastStage_CompletesQuest()
    {
        var (proc, log, ctx, bus) = Setup();
        var quest = MakeSingleStageQuest();

        proc.StartQuest(log, quest);

        QuestUpdatedEvent? completed = null;
        bus.Subscribe<QuestUpdatedEvent>(e => { if (e.NewStatus == QuestStatus.Completed) completed = e; });

        var changed = proc.EvaluateQuest(log, quest, ctx);

        Assert.True(changed);
        Assert.Equal(QuestStatus.Completed, log.GetStatus("test_quest"));
        Assert.NotNull(completed);
    }

    [Fact]
    public void EvaluateQuest_LastStage_GrantsXPReward()
    {
        var (proc, log, ctx, _) = Setup();
        var quest = MakeSingleStageQuest(xpReward: 200);

        proc.StartQuest(log, quest);
        var before = ctx.Level.CurrentXP;
        proc.EvaluateQuest(log, quest, ctx);

        Assert.Equal(before + 200, ctx.Level.CurrentXP);
    }

    [Fact]
    public void EvaluateQuest_ExecutesOnCompleteActions()
    {
        var (proc, log, ctx, _) = Setup();
        var quest = MakeTwoStageQuest(
            stage1OnComplete: [new QuestSetFlagAction("stage_cleared")]);

        proc.StartQuest(log, quest);
        proc.EvaluateQuest(log, quest, ctx);

        Assert.True(ctx.WorldState.GetFlag("stage_cleared"));
    }

    [Fact]
    public void EvaluateQuest_PublishesStageCompletedEvent()
    {
        var (proc, log, ctx, bus) = Setup();
        var quest = MakeTwoStageQuest();

        proc.StartQuest(log, quest);

        QuestStageCompletedEvent? received = null;
        bus.Subscribe<QuestStageCompletedEvent>(e => received = e);
        proc.EvaluateQuest(log, quest, ctx);

        Assert.NotNull(received);
        Assert.Equal("test_quest", received.Value.QuestId);
        Assert.Equal("s1", received.Value.StageId);
    }

    [Fact]
    public void EvaluateQuest_FailConditionMet_FailsQuest()
    {
        var (proc, log, ctx, bus) = Setup();
        ctx.WorldState.SetFlag("crate_destroyed", true);

        var quest = MakeSingleStageQuest(
            conditions: [new QuestFlagCondition("delivered")],
            failConditions: [new QuestFlagCondition("crate_destroyed")]);

        proc.StartQuest(log, quest);

        QuestUpdatedEvent? received = null;
        bus.Subscribe<QuestUpdatedEvent>(e => { if (e.NewStatus == QuestStatus.Failed) received = e; });
        var changed = proc.EvaluateQuest(log, quest, ctx);

        Assert.True(changed);
        Assert.Equal(QuestStatus.Failed, log.GetStatus("test_quest"));
        Assert.NotNull(received);
    }

    [Fact]
    public void EvaluateQuest_FailCondition_ExecutesOnFailActions()
    {
        var (proc, log, ctx, _) = Setup();
        ctx.WorldState.SetFlag("crate_destroyed", true);

        var quest = MakeSingleStageQuest(
            failConditions: [new QuestFlagCondition("crate_destroyed")],
            onFail: [new QuestSetFlagAction("quest_failed_flag")]);

        proc.StartQuest(log, quest);
        proc.EvaluateQuest(log, quest, ctx);

        Assert.True(ctx.WorldState.GetFlag("quest_failed_flag"));
    }

    [Fact]
    public void EvaluateQuest_FailCondition_CheckedBeforeCompletion()
    {
        var (proc, log, ctx, _) = Setup();
        // Both fail and completion conditions are true — fail should win
        ctx.WorldState.SetFlag("target_flag", true);

        var quest = MakeSingleStageQuest(
            conditions: [new QuestFlagCondition("target_flag")],
            failConditions: [new QuestFlagCondition("target_flag")]);

        proc.StartQuest(log, quest);
        proc.EvaluateQuest(log, quest, ctx);

        Assert.Equal(QuestStatus.Failed, log.GetStatus("test_quest"));
    }

    [Fact]
    public void EvaluateQuest_EmptyConditions_CompletesImmediately()
    {
        var (proc, log, ctx, _) = Setup();
        var quest = MakeSingleStageQuest();

        proc.StartQuest(log, quest);
        var changed = proc.EvaluateQuest(log, quest, ctx);

        Assert.True(changed);
        Assert.Equal(QuestStatus.Completed, log.GetStatus("test_quest"));
    }
}
