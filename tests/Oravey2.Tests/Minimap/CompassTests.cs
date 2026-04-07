using Oravey2.Core.UI;

namespace Oravey2.Tests.Minimap;

public class CompassTests
{
    private readonly CompassModel _compass = new(180f);

    [Fact]
    public void FacingNorth_NorthCentred()
    {
        var dirs = _compass.GetVisibleDirections(0f);
        var north = dirs.FirstOrDefault(d => d.Label == "N");
        Assert.Equal("N", north.Label);
        Assert.Equal(0f, north.NormalisedPosition, 0.01f);
    }

    [Fact]
    public void FacingEast_EastCentred()
    {
        var dirs = _compass.GetVisibleDirections(90f);
        var east = dirs.FirstOrDefault(d => d.Label == "E");
        Assert.Equal("E", east.Label);
        Assert.Equal(0f, east.NormalisedPosition, 0.01f);
    }

    [Fact]
    public void POIBearing_East_ShowsRight()
    {
        // Camera facing north (0°), POI to the east (90°)
        float? pos = _compass.GetPoiBearing(0f, 90f);
        Assert.NotNull(pos);
        Assert.True(pos > 0f, "POI to the east should be right of centre (positive)");
        Assert.Equal(1f, pos.Value, 0.01f); // 90° / 90° (half-strip) = 1.0
    }

    [Fact]
    public void POIBearing_West_ShowsLeft()
    {
        // Camera facing north (0°), POI to the west (270°)
        float? pos = _compass.GetPoiBearing(0f, 270f);
        Assert.NotNull(pos);
        Assert.True(pos < 0f, "POI to the west should be left of centre (negative)");
        Assert.Equal(-1f, pos.Value, 0.01f);
    }

    [Fact]
    public void POIBearing_Behind_ReturnsNull()
    {
        // Camera facing north (0°), POI directly south (180°) — outside 180° strip
        // 180° delta at exactly the boundary — implementation uses > not >=, let's test beyond
        float? pos = _compass.GetPoiBearing(0f, 181f);
        Assert.Null(pos);
    }

    [Fact]
    public void FacingSouth_SouthCentred()
    {
        var dirs = _compass.GetVisibleDirections(180f);
        var south = dirs.FirstOrDefault(d => d.Label == "S");
        Assert.Equal("S", south.Label);
        Assert.Equal(0f, south.NormalisedPosition, 0.01f);
    }

    [Fact]
    public void FacingNorth_EastAndWestVisible()
    {
        var dirs = _compass.GetVisibleDirections(0f);
        var labels = dirs.Select(d => d.Label).ToHashSet();
        Assert.Contains("E", labels);
        Assert.Contains("W", labels);
        Assert.Contains("NE", labels);
        Assert.Contains("NW", labels);
    }

    [Fact]
    public void ComputeBearing_NorthToEast_Is90()
    {
        float bearing = CompassModel.ComputeBearing(0, 0, 10, 0);
        Assert.Equal(90f, bearing, 1f);
    }

    [Fact]
    public void ComputeBearing_NorthToSouth_Is180()
    {
        // +Z = south in Stride
        float bearing = CompassModel.ComputeBearing(0, 0, 0, 10);
        Assert.Equal(180f, bearing, 1f);
    }

    [Fact]
    public void HudStatusBar_FormattedTime()
    {
        var bar = new HudStatusBar();
        bar.Update("Wasteland", 10, 20, 14.5f, 3, "clear", 25, 0.1f);
        Assert.Equal("14:30", bar.FormattedTime);
    }

    [Fact]
    public void HudStatusBar_AutoHide_VisibleOnChange()
    {
        var bar = new HudStatusBar();
        bar.Update("Wasteland", 10, 20, 12f, 1, "clear", 20, 0f);
        Assert.True(bar.IsVisible);
    }

    [Fact]
    public void HudStatusBar_AutoHide_HidesAfterDelay()
    {
        var bar = new HudStatusBar();
        bar.Update("Wasteland", 10, 20, 12f, 1, "clear", 20, 0f);
        Assert.True(bar.IsVisible);

        // Simulate time passing with no changes
        bar.Update("Wasteland", 10, 20, 12f, 1, "clear", 20, 6f);
        Assert.False(bar.IsVisible);
    }

    [Fact]
    public void HudStatusBar_FormattedCoordinates()
    {
        var bar = new HudStatusBar();
        bar.Update("Zone A", 42, 17, 8f, 2, "rain", 15, 0f);
        Assert.Equal("(42, 17)", bar.FormattedCoordinates);
    }
}
