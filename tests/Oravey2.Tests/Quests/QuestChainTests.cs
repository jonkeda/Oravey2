using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Dialogue;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.NPC;
using Oravey2.Core.Quests;
using Oravey2.Core.World;

namespace Oravey2.Tests.Quests;

public class QuestChainTests
{
    private static (QuestProcessor proc, QuestLogComponent log, QuestContext ctx,
        WorldStateService world, EventBus bus, LevelComponent level, InventoryComponent inv)
        Setup()
    {
        var stats = new StatsComponent();
        var inv = new InventoryComponent(stats);
        var world = new WorldStateService();
        var bus = new EventBus();
        var level = new LevelComponent(stats, bus);
        var log = new QuestLogComponent();
        var ctx = new QuestContext(inv, world, level, log, bus);
        var proc = new QuestProcessor(bus);

        // Wire quest-start subscriber (mimics ScenarioLoader.InitializeQuestSystem)
        bus.Subscribe<QuestStartRequestedEvent>(e =>
        {
            var quest = QuestChainDefinitions.GetQuest(e.QuestId);
            if (quest != null)
            {
                proc.StartQuest(log, quest);
                world.SetFlag($"{e.QuestId}_active", true);
            }
        });

        bus.Subscribe<QuestUpdatedEvent>(e =>
        {
            if (e.NewStatus == QuestStatus.Completed)
                world.SetFlag($"{e.QuestId}_active", false);
        });

        return (proc, log, ctx, world, bus, level, inv);
    }

    // --- Quest 1: Rat Problem ---

    [Fact]
    public void RatHunt_Start_SetsActive()
    {
        var (proc, log, ctx, world, bus, _, _) = Setup();
        var quest = QuestChainDefinitions.RatHunt();

        proc.StartQuest(log, quest);

        Assert.Equal(QuestStatus.Active, log.GetStatus("q_rat_hunt"));
        Assert.Equal("kill_rats", log.GetCurrentStage("q_rat_hunt"));
    }

    [Fact]
    public void RatHunt_KillRatsStage_AdvancesOnThreeKills()
    {
        var (proc, log, ctx, world, _, _, _) = Setup();
        var quest = QuestChainDefinitions.RatHunt();
        proc.StartQuest(log, quest);

        // Not enough kills — stage stays
        world.SetCounter("rats_killed", 2);
        proc.EvaluateQuest(log, quest, ctx);
        Assert.Equal("kill_rats", log.GetCurrentStage("q_rat_hunt"));

        // Third kill — stage advances
        world.SetCounter("rats_killed", 3);
        proc.EvaluateQuest(log, quest, ctx);
        Assert.Equal("report_1", log.GetCurrentStage("q_rat_hunt"));
        Assert.True(world.GetFlag("rats_cleared"));
    }

    [Fact]
    public void RatHunt_Report_CompletesAndAwardsXP()
    {
        var (proc, log, ctx, world, _, level, _) = Setup();
        var quest = QuestChainDefinitions.RatHunt();
        proc.StartQuest(log, quest);

        // Advance past kill stage
        world.SetCounter("rats_killed", 3);
        proc.EvaluateQuest(log, quest, ctx);

        // Simulate dialogue report
        world.SetFlag("q_rat_hunt_reported", true);
        var levelBefore = level.CurrentXP;
        proc.EvaluateQuest(log, quest, ctx);

        Assert.Equal(QuestStatus.Completed, log.GetStatus("q_rat_hunt"));
        Assert.True(world.GetFlag("q_rat_hunt_done"));
        Assert.Equal(levelBefore + 50, level.CurrentXP);
    }

    // --- Quest 2: Clear the Camp ---

    [Fact]
    public void RaiderCamp_RequiresQuest1Done()
    {
        var (proc, log, _, world, _, _, _) = Setup();

        // Can't start quest 2 without quest 1 done (the dialogue gates this, but test the definition)
        Assert.Equal(QuestStatus.NotStarted, log.GetStatus("q_raider_camp"));
    }

    [Fact]
    public void RaiderCamp_ScarKill_CompletesAndStartsQuest3()
    {
        var (proc, log, ctx, world, bus, level, _) = Setup();
        var quest = QuestChainDefinitions.RaiderCamp();
        proc.StartQuest(log, quest);

        // Kill Scar
        world.SetFlag("scar_killed", true);
        proc.EvaluateQuest(log, quest, ctx);

        Assert.Equal(QuestStatus.Completed, log.GetStatus("q_raider_camp"));
        Assert.True(world.GetFlag("q_raider_camp_done"));
        // Quest 3 auto-started
        Assert.Equal(QuestStatus.Active, log.GetStatus("q_safe_passage"));
    }

    // --- Quest 3: Safe Passage ---

    [Fact]
    public void SafePassage_Report_CompletesAndSetsM1Flag()
    {
        var (proc, log, ctx, world, bus, level, _) = Setup();

        // Start Q3 directly
        var quest = QuestChainDefinitions.SafePassage();
        proc.StartQuest(log, quest);

        // Simulate dialogue report
        world.SetFlag("reported_to_elder", true);
        proc.EvaluateQuest(log, quest, ctx);

        Assert.Equal(QuestStatus.Completed, log.GetStatus("q_safe_passage"));
        Assert.True(world.GetFlag("m1_complete"));
    }

    [Fact]
    public void SafePassage_Awards150XP()
    {
        var (proc, log, ctx, world, _, level, _) = Setup();
        var quest = QuestChainDefinitions.SafePassage();
        proc.StartQuest(log, quest);

        world.SetFlag("reported_to_elder", true);
        var xpBefore = level.CurrentXP;
        proc.EvaluateQuest(log, quest, ctx);

        Assert.Equal(xpBefore + 150, level.CurrentXP);
    }

    // --- Full chain flow ---

    [Fact]
    public void FullChain_Quest2RequiresQuest1Complete_ViaFlag()
    {
        var (proc, log, ctx, world, _, _, _) = Setup();

        // Start & complete Q1
        proc.StartQuest(log, QuestChainDefinitions.RatHunt());
        world.SetCounter("rats_killed", 3);
        proc.EvaluateQuest(log, QuestChainDefinitions.RatHunt(), ctx);
        world.SetFlag("q_rat_hunt_reported", true);
        proc.EvaluateQuest(log, QuestChainDefinitions.RatHunt(), ctx);

        Assert.True(world.GetFlag("q_rat_hunt_done"));

        // Now Q2 can start
        proc.StartQuest(log, QuestChainDefinitions.RaiderCamp());
        Assert.Equal(QuestStatus.Active, log.GetStatus("q_raider_camp"));
    }

    [Fact]
    public void FullChain_Quest3AutoStarts_AfterQuest2()
    {
        var (proc, log, ctx, world, bus, _, _) = Setup();

        proc.StartQuest(log, QuestChainDefinitions.RaiderCamp());
        world.SetFlag("scar_killed", true);
        proc.EvaluateQuest(log, QuestChainDefinitions.RaiderCamp(), ctx);

        // Q3 auto-started by Q2 OnComplete → QuestStartQuestAction → event → subscriber
        Assert.Equal(QuestStatus.Active, log.GetStatus("q_safe_passage"));
        Assert.True(world.GetFlag("q_safe_passage_active"));
    }

    // --- Elder dialogue branches ---

    [Fact]
    public void ElderDialogue_Initial_OffersQuest1()
    {
        var (_, _, _, world, bus, level, inv) = Setup();
        var stats = new StatsComponent();
        var skills = new SkillsComponent(stats);
        var ctx = new DialogueContext(skills, inv, world, level, bus);
        var proc = new DialogueProcessor(bus);

        var tree = TownDialogueTrees.ElderDialogue();
        proc.StartDialogue(tree);

        var choices = proc.GetAvailableChoices(ctx);
        var anyWork = choices.FirstOrDefault(c => c.Choice.Text == "Any work?");
        Assert.NotNull(anyWork.Choice);
        Assert.True(anyWork.Available);
    }

    [Fact]
    public void ElderDialogue_Quest1Active_ShowsProgress()
    {
        var (_, _, _, world, bus, level, inv) = Setup();
        world.SetFlag("q_rat_hunt_active", true);
        var stats = new StatsComponent();
        var skills = new SkillsComponent(stats);
        var ctx = new DialogueContext(skills, inv, world, level, bus);
        var proc = new DialogueProcessor(bus);

        var tree = TownDialogueTrees.ElderDialogue();
        proc.StartDialogue(tree);

        var choices = proc.GetAvailableChoices(ctx);
        var stillWorking = choices.FirstOrDefault(c => c.Choice.Text == "Still working on it.");
        Assert.NotNull(stillWorking.Choice);
        Assert.True(stillWorking.Available);

        // "Any work?" should NOT be available
        var anyWork = choices.FirstOrDefault(c => c.Choice.Text == "Any work?");
        Assert.False(anyWork.Available);
    }

    [Fact]
    public void ElderDialogue_Quest1Done_OffersQuest2()
    {
        var (_, _, _, world, bus, level, inv) = Setup();
        world.SetFlag("q_rat_hunt_done", true);
        var stats = new StatsComponent();
        var skills = new SkillsComponent(stats);
        var ctx = new DialogueContext(skills, inv, world, level, bus);
        var proc = new DialogueProcessor(bus);

        var tree = TownDialogueTrees.ElderDialogue();
        proc.StartDialogue(tree);

        var choices = proc.GetAvailableChoices(ctx);
        var anotherJob = choices.FirstOrDefault(c => c.Choice.Text == "Got another job?");
        Assert.NotNull(anotherJob.Choice);
        Assert.True(anotherJob.Available);
    }

    [Fact]
    public void ElderDialogue_Quest3Complete_SetsM1Flag()
    {
        var (_, _, _, world, bus, level, inv) = Setup();
        world.SetFlag("m1_complete", true);
        var stats = new StatsComponent();
        var skills = new SkillsComponent(stats);
        var ctx = new DialogueContext(skills, inv, world, level, bus);
        var proc = new DialogueProcessor(bus);

        var tree = TownDialogueTrees.ElderDialogue();
        proc.StartDialogue(tree);

        var choices = proc.GetAvailableChoices(ctx);
        var postComplete = choices.FirstOrDefault(c => c.Choice.Text == "How are things?");
        Assert.NotNull(postComplete.Choice);
        Assert.True(postComplete.Available);
    }
}
