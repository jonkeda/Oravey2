using Brinell.Stride.Context;
using Brinell.Stride.Testing;
using Oravey2.Core.Automation;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Test fixture that launches Oravey2 with the wasteland scenario.
/// </summary>
public class WastelandTestFixture : StrideTestFixtureBase
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
        options.GameArguments = ["--automation", "--scenario", "wasteland"];
        options.StartupTimeoutMs = 30000;
        options.ConnectionTimeoutMs = 15000;
        options.DefaultTimeoutMs = 5000;
        return options;
    }
}

/// <summary>
/// Tests for the wasteland scenario: enemy spawning, positions, zone transition back to town.
/// </summary>
public class WastelandTests : IAsyncLifetime
{
    private readonly WastelandTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void Wasteland_GateReturnsToTown()
    {
        // Wasteland west gate exit trigger at (-15.5, 0.5, 1.5) with radius 1.5
        GameQueryHelpers.TeleportPlayer(_fixture.Context, -15.5, 0.5, 1.5);

        // Allow game frames to tick so ZoneExitTriggerScript.Update() processes
        Thread.Sleep(500);

        var zone = GameQueryHelpers.GetCurrentZone(_fixture.Context);
        Assert.Equal("town", zone.ZoneId);
        Assert.Equal("Haven", zone.ZoneName);
    }
}
