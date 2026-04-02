using Brinell.Stride.Context;
using Brinell.Stride.Infrastructure;
using Brinell.Stride.Testing;
using Oravey2.Core.Automation;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Integration tests for M1 Phase 3.5: enemy respawn on zone re-entry,
/// boss no-respawn after kill, save/load quest preservation, and full E2E quest chain.
/// Uses town fixture — transitions to wasteland and back as needed.
/// </summary>
public class QuestIntegrationTests : IAsyncLifetime
{
    private readonly TownTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void EnemiesRespawn_OnZoneReEntry()
    {
        // Transition town → wasteland
        TransitionToWasteland();

        // Count enemies
        var combat1 = GameQueryHelpers.GetCombatState(_fixture.Context);
        var initialCount = combat1.EnemyCount;
        Assert.True(initialCount >= 3, "Expected at least 3 radrats in wasteland");

        // Kill one enemy
        var firstEnemy = combat1.Enemies.First(e => e.IsAlive);
        GameQueryHelpers.KillEnemy(_fixture.Context, firstEnemy.Id);

        var combat2 = GameQueryHelpers.GetCombatState(_fixture.Context);
        var aliveAfterKill = combat2.Enemies.Count(e => e.IsAlive);
        Assert.Equal(initialCount - 1, aliveAfterKill);

        // Return to town
        TransitionToTown();

        // Re-enter wasteland — enemies should respawn fresh
        TransitionToWasteland();

        var combat3 = GameQueryHelpers.GetCombatState(_fixture.Context);
        var respawnedCount = combat3.Enemies.Count(e => e.IsAlive);
        Assert.True(respawnedCount >= initialCount, "Enemies should respawn on zone re-entry");
    }

    [Fact]
    public void BossDoesNotRespawn_AfterKill()
    {
        // Accept quest 2 (raider camp) by setting prerequisites
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "q_rat_hunt_done", true);
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);

        var jobIdx = dialogue.Choices.FindIndex(c => c.Text == "Got another job?" && c.Available);
        Assert.True(jobIdx >= 0, "Expected 'Got another job?' choice");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, jobIdx);

        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var acceptIdx = dialogue.Choices.FindIndex(c => c.Text == "Consider it done.");
        Assert.True(acceptIdx >= 0, "Expected 'Consider it done.' choice");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, acceptIdx);

        // Verify quest is active
        var quests = GameQueryHelpers.GetActiveQuests(_fixture.Context);
        Assert.Contains(quests.Quests, q => q.Id == "q_raider_camp" && q.Status == "Active");

        // Transition to wasteland — Scar should be present
        TransitionToWasteland();

        var combat1 = GameQueryHelpers.GetCombatState(_fixture.Context);
        var scar = combat1.Enemies.FirstOrDefault(e => e.Id.Contains("scar"));
        Assert.NotNull(scar);

        // Kill Scar
        GameQueryHelpers.KillEnemy(_fixture.Context, scar.Id);
        Thread.Sleep(200); // Allow quest evaluator to process

        // Verify scar_killed flag
        var flag = GameQueryHelpers.GetWorldFlag(_fixture.Context, "scar_killed");
        Assert.True(flag.Value);

        // Return to town and re-enter wasteland
        TransitionToTown();
        TransitionToWasteland();

        // Scar should NOT respawn (quest completed)
        var combat2 = GameQueryHelpers.GetCombatState(_fixture.Context);
        var scarRespawned = combat2.Enemies.Any(e => e.Id.Contains("scar"));
        Assert.False(scarRespawned, "Scar should not respawn after being killed");

        // Radrats should still be alive (they respawn)
        var radrats = combat2.Enemies.Count(e => e.IsAlive);
        Assert.True(radrats >= 3, "Radrats should respawn on zone re-entry");
    }

    [Fact]
    public void SaveLoad_PreservesQuestState()
    {
        // Accept quest 1
        AcceptRatHunt();

        // Set some quest progress
        GameQueryHelpers.SetWorldCounter(_fixture.Context, "rats_killed", 2);

        // Verify quest is active before save
        var questsBefore = GameQueryHelpers.GetActiveQuests(_fixture.Context);
        Assert.Contains(questsBefore.Quests, q => q.Id == "q_rat_hunt" && q.Status == "Active");

        // Save
        GameQueryHelpers.TriggerSave(_fixture.Context);

        // Verify save exists
        var saveExists = GameQueryHelpers.GetSaveExists(_fixture.Context);
        Assert.True(saveExists.Exists);

        // Load (restores quest state)
        GameQueryHelpers.TriggerLoad(_fixture.Context);

        // Verify quest state preserved
        var questsAfter = GameQueryHelpers.GetActiveQuests(_fixture.Context);
        Assert.Contains(questsAfter.Quests, q => q.Id == "q_rat_hunt" && q.Status == "Active");

        // Verify counter preserved
        var counter = GameQueryHelpers.GetWorldCounter(_fixture.Context, "rats_killed");
        Assert.Equal(2, counter.Value);
    }

    [Fact]
    public void FullQuestChain_E2E()
    {
        // === Quest 1: Rat Hunt ===
        AcceptRatHunt();

        // Transition to wasteland and simulate 3 rat kills
        TransitionToWasteland();
        GameQueryHelpers.SetWorldCounter(_fixture.Context, "rats_killed", 3);
        Thread.Sleep(200); // Quest evaluator advances stage

        // Verify rats_cleared flag
        var ratsCleared = GameQueryHelpers.GetWorldFlag(_fixture.Context, "rats_cleared");
        Assert.True(ratsCleared.Value);

        // Return to town to report
        TransitionToTown();

        // Report to Elder
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var turnInIdx = dialogue.Choices.FindIndex(c => c.Text == "The rats are dead." && c.Available);
        Assert.True(turnInIdx >= 0, "Expected 'The rats are dead.' choice");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, turnInIdx);

        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var thanksIdx = dialogue.Choices.FindIndex(c => c.Text == "Thanks.");
        if (thanksIdx >= 0)
            GameQueryHelpers.SelectDialogueChoice(_fixture.Context, thanksIdx);
        Thread.Sleep(200);

        var quests = GameQueryHelpers.GetActiveQuests(_fixture.Context);
        var q1 = quests.Quests.FirstOrDefault(q => q.Id == "q_rat_hunt");
        Assert.NotNull(q1);
        Assert.Equal("Completed", q1.Status);

        // === Quest 2: Raider Camp ===
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var jobIdx = dialogue.Choices.FindIndex(c => c.Text == "Got another job?" && c.Available);
        Assert.True(jobIdx >= 0, "Expected 'Got another job?' after Q1 complete");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, jobIdx);

        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var acceptQ2Idx = dialogue.Choices.FindIndex(c => c.Text == "Consider it done.");
        Assert.True(acceptQ2Idx >= 0);
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, acceptQ2Idx);

        // Go to wasteland and kill Scar
        TransitionToWasteland();

        var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
        var scar = combat.Enemies.FirstOrDefault(e => e.Id.Contains("scar"));
        Assert.NotNull(scar);
        GameQueryHelpers.KillEnemy(_fixture.Context, scar.Id);
        Thread.Sleep(200);

        // Verify quest 2 completed and quest 3 auto-started
        quests = GameQueryHelpers.GetActiveQuests(_fixture.Context);
        var q2 = quests.Quests.FirstOrDefault(q => q.Id == "q_raider_camp");
        Assert.NotNull(q2);
        Assert.Equal("Completed", q2.Status);

        var q3 = quests.Quests.FirstOrDefault(q => q.Id == "q_safe_passage");
        Assert.NotNull(q3);
        Assert.Equal("Active", q3.Status);

        // === Quest 3: Safe Passage ===
        TransitionToTown();

        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var reportIdx = dialogue.Choices.FindIndex(c => c.Text == "The camp is clear." && c.Available);
        Assert.True(reportIdx >= 0, "Expected 'The camp is clear.' choice for Q3");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, reportIdx);

        // Complete dialogue if needed
        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        if (dialogue.Active)
        {
            var closeIdx = dialogue.Choices.FindIndex(c => c.Available);
            if (closeIdx >= 0)
                GameQueryHelpers.SelectDialogueChoice(_fixture.Context, closeIdx);
        }
        Thread.Sleep(200);

        // Verify all quests complete
        quests = GameQueryHelpers.GetActiveQuests(_fixture.Context);
        q3 = quests.Quests.FirstOrDefault(q => q.Id == "q_safe_passage");
        Assert.NotNull(q3);
        Assert.Equal("Completed", q3.Status);

        // Verify m1_complete flag
        var m1Flag = GameQueryHelpers.GetWorldFlag(_fixture.Context, "m1_complete");
        Assert.True(m1Flag.Value, "m1_complete flag should be set after all quests done");
    }

    // --- Helpers ---

    private void AcceptRatHunt()
    {
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);

        var anyWorkIdx = dialogue.Choices.FindIndex(c => c.Text == "Any work?" && c.Available);
        Assert.True(anyWorkIdx >= 0, "Expected 'Any work?' choice");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, anyWorkIdx);

        dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var acceptIdx = dialogue.Choices.FindIndex(c => c.Text == "I'll handle it.");
        Assert.True(acceptIdx >= 0, "Expected 'I'll handle it.' choice");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, acceptIdx);
    }

    private void TransitionToWasteland()
    {
        var zone = GameQueryHelpers.GetCurrentZone(_fixture.Context);
        if (zone.ZoneId == "wasteland") return;

        // Town gate at (14.5, 0.5, 2.0) triggers wasteland transition
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 14.5, 0.5, 2.0);
        Thread.Sleep(500);
    }

    private void TransitionToTown()
    {
        var zone = GameQueryHelpers.GetCurrentZone(_fixture.Context);
        if (zone.ZoneId == "town") return;

        // Wasteland gate at (-15.5, 0.5, 1.5) triggers town transition
        GameQueryHelpers.TeleportPlayer(_fixture.Context, -15.5, 0.5, 1.5);
        Thread.Sleep(500);
    }
}
