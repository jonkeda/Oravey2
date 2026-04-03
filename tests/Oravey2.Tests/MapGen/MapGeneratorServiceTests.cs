using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Services;
using Oravey2.MapGen.Validation;

namespace Oravey2.Tests.MapGen;

public class MapGeneratorServiceTests
{
    [Fact]
    public async Task Service_CreatesAndDisposes_NoError()
    {
        var assets = new AssetRegistry(new Dictionary<string, List<AssetEntry>>());
        var validator = new TerrainBlueprintValidator(assets);

        await using var service = new MapGeneratorService(assets, validator);
        // No exception = pass
    }

    [Fact]
    public async Task Generate_WithoutCli_ReturnsError()
    {
        var assets = new AssetRegistry(new Dictionary<string, List<AssetEntry>>());
        var validator = new TerrainBlueprintValidator(assets);

        await using var service = new MapGeneratorService(assets, validator);
        service.CliPath = "nonexistent-copilot-cli-path";

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
