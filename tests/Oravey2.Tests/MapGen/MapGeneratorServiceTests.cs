using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Services;

namespace Oravey2.Tests.MapGen;

public class MapGeneratorServiceTests
{
    [Fact]
    public async Task Service_CreatesAndDisposes_NoError()
    {
        var assets = new AssetRegistry(new Dictionary<string, List<AssetEntry>>());

        await using var service = new MapGeneratorService(assets);
        // No exception = pass
    }

    [Fact]
    public async Task Generate_ReturnsNotImplementedError()
    {
        var assets = new AssetRegistry(new Dictionary<string, List<AssetEntry>>());

        await using var service = new MapGeneratorService(assets);

        var result = await service.GenerateAsync(new Oravey2.MapGen.Models.MapGenerationRequest
        {
            LocationName = "Test",
            GeographyDescription = "Test",
            PostApocContext = "Test",
            ChunksWide = 1,
            ChunksHigh = 1,
            MinLevel = 1,
            MaxLevel = 1,
            DifficultyDescription = "Easy",
            Factions = Array.Empty<string>()
        });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
}
