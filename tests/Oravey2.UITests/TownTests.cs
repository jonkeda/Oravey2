using Brinell.Stride.Context;
using Brinell.Stride.Testing;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Test fixture that launches Oravey2 with the town scenario.
/// </summary>
public class TownTestFixture : StrideTestFixtureBase
{
    protected override string GetDefaultAppPath()
    {
        var solutionDir = FindSolutionDirectory();
        return Path.Combine(solutionDir,
            "src", "Oravey2.Windows", "bin", "Debug", "net10.0", "Oravey2.Windows.exe");
    }

    protected override StrideTestContextOptions CreateOptions()
    {
        var options = base.CreateOptions();
        options.GameArguments = ["--automation", "--scenario", "town"];
        options.StartupTimeoutMs = 30000;
        options.ConnectionTimeoutMs = 15000;
        options.DefaultTimeoutMs = 5000;
        return options;
    }
}

/// <summary>
/// Tests for the town scenario: NPC spawning, positions, automation queries.
/// </summary>
public class TownTests : IAsyncLifetime
{
    private readonly TownTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    [Trait("Category", "Smoke")]
    public void Town_LoadsSuccessfully()
    {
        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", state);
    }

    [Fact]
    public void Town_HasFourNpcs()
    {
        var npcs = GameQueryHelpers.GetNpcList(_fixture.Context);
        Assert.Equal(4, npcs.Count);
    }

    [Fact]
    public void Town_ElderExists_AtCorrectPosition()
    {
        var npcs = GameQueryHelpers.GetNpcList(_fixture.Context);
        var elder = npcs.Npcs.First(n => n.Id == "elder");

        Assert.Equal("Elder Tomas", elder.DisplayName);
        Assert.Equal("QuestGiver", elder.Role);
        Assert.Equal(-4.0, elder.X, 0.5);
        Assert.Equal(0.5, elder.Y, 0.5);
        Assert.Equal(-4.5, elder.Z, 0.5);
    }

    [Fact]
    public void Town_TeleportToElder_ShowsInRange()
    {
        // Elder is at (-4, 0.5, -4.5) — teleport player nearby
        GameQueryHelpers.TeleportPlayer(_fixture.Context, -3.5, 0.5, -4.5);
        var result = GameQueryHelpers.GetNpcInRange(_fixture.Context);

        Assert.True(result.InRange);
        Assert.Equal("elder", result.NpcId);
    }

    [Fact]
    public void Town_FarFromNpc_NotInRange()
    {
        // Player at spawn (0, 0.5, 0) — no NPC within 2 units
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 0, 0.5, 0);
        var result = GameQueryHelpers.GetNpcInRange(_fixture.Context);

        Assert.False(result.InRange);
    }

    [Fact]
    public void Town_InteractWithElder_FiresEvent()
    {
        var result = GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");

        Assert.True(result.Success);
        Assert.Equal("elder", result.NpcId);
        Assert.Equal("elder_dialogue", result.DialogueTreeId);
    }

    [Fact]
    public void Town_InteractWithElder_OpensDialogue()
    {
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var state = GameQueryHelpers.GetGameState(_fixture.Context);

        Assert.Equal("InDialogue", state);
    }

    [Fact]
    public void Town_DialogueShows_SpeakerAndText()
    {
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);

        Assert.True(dialogue.Active);
        Assert.Equal("Elder Tomas", dialogue.Speaker);
        Assert.Contains("Elder Tomas", dialogue.Text!);
    }

    [Fact]
    public void Town_DialogueChoices_ArePresent()
    {
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);

        Assert.True(dialogue.Choices.Count >= 2);
        Assert.Contains(dialogue.Choices, c => c.Text.Contains("going on"));
        Assert.Contains(dialogue.Choices, c => c.Text.Contains("Goodbye"));
    }

    [Fact]
    public void Town_SelectLeave_EndsDialogue()
    {
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "elder");
        // "Goodbye" is the second choice (index 1)
        var result = GameQueryHelpers.SelectDialogueChoice(_fixture.Context, 1);

        Assert.True(result.Success);
        Assert.True(result.DialogueEnded);

        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", state);
    }

    [Fact]
    public void Town_CivilianDialogue_Works()
    {
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "civilian_1");
        var dialogue = GameQueryHelpers.GetDialogueState(_fixture.Context);
        Assert.True(dialogue.Active);
        Assert.Equal("Settler", dialogue.Speaker);

        // Select "Thanks" (index 0) → ends dialogue
        var result = GameQueryHelpers.SelectDialogueChoice(_fixture.Context, 0);
        Assert.True(result.DialogueEnded);

        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", state);
    }

    // --- Sub-Phase 2.5: Merchant Buy/Sell ---

    [Fact]
    public void Town_BuyMedkit_DeductsCaps()
    {
        var capsBefore = GameQueryHelpers.GetCapsState(_fixture.Context);
        Assert.Equal(50, capsBefore.Caps);

        // Interact with merchant, choose "Buy Medkit (10 caps)" (index 0)
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "merchant");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, 0);

        var capsAfter = GameQueryHelpers.GetCapsState(_fixture.Context);
        Assert.Equal(40, capsAfter.Caps);
    }

    [Fact]
    public void Town_BuyMedkit_AddsToInventory()
    {
        // Still in merchant dialogue from previous test (looping tree)
        // Or re-interact — let's check state first
        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        if (state == "InDialogue")
        {
            // Leave dialogue first (choice 3 = "Never mind")
            GameQueryHelpers.SelectDialogueChoice(_fixture.Context, 3);
        }

        GameQueryHelpers.InteractWithNpc(_fixture.Context, "merchant");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, 0); // Buy Medkit

        // Leave dialogue to check inventory
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, 3); // "Never mind"

        var inventory = GameQueryHelpers.GetInventoryState(_fixture.Context);
        Assert.Contains(inventory.Items, i => i.Id == "medkit");
    }

    [Fact]
    public void Town_SellScrap_AddsCaps()
    {
        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        if (state == "InDialogue")
        {
            GameQueryHelpers.SelectDialogueChoice(_fixture.Context, 3);
        }

        // Give player scrap metal via automation
        GameQueryHelpers.GiveItemToPlayer(_fixture.Context, "scrap_metal", 1);

        var capsBefore = GameQueryHelpers.GetCapsState(_fixture.Context);

        // Interact with merchant, choose "Sell Scrap Metal (5 caps)" (index 2)
        GameQueryHelpers.InteractWithNpc(_fixture.Context, "merchant");
        GameQueryHelpers.SelectDialogueChoice(_fixture.Context, 2);

        var capsAfter = GameQueryHelpers.GetCapsState(_fixture.Context);
        Assert.Equal(capsBefore.Caps + 5, capsAfter.Caps);
    }
}
