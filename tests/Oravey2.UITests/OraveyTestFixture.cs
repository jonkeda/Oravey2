using Brinell.Stride.Context;
using Brinell.Stride.Testing;

namespace Oravey2.UITests;

/// <summary>
/// Test fixture that launches Oravey2 with automation enabled
/// and provides a connected StrideTestContext.
/// </summary>
public class OraveyTestFixture : StrideTestFixtureBase
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
        options.GameArguments = ["--automation"];
        options.StartupTimeoutMs = 30000;
        options.ConnectionTimeoutMs = 15000;
        options.DefaultTimeoutMs = 5000;
        return options;
    }
}
