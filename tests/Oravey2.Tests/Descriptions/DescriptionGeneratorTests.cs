using Oravey2.Core.Descriptions;
using Oravey2.Core.World;

namespace Oravey2.Tests.Descriptions;

public class DescriptionGeneratorTests
{
    private static LocationContext MakeContext(string name = "TestTown", string poiType = "town",
        BiomeType biome = BiomeType.Wasteland) =>
        new(name, poiType, biome, RegionName: "North Wastes", ExistingTagline: "A dusty settlement.");

    [Fact]
    public async Task Generate_WithLlm_ReturnsSummaryMediumFull()
    {
        var llm = new Func<string, CancellationToken, Task<string>>((prompt, ct) =>
            Task.FromResult("A battered town of fifty souls, clinging to the remnants of an old highway junction."));

        var generator = new DescriptionGenerator(llm);
        var context = MakeContext();

        var summary = await generator.GenerateSummaryAsync(context);

        Assert.NotEmpty(summary);
        Assert.True(summary.Length <= 300, $"Summary should be ≤300 chars, got {summary.Length}");
    }

    [Fact]
    public async Task Generate_LlmUnavailable_FallsBackToTemplate()
    {
        var llm = new Func<string, CancellationToken, Task<string>>((prompt, ct) =>
            throw new InvalidOperationException("LLM unavailable"));

        var generator = new DescriptionGenerator(llm);
        var context = MakeContext();

        var summary = await generator.GenerateSummaryAsync(context);

        Assert.NotEmpty(summary);
        Assert.Contains("TestTown", summary); // Template-based fallback includes the name
    }

    [Fact]
    public async Task Generate_LlmBadResponse_RetriesAndSucceeds()
    {
        int callCount = 0;
        var llm = new Func<string, CancellationToken, Task<string>>((prompt, ct) =>
        {
            callCount++;
            if (callCount == 1) return Task.FromResult(""); // Empty response, should retry
            return Task.FromResult("A valid description on the second try.");
        });

        var generator = new DescriptionGenerator(llm);
        var context = MakeContext();

        var summary = await generator.GenerateSummaryAsync(context);

        Assert.Equal("A valid description on the second try.", summary);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task SummaryLength_Under300Chars()
    {
        // LLM returns a very long response — generator should truncate
        var longText = new string('x', 500);
        var llm = new Func<string, CancellationToken, Task<string>>((prompt, ct) =>
            Task.FromResult(longText));

        var generator = new DescriptionGenerator(llm);
        var summary = await generator.GenerateSummaryAsync(MakeContext());

        Assert.True(summary.Length <= 300, $"Summary should be ≤300 chars, got {summary.Length}");
    }

    [Fact]
    public async Task GenerateDossier_WithLlm_ReturnsDossier()
    {
        var llm = new Func<string, CancellationToken, Task<string>>((prompt, ct) =>
            Task.FromResult("A multi-paragraph dossier about the town."));

        var generator = new DescriptionGenerator(llm);
        var dossier = await generator.GenerateDossierAsync(MakeContext());

        Assert.NotEmpty(dossier);
        Assert.True(dossier.Length <= 1500);
    }

    [Fact]
    public async Task GenerateDossier_NoLlm_ReturnsFallbackWithNote()
    {
        var generator = new DescriptionGenerator(llmCall: null);
        var dossier = await generator.GenerateDossierAsync(MakeContext());

        Assert.Contains("communications link", dossier);
    }

    [Fact]
    public void BuildSummaryPrompt_ContainsLocationDetails()
    {
        var context = MakeContext("Rivetville", "town", BiomeType.Industrial);
        var prompt = DescriptionGenerator.BuildSummaryPrompt(context);

        Assert.Contains("Rivetville", prompt);
        Assert.Contains("town", prompt);
        Assert.Contains("Industrial", prompt);
    }
}
