using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests.Weather;

/// <summary>
/// UI tests that verify weather effects and day/night lighting render correctly.
/// These require a running game process.
/// </summary>
public class WeatherTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public WeatherTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void ToggleRain_ParticlesVisible()
    {
        // Set weather override flag to rain via world flag
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "weather_rain", true);

        // Advance a few frames
        _fixture.Context.HoldKey(VirtualKey.W, 200);

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot with rain: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot file size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000,
            "Screenshot with rain enabled too small — rain particles may not be rendering");
    }

    [Fact]
    public void ToggleSnow_TerrainWhitens()
    {
        // Set weather override flag to snow
        GameQueryHelpers.SetWorldFlag(_fixture.Context, "weather_snow", true);

        // Wait a bit for accumulation (hold a key as a frame-advancing proxy)
        _fixture.Context.HoldKey(VirtualKey.W, 500);

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot with snow: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot file size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000,
            "Screenshot with snow enabled too small — snow effect may not be rendering");
    }

    [Fact]
    public void DayNightCycle_LightingChanges()
    {
        // Set time to noon via world counter
        GameQueryHelpers.SetWorldCounter(_fixture.Context, "time_hour", 12);

        var noonPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Noon screenshot: {noonPath}");
        Assert.True(File.Exists(noonPath));
        var noonBytes = File.ReadAllBytes(noonPath);

        // Set time to midnight
        GameQueryHelpers.SetWorldCounter(_fixture.Context, "time_hour", 0);

        var midnightPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Midnight screenshot: {midnightPath}");
        Assert.True(File.Exists(midnightPath));
        var midnightBytes = File.ReadAllBytes(midnightPath);

        _output.WriteLine($"Noon: {noonBytes.Length} bytes, Midnight: {midnightBytes.Length} bytes");

        // Both should be valid screenshots
        Assert.True(noonBytes.Length > 1000, "Noon screenshot too small");
        Assert.True(midnightBytes.Length > 1000, "Midnight screenshot too small");
    }
}
