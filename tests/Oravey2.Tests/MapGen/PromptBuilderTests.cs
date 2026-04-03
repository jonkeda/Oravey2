using Oravey2.MapGen.Models;
using Oravey2.MapGen.Services;

namespace Oravey2.Tests.MapGen;

public class PromptBuilderTests
{
    private readonly PromptBuilder _builder = new();

    [Fact]
    public void BuildSystemPrompt_ContainsSchema()
    {
        var prompt = _builder.BuildSystemPrompt();
        Assert.Contains("map-blueprint-v1", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ExcludesEntities()
    {
        var prompt = _builder.BuildSystemPrompt();
        Assert.Contains("OMIT", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSystemPrompt_InstructsWriteBlueprintTool()
    {
        var prompt = _builder.BuildSystemPrompt();
        Assert.Contains("write_blueprint", prompt);
        Assert.Contains("Do NOT output the JSON as text", prompt);
    }

    [Fact]
    public void BuildUserPrompt_ContainsLocationName()
    {
        var request = CreateRequest();
        var prompt = _builder.BuildUserPrompt(request);
        Assert.Contains("Portland", prompt);
    }

    [Fact]
    public void BuildUserPrompt_ContainsDimensions()
    {
        var request = CreateRequest();
        var prompt = _builder.BuildUserPrompt(request);
        Assert.Contains("4 chunks wide", prompt);
        Assert.Contains("4 chunks high", prompt);
    }

    [Fact]
    public void BuildUserPrompt_ContainsFactions()
    {
        var request = CreateRequest();
        var prompt = _builder.BuildUserPrompt(request);
        Assert.Contains("Settlers", prompt);
        Assert.Contains("Raiders", prompt);
    }

    [Fact]
    public void BuildUserPrompt_ContainsLevelRange()
    {
        var request = CreateRequest();
        var prompt = _builder.BuildUserPrompt(request);
        Assert.Contains("1", prompt);
        Assert.Contains("5", prompt);
    }

    private static MapGenerationRequest CreateRequest() => new()
    {
        LocationName = "Portland",
        GeographyDescription = "Pacific NW river city with hills",
        PostApocContext = "Nuclear fallout zone, overgrown ruins",
        ChunksWide = 4,
        ChunksHigh = 4,
        MinLevel = 1,
        MaxLevel = 5,
        DifficultyDescription = "Moderate",
        Factions = new[] { "Settlers", "Raiders" }
    };
}
