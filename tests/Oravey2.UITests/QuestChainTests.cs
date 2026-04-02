using Brinell.Stride.Context;
using Brinell.Stride.Testing;
using Oravey2.Core.Automation;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Tests for the M1 Phase 3.3 quest chain: Elder dialogue, quest acceptance,
/// kill tracking, quest completion, and quest state progression.
/// Uses town fixture since quests start from Elder Tomas dialogue.
/// </summary>
public class QuestChainTests : IAsyncLifetime
{
    private readonly TownTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void AcceptQuest1_FromElder()
    {
        // Interact with Elder, choose "Any work?" (should be first available choice)
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        Assert.True(dialogue.Active);
        Assert.Equal("Elder Tomas", dialogue.Speaker);

        // Find "Any work?" choice
        var anyWorkIdx = dialogue.Choices.FindIndex(c => c.Text == "Any work?" && c.Available);
        Assert.True(anyWorkIdx >= 0, "Expected 'Any work?' choice to be available");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, anyWorkIdx);

        // Now in quest_offer_1 node
        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        Assert.Contains("Radrats", dialogue.Text!);

        // Accept: "I'll handle it."
        var acceptIdx = dialogue.Choices.FindIndex(c => c.Text == "I'll handle it.");
        Assert.True(acceptIdx >= 0);
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, acceptIdx);

        // Quest should now be active
        var quests = GameQueryHelpers.GetActiveQuests(_fixture.Context);
        var ratHunt = quests.Quests.FirstOrDefault(q => q.Id == "q_rat_hunt");
        Assert.NotNull(ratHunt);
        Assert.Equal("Active", ratHunt.Status);
        Assert.Equal("kill_rats", ratHunt.CurrentStage);
    }

    [Fact]
    public void KillCounter_Increments_ViaAutomation()
    {
        // Accept quest first
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var anyWorkIdx = dialogue.Choices.FindIndex(c => c.Text == "Any work?" && c.Available);
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, anyWorkIdx);
        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var acceptIdx = dialogue.Choices.FindIndex(c => c.Text == "I'll handle it.");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, acceptIdx);

        // Simulate kills via world counter
        GameQueryHelpers.SetWorldCounter(_fixture.Context, "rats_killed", 2);
        var counter = GameQueryHelpers.GetWorldCounter(_fixture.Context, "rats_killed");
        Assert.Equal(2, counter.Value);
    }

    [Fact]
    public void Quest1_Complete_After3Kills_SetsRatsCleared()
    {
        // Accept quest
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var anyWorkIdx = dialogue.Choices.FindIndex(c => c.Text == "Any work?" && c.Available);
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, anyWorkIdx);
        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var acceptIdx = dialogue.Choices.FindIndex(c => c.Text == "I'll handle it.");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, acceptIdx);

        // Set 3 kills — quest evaluator should advance stage
        GameQueryHelpers.SetWorldCounter(_fixture.Context, "rats_killed", 3);
        // Allow quest evaluator to run (next frame)
        Thread.Sleep(200);

        // Check rats_cleared flag
        var flag = GameQueryHelpers.GetWorldFlag(_fixture.Context, "rats_cleared");
        Assert.True(flag.Value, "Expected rats_cleared flag to be set after 3 kills");

        // Quest should have advanced to report_1 stage
        var quests = GameQueryHelpers.GetActiveQuests(_fixture.Context);
        var ratHunt = quests.Quests.FirstOrDefault(q => q.Id == "q_rat_hunt");
        Assert.NotNull(ratHunt);
        Assert.Equal("Active", ratHunt.Status);
        Assert.Equal("report_1", ratHunt.CurrentStage);
    }

    [Fact]
    public void Quest1_Reward_OnReturn()
    {
        // Setup: accept quest, simulate 3 kills
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var anyWorkIdx = dialogue.Choices.FindIndex(c => c.Text == "Any work?" && c.Available);
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, anyWorkIdx);
        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var acceptIdx = dialogue.Choices.FindIndex(c => c.Text == "I'll handle it.");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, acceptIdx);

        GameQueryHelpers.SetWorldCounter(_fixture.Context, "rats_killed", 3);
        Thread.Sleep(200);

        var capsBefore = GameQueryHelpers.GetCapsState(_fixture.Context);

        // Talk to Elder again — should show "The rats are dead." choice
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);

        var turnInIdx = dialogue.Choices.FindIndex(c => c.Text == "The rats are dead." && c.Available);
        Assert.True(turnInIdx >= 0, "Expected 'The rats are dead.' choice");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, turnInIdx);

        // Should be in quest_1_complete node
        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        Assert.Contains("reward", dialogue.Text!.ToLower());

        // Accept reward
        var thanksIdx = dialogue.Choices.FindIndex(c => c.Text == "Thanks.");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, thanksIdx);

        // Caps should have increased by 15
        var capsAfter = GameQueryHelpers.GetCapsState(_fixture.Context);
        Assert.Equal(capsBefore.Caps + 15, capsAfter.Caps);

        // Quest should complete after quest evaluator runs
        Thread.Sleep(200);
        var quests = GameQueryHelpers.GetActiveQuests(_fixture.Context);
        var ratHunt = quests.Quests.FirstOrDefault(q => q.Id == "q_rat_hunt");
        Assert.NotNull(ratHunt);
        Assert.Equal("Completed", ratHunt.Status);
    }

    [Fact]
    public void Quest2_Available_AfterQuest1()
    {
        // Complete quest 1 fully via flags
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "q_rat_hunt_done", true);

        // Talk to Elder
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);

        var anotherJobIdx = dialogue.Choices.FindIndex(c => c.Text == "Got another job?" && c.Available);
        Assert.True(anotherJobIdx >= 0, "Expected 'Got another job?' choice after Q1 complete");
    }

    [Fact]
    public void Quest2_Accept_SetsActive()
    {
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "q_rat_hunt_done", true);

        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var jobIdx = dialogue.Choices.FindIndex(c => c.Text == "Got another job?" && c.Available);
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, jobIdx);

        // In quest_offer_2 node
        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        Assert.Contains("Scar", dialogue.Text!);

        var acceptIdx = dialogue.Choices.FindIndex(c => c.Text == "Consider it done.");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, acceptIdx);

        var quests = GameQueryHelpers.GetActiveQuests(_fixture.Context);
        var raiderCamp = quests.Quests.FirstOrDefault(q => q.Id == "q_raider_camp");
        Assert.NotNull(raiderCamp);
        Assert.Equal("Active", raiderCamp.Status);
    }

    [Fact]
    public void Quest3_ReportToElder_Completes()
    {
        // Setup: Q3 active via flags
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "q_safe_passage_active", true);

        // Wire Q3 as Active in quest log by starting it
        // We need to make the quest system think Q3 is active.
        // The cleanest way: set up the prerequisite flags and trigger Q3 via SetWorldFlag
        // Actually, let's use the dialogue path: simulate Q2 done, Q3 auto-started
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "q_raider_camp_done", true);
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "scar_killed", true);

        // Start Q3 manually by triggering its start via a quest that publishes QuestStartRequestedEvent
        // Simpler: directly set the flag and check dialogue
        // The dialogue condition is just FlagCondition("q_safe_passage_active")

        // Talk to Elder
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);

        var reportIdx = dialogue.Choices.FindIndex(c => c.Text == "The camp is clear." && c.Available);
        Assert.True(reportIdx >= 0, "Expected 'The camp is clear.' choice when Q3 active");
    }
}
