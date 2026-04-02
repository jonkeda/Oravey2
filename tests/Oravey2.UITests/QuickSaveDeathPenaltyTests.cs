using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests;

/// <summary>
/// Tests for QuickSave (F5), QuickLoad (F9), auto-save, and death penalty.
/// Game starts in Exploring (automation mode).
/// </summary>
public class QuickSaveDeathPenaltyTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public QuickSaveDeathPenaltyTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void QuickSave_CreatesFile()
    {
        // Press F5 for quick save
        _fixture.Context.PressKey(VirtualKey.F5);

        var exists = GameQueryHelpers.GetSaveExists(_fixture.Context);
        _output.WriteLine($"Save exists after F5: {exists.Exists}");
        Assert.True(exists.Exists);
    }

    [Fact]
    public void QuickSaveLoad_RestoresPosition()
    {
        // Teleport to known position and quick save
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 3.0, 0.5, -2.0);
        _fixture.Context.PressKey(VirtualKey.F5);

        // Move away
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 0, 0.5, 0);

        // Quick load (F9)
        _fixture.Context.PressKey(VirtualKey.F9);

        // Verify position restored
        var pos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _output.WriteLine($"After F9: x={pos.X:F1}, z={pos.Z:F1}");
        Assert.Equal(3.0, pos.X, 0.5);
        Assert.Equal(-2.0, pos.Z, 0.5);
    }

    [Fact]
    public void DeathPenalty_LosesCaps()
    {
        // Verify starting caps
        var before = GameQueryHelpers.GetCapsState(_fixture.Context);
        _output.WriteLine($"Caps before death: {before.Caps}");
        Assert.Equal(50, before.Caps);

        // Kill the player (deal massive damage)
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        GameQueryHelpers.DamagePlayer(_fixture.Context, hud.MaxHp + 10);

        // Game should be in GameOver state
        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        _output.WriteLine($"State after death: {state}");
        Assert.Equal("GameOver", state);

        // Caps should have lost 10%: 50 -> 45
        var after = GameQueryHelpers.GetCapsState(_fixture.Context);
        _output.WriteLine($"Caps after death: {after.Caps}");
        Assert.Equal(45, after.Caps);
    }

    [Fact]
    public void SaveLoad_PreservesCaps()
    {
        // Save with default 50 caps
        GameQueryHelpers.TriggerSave(_fixture.Context);

        // Verify round-trip via TriggerLoad
        GameQueryHelpers.TriggerLoad(_fixture.Context);

        var caps = GameQueryHelpers.GetCapsState(_fixture.Context);
        _output.WriteLine($"Caps after load: {caps.Caps}");
        Assert.Equal(50, caps.Caps);
    }
}
