using Brinell.Stride.Infrastructure;
using Oravey2.Core.Automation;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests;

/// <summary>
/// Tests for M1 Phase 4: Death → respawn flow via DeathRespawnScript.
/// Uses TownTestFixture (town scenario has DeathRespawnScript wired).
/// Tests verify the full live-game death→overlay→respawn→town sequence.
/// </summary>
public class DeathRespawnUITests : IAsyncLifetime
{
    private readonly TownTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public DeathRespawnUITests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void PlayerDeath_ShowsYouDied()
    {
        // Force player death
        GameQueryHelpers.ForcePlayerDeath(_fixture.Context);
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var overlay = GameQueryHelpers.GetGameOverState(_fixture.Context);
        _output.WriteLine($"Overlay visible={overlay.Visible}, title={overlay.Title}");
        Assert.True(overlay.Visible);
        Assert.Equal("YOU DIED", overlay.Title);

        var death = GameQueryHelpers.GetDeathState(_fixture.Context);
        Assert.True(death.IsDead);
    }

    [Fact]
    public void PlayerDeath_RespawnsAfterDelay()
    {
        // Record starting zone
        var zoneBefore = GameQueryHelpers.GetCurrentZone(_fixture.Context);
        _output.WriteLine($"Zone before: {zoneBefore.ZoneId}");

        // Force player death
        GameQueryHelpers.ForcePlayerDeath(_fixture.Context);

        // Wait for respawn sequence (3s + margin)
        _fixture.Context.HoldKey(VirtualKey.Space, 4000);

        // Should be back in Exploring after respawn
        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        _output.WriteLine($"State after respawn: {state}");
        Assert.Equal("Exploring", state);
    }

    [Fact]
    public void PlayerDeath_CapsReduced()
    {
        // Verify starting caps
        var before = GameQueryHelpers.GetCapsState(_fixture.Context);
        _output.WriteLine($"Caps before: {before.Caps}");
        Assert.Equal(50, before.Caps);

        // Force death and wait for full respawn
        GameQueryHelpers.ForcePlayerDeath(_fixture.Context);
        _fixture.Context.HoldKey(VirtualKey.Space, 4000);

        // Caps should be reduced by 10%: 50 → 45
        var after = GameQueryHelpers.GetCapsState(_fixture.Context);
        _output.WriteLine($"Caps after: {after.Caps}");
        Assert.Equal(45, after.Caps);
    }

    [Fact]
    public void PlayerDeath_HpFull_AfterRespawn()
    {
        // Force death and wait for respawn
        GameQueryHelpers.ForcePlayerDeath(_fixture.Context);
        _fixture.Context.HoldKey(VirtualKey.Space, 4000);

        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        _output.WriteLine($"HP after respawn: {hud.Hp}/{hud.MaxHp}");
        Assert.Equal(hud.MaxHp, hud.Hp);
    }

    [Fact]
    public void PlayerDeath_InTownZone()
    {
        // Transition to wasteland first
        TransitionToWasteland();
        var zoneW = GameQueryHelpers.GetCurrentZone(_fixture.Context);
        Assert.Equal("wasteland", zoneW.ZoneId);

        // Force death and wait for respawn
        GameQueryHelpers.ForcePlayerDeath(_fixture.Context);
        _fixture.Context.HoldKey(VirtualKey.Space, 4000);

        // Should respawn in town
        var zone = GameQueryHelpers.GetCurrentZone(_fixture.Context);
        _output.WriteLine($"Zone after respawn: {zone.ZoneId}");
        Assert.Equal("town", zone.ZoneId);
    }

    [Fact]
    public void PlayerDeath_CombatReset()
    {
        // Force death and wait for respawn
        GameQueryHelpers.ForcePlayerDeath(_fixture.Context);
        _fixture.Context.HoldKey(VirtualKey.Space, 4000);

        // After respawn in town, game state should be Exploring (not InCombat)
        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        _output.WriteLine($"State after respawn: {state}");
        Assert.Equal("Exploring", state);
    }

    [Fact]
    public void PlayerDeath_QuestPreserved()
    {
        // Accept a quest first
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        var anyWorkIdx = dialogue.Choices.FindIndex(c => c.Text == "Any work?" && c.Available);
        if (anyWorkIdx >= 0)
        {
            GameQueryHelpers.SelectDialogueChoice(_fixture.Context, anyWorkIdx);
            dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
            var acceptIdx = dialogue.Choices.FindIndex(c => c.Text == "I'll handle it.");
            if (acceptIdx >= 0)
                GameQueryHelpers.SelectDialogueChoice(_fixture.Context, acceptIdx);
        }

        var questsBefore = GameQueryHelpers.GetActiveQuests(_fixture.Context);
        var q1Before = questsBefore.Quests.FirstOrDefault(q => q.Id == "q_rat_hunt");
        _output.WriteLine($"Quest before death: {q1Before?.Status ?? "null"}");

        // Die and respawn
        GameQueryHelpers.ForcePlayerDeath(_fixture.Context);
        _fixture.Context.HoldKey(VirtualKey.Space, 4000);

        // Quest should still be active
        var questsAfter = GameQueryHelpers.GetActiveQuests(_fixture.Context);
        var q1After = questsAfter.Quests.FirstOrDefault(q => q.Id == "q_rat_hunt");
        _output.WriteLine($"Quest after respawn: {q1After?.Status ?? "null"}");
        Assert.NotNull(q1After);
        Assert.Equal("Active", q1After.Status);
    }

    [Fact]
    public void PlayerDeath_InventoryPreserved()
    {
        // Give player an item
        GameQueryHelpers.GiveItemToPlayer(_fixture.Context, "medkit", 2);
        var invBefore = GameQueryHelpers.GetInventoryState(_fixture.Context);
        var countBefore = invBefore.Items.Count;
        _output.WriteLine($"Items before death: {countBefore}");

        // Die and respawn
        GameQueryHelpers.ForcePlayerDeath(_fixture.Context);
        _fixture.Context.HoldKey(VirtualKey.Space, 4000);

        // Inventory should be preserved
        var invAfter = GameQueryHelpers.GetInventoryState(_fixture.Context);
        _output.WriteLine($"Items after respawn: {invAfter.Items.Count}");
        Assert.Equal(countBefore, invAfter.Items.Count);
    }

    // --- Helpers ---

    private void TransitionToWasteland()
    {
        var zone = GameQueryHelpers.GetCurrentZone(_fixture.Context);
        if (zone.ZoneId == "wasteland") return;

        GameQueryHelpers.TeleportPlayer(_fixture.Context, 14.5, 0.5, 2.0);
        Thread.Sleep(500);
    }
}
