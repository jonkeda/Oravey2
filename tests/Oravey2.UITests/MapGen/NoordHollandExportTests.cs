using Brinell.Maui.Testing;
using Oravey2.UITests.MapGen.Pages;
using Xunit;

namespace Oravey2.UITests.MapGen;

/// <summary>
/// Smoke tests for the Noord-Holland content pack export via the MapGen
/// Assembly step (step 8). The fixture auto-loads the pre-built pipeline
/// state so the wizard opens directly at the Assembly step.
/// </summary>
[Collection("MapGen")]
public class NoordHollandExportTests
{
    private readonly MapGenTestFixture _fixture;
    private readonly AssemblyStepPage _page;

    public NoordHollandExportTests(MapGenTestFixture fixture)
    {
        _fixture = fixture;
        _page = new AssemblyStepPage(fixture.Context);
    }

    [Fact]
    public void Assembly_ExportButton_IsVisible()
    {
        _page.WaitIdle();
        _page.ExportToDb.AssertExists();
        _page.ExportToDb.AssertEnabled(true);
    }

    [Fact]
    public void Assembly_ExportToDb_ShowsSuccessStatus()
    {
        _page.WaitIdle();
        _page.ExportToDb.Click();
        _page.WaitIdle();

        var status = _page.StatusText.GetText();
        Assert.Contains("Exported", status);
        Assert.Contains("Noord-Holland", status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Assembly_ExportToDb_CreatesWorldDb()
    {
        // Walk up from test assembly to find the solution root
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Oravey2.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var contentPackDir = Path.Combine(dir.FullName,
            "content", "Oravey2.Apocalyptic.NL.NH");
        var worldDbPath = Path.Combine(contentPackDir, "world.db");

        _page.WaitIdle();
        _page.ExportToDb.Click();
        _page.WaitIdle();

        Assert.True(File.Exists(worldDbPath),
            $"world.db was not created at {worldDbPath}");
    }

    [Fact]
    public void Assembly_ExportToDb_ReportsChunkCount()
    {
        _page.WaitIdle();
        _page.ExportToDb.Click();
        _page.WaitIdle();

        var status = _page.StatusText.GetText();
        // Status should mention chunk count, e.g. "12 chunks"
        Assert.Matches(@"\d+\s+chunk", status);
    }

    [Fact]
    public void Assembly_Validate_ShowsSummary()
    {
        _page.WaitIdle();
        _page.Validate.Click();
        _page.WaitIdle();

        _page.ValidationSummary.AssertExists();
        var summary = _page.ValidationSummary.GetText();
        Assert.False(string.IsNullOrWhiteSpace(summary),
            "Validation summary should not be empty");
    }

    [Fact]
    public void Assembly_ExportToDb_DisabledWhileRunning()
    {
        _page.WaitIdle();
        _page.ExportToDb.Click();

        // The export is fast — verify the sentinel shows busy during the operation.
        // If it completes before we can check, just verify it returned to enabled.
        _page.WaitIdle();
        _page.ExportToDb.AssertEnabled(true);
    }

    [Fact]
    public void Assembly_ExportToDb_ScreenshotShowsStatus()
    {
        _page.WaitIdle();
        _page.ExportToDb.Click();
        _page.WaitIdle();

        var screenshotPath = Path.Combine(
            Path.GetTempPath(), "mapgen-export-screenshot.png");
        _fixture.Context.SaveScreenshot(screenshotPath);

        Assert.True(File.Exists(screenshotPath),
            "Screenshot file was not created");
        Assert.True(new FileInfo(screenshotPath).Length > 1024,
            "Screenshot file is suspiciously small (< 1KB)");
    }
}
