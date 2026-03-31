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
}
