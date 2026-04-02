using Oravey2.Core.Quests;
using Oravey2.Core.Save;
using Oravey2.Core.World;

namespace Oravey2.Tests.Save;

public class QuestStatePersistenceTests
{
    [Fact]
    public void SaveDataBuilder_WithQuestStates_CapturesQuestStatus()
    {
        var quests = new Dictionary<string, QuestStatus>
        {
            ["q_rat_hunt"] = QuestStatus.Completed,
            ["q_raider_camp"] = QuestStatus.Active,
        };
        var stages = new Dictionary<string, string>
        {
            ["q_raider_camp"] = "kill_scar",
        };
        var flags = new Dictionary<string, bool>
        {
            ["rats_cleared"] = true,
        };
        var counters = new Dictionary<string, int>
        {
            ["rats_killed"] = 3,
        };

        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithQuestStates(quests, stages, flags, counters)
            .Build();

        Assert.Equal(QuestStatus.Completed, data.QuestStates["q_rat_hunt"]);
        Assert.Equal(QuestStatus.Active, data.QuestStates["q_raider_camp"]);
        Assert.Equal("kill_scar", data.QuestStages["q_raider_camp"]);
        Assert.True(data.WorldFlags["rats_cleared"]);
        Assert.Equal(3, data.WorldCounters["rats_killed"]);
    }

    [Fact]
    public void SaveDataRestorer_ExposesQuestState()
    {
        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithQuestStates(
                new Dictionary<string, QuestStatus> { ["q1"] = QuestStatus.Active },
                new Dictionary<string, string> { ["q1"] = "s1" },
                new Dictionary<string, bool> { ["flag1"] = true },
                new Dictionary<string, int> { ["counter1"] = 5 })
            .Build();

        var restorer = new SaveDataRestorer(data);

        Assert.Equal(QuestStatus.Active, restorer.QuestStates["q1"]);
        Assert.Equal("s1", restorer.QuestStages["q1"]);
        Assert.True(restorer.WorldFlags["flag1"]);
        Assert.Equal(5, restorer.WorldCounters["counter1"]);
    }

    [Fact]
    public void QuestLog_RestoreFromSave_SetsStatusAndStages()
    {
        var log = new QuestLogComponent();
        log.StartQuest("old_quest", "old_stage");

        log.RestoreFromSave(
            new Dictionary<string, QuestStatus>
            {
                ["q_rat_hunt"] = QuestStatus.Completed,
                ["q_raider_camp"] = QuestStatus.Active,
            },
            new Dictionary<string, string>
            {
                ["q_raider_camp"] = "kill_scar",
            });

        Assert.Equal(QuestStatus.Completed, log.GetStatus("q_rat_hunt"));
        Assert.Equal(QuestStatus.Active, log.GetStatus("q_raider_camp"));
        Assert.Equal("kill_scar", log.GetCurrentStage("q_raider_camp"));
        Assert.Null(log.GetCurrentStage("q_rat_hunt"));
        // Old quest should be gone
        Assert.Equal(QuestStatus.NotStarted, log.GetStatus("old_quest"));
    }

    [Fact]
    public void WorldState_RestoreFromSave_SetsFlagsAndCounters()
    {
        var world = new WorldStateService();
        world.SetFlag("old_flag", true);
        world.SetCounter("old_counter", 99);

        world.RestoreFromSave(
            new Dictionary<string, bool> { ["rats_cleared"] = true, ["scar_killed"] = true },
            new Dictionary<string, int> { ["rats_killed"] = 3 });

        Assert.True(world.GetFlag("rats_cleared"));
        Assert.True(world.GetFlag("scar_killed"));
        Assert.Equal(3, world.GetCounter("rats_killed"));
        // Old state should be cleared
        Assert.False(world.GetFlag("old_flag"));
        Assert.Equal(0, world.GetCounter("old_counter"));
    }

    [Fact]
    public void RoundTrip_QuestState_PreservedThroughSaveLoad()
    {
        // Set up game state
        var log = new QuestLogComponent();
        var world = new WorldStateService();
        var quest = QuestChainDefinitions.RatHunt();
        log.StartQuest(quest.Id, quest.FirstStageId);
        world.SetCounter("rats_killed", 2);
        world.SetFlag("some_flag", true);

        // Save
        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithQuestStates(
                new Dictionary<string, QuestStatus>(log.Quests),
                new Dictionary<string, string>(log.CurrentStages),
                new Dictionary<string, bool>(world.Flags),
                new Dictionary<string, int>(world.Counters))
            .Build();

        // Create fresh components
        var log2 = new QuestLogComponent();
        var world2 = new WorldStateService();

        // Restore
        var restorer = new SaveDataRestorer(data);
        log2.RestoreFromSave(restorer.QuestStates, restorer.QuestStages);
        world2.RestoreFromSave(restorer.WorldFlags, restorer.WorldCounters);

        // Verify round-trip
        Assert.Equal(QuestStatus.Active, log2.GetStatus("q_rat_hunt"));
        Assert.Equal("kill_rats", log2.GetCurrentStage("q_rat_hunt"));
        Assert.Equal(2, world2.GetCounter("rats_killed"));
        Assert.True(world2.GetFlag("some_flag"));
    }

    [Fact]
    public void RoundTrip_CompletedQuest_NoCurrentStage()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "s1");
        log.CompleteQuest("q1");

        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithQuestStates(
                new Dictionary<string, QuestStatus>(log.Quests),
                new Dictionary<string, string>(log.CurrentStages),
                [], [])
            .Build();

        var log2 = new QuestLogComponent();
        var restorer = new SaveDataRestorer(data);
        log2.RestoreFromSave(restorer.QuestStates, restorer.QuestStages);

        Assert.Equal(QuestStatus.Completed, log2.GetStatus("q1"));
        Assert.Null(log2.GetCurrentStage("q1"));
    }
}
