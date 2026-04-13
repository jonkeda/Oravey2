using Brinell.Maui.Testing;
using Oravey2.UITests.MapGen.Pages;
using Xunit;

namespace Oravey2.UITests.MapGen;

/// <summary>
/// UI tests for the Layout step spatial specification visualization.
/// Tests the display of grid information, spatial spec stats, and visualization controls.
/// </summary>
[Collection("MapGen")]
public class LayoutStepSpatialSpecTests
{
    private readonly MapGenTestFixture _fixture;
    private readonly LayoutStepPage _page;

    public LayoutStepSpatialSpecTests(MapGenTestFixture fixture)
    {
        _fixture = fixture;
        _page = new LayoutStepPage(fixture.Context);
    }

    [Fact]
    public void LayoutStep_StatusText_IsPresent()
    {
        _page.WaitIdle();
        _page.StatusText.AssertExists();
    }

    [Fact]
    public void LayoutStep_GridDimensions_IsPresent()
    {
        _page.WaitIdle();
        _page.GridDimensions.AssertExists();
    }

    [Fact]
    public void LayoutStep_BuildingCount_IsPresent()
    {
        _page.WaitIdle();
        _page.BuildingCount.AssertExists();
    }

    [Fact]
    public void LayoutStep_RoadLength_IsPresent()
    {
        _page.WaitIdle();
        _page.RoadLength.AssertExists();
    }

    [Fact]
    public void LayoutStep_WaterArea_IsPresent()
    {
        _page.WaitIdle();
        _page.WaterArea.AssertExists();
    }

    [Fact]
    public void LayoutStep_ZoomLevel_IsPresent()
    {
        _page.WaitIdle();
        _page.ZoomLevel.AssertExists();
    }

    [Fact]
    public void LayoutStep_ResetViewButton_IsPresent()
    {
        _page.WaitIdle();
        _page.ResetViewButton.AssertExists();
    }

    [Fact]
    public void LayoutStep_FitToScreenButton_IsPresent()
    {
        _page.WaitIdle();
        _page.FitToScreenButton.AssertExists();
    }

    [Fact]
    public void LayoutStep_GridDimensions_DisplaysText()
    {
        _page.WaitIdle();
        var text = _page.GridDimensions.GetText();
        // Should not be empty
        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public void LayoutStep_BuildingCount_DisplaysText()
    {
        _page.WaitIdle();
        var text = _page.BuildingCount.GetText();
        // Should not be empty
        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }
}

