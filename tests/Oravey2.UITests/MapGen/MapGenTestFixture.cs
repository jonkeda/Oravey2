using Brinell.Maui.Context;
using Brinell.Maui.Testing;
using Xunit;

namespace Oravey2.UITests.MapGen;

/// <summary>
/// Shared fixture that launches the MapGen MAUI app with the Noord-Holland
/// pipeline state pre-loaded at step 8 (Assembly).
/// </summary>
public class MapGenTestFixture : MauiTestFixtureBase
{
    protected override MauiTestContextOptions CreateTestContextOptions()
    {
        var solutionDir = FindSolutionDirectory();

        // Point the app at the repo's data/ folder so PipelineStateService
        // finds data/regions/noord-holland/pipeline-state.json.
        Environment.SetEnvironmentVariable("MAPGEN_DATA_ROOT",
            Path.Combine(solutionDir, "data"));

        // Tell the app to auto-load the noord-holland pipeline at step 8.
        // This also makes App.xaml.cs use PipelineWizardView directly (no TabbedPage),
        // because FlaUI cannot traverse TabbedPage content on WinUI3.
        Environment.SetEnvironmentVariable("MAPGEN_AUTO_LOAD_REGION",
            "noord-holland");

        return base.CreateTestContextOptions();
    }

    protected override string GetDefaultAppPath(string platform)
    {
        var solutionDir = FindSolutionDirectory();
        return Path.Combine(solutionDir,
            "src", "Oravey2.MapGen.App", "bin", "Debug",
            "net10.0-windows10.0.19041.0", "win-x64",
            "Oravey2.MapGen.App.exe");
    }
}

[CollectionDefinition("MapGen")]
public class MapGenCollection : ICollectionFixture<MapGenTestFixture> { }
