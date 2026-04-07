using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests.Descriptions;

/// <summary>
/// UI tests for the location info panel. Verifies the panel appears
/// when a POI description is shown, expands tiers, and dismisses.
/// Requires a running game process.
/// </summary>
public class InfoPanelTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public InfoPanelTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void ClickPOI_InfoPanelAppears()
    {
        // Show the info panel via automation (simulates clicking a POI)
        var result = GameQueryHelpers.ShowInfoPanel(
            _fixture.Context,
            locationId: 1,
            name: "Dustville",
            type: "town",
            tagline: "A sun-bleached settlement on cracked earth.",
            summary: "Dustville was once a thriving community.");

        _output.WriteLine($"Panel visible: {result.Visible}, name: {result.LocationName}");
        Assert.True(result.Visible, "Info panel should be visible after showing");
        Assert.Equal("Dustville", result.LocationName);
        Assert.Equal("A sun-bleached settlement on cracked earth.", result.Tagline);

        // Verify panel state via query
        var state = GameQueryHelpers.GetInfoPanelState(_fixture.Context);
        Assert.True(state.Visible);
        Assert.Equal(1, state.LocationId);
        Assert.Equal("tagline", state.CurrentTier);
    }

    [Fact]
    public void ExpandDescription_ShowsMoreText()
    {
        // Show panel with summary data
        GameQueryHelpers.ShowInfoPanel(
            _fixture.Context,
            locationId: 2,
            name: "Ironhaven",
            type: "town",
            tagline: "Walls of scrap metal surround this refuge.",
            summary: "Ironhaven is a fortified enclave built from salvaged industrial materials.");

        // Expand to summary tier
        var expanded = GameQueryHelpers.ExpandInfoPanel(_fixture.Context, "summary");
        _output.WriteLine($"Expanded to tier: {expanded.CurrentTier}");
        Assert.True(expanded.Success);
        Assert.Equal("summary", expanded.CurrentTier);

        // Verify state shows summary tier
        var state = GameQueryHelpers.GetInfoPanelState(_fixture.Context);
        Assert.Equal("summary", state.CurrentTier);
        Assert.NotNull(state.Summary);
    }

    [Fact]
    public void ClosePanel_Dismisses()
    {
        // Show panel
        GameQueryHelpers.ShowInfoPanel(
            _fixture.Context,
            locationId: 3,
            name: "Checkpoint Alpha",
            type: "checkpoint",
            tagline: "A roadside barricade.");

        // Verify it's visible
        var before = GameQueryHelpers.GetInfoPanelState(_fixture.Context);
        Assert.True(before.Visible);

        // Close panel
        var after = GameQueryHelpers.CloseInfoPanel(_fixture.Context);
        _output.WriteLine($"Panel visible after close: {after.Visible}");
        Assert.False(after.Visible, "Info panel should be hidden after closing");
    }
}
