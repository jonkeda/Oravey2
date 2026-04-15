using System.Numerics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Oravey2.Contracts.ContentPack;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.Tests.Generation;

public class TownCuratorDiscoverTests
{
    private static readonly TownGenerationParams DefaultParams = TownGenerationParams.Apocalyptic;

    private static RegionTemplate CreateTestRegion(int townCount = 20)
    {
        var towns = new List<TownEntry>();
        for (int i = 0; i < townCount; i++)
        {
            double lat = 52.30 + i * 0.15;
            double lon = 4.80 + i * 0.05;
            int pop = 10000 + i * 5000;
            var category = i switch
            {
                < 3 => TownCategory.Hamlet,
                < 8 => TownCategory.Village,
                < 15 => TownCategory.Town,
                _ => TownCategory.City
            };
            towns.Add(new TownEntry(
                $"Town{i}", lat, lon, pop,
                new Vector2(i * 20000f, i * 16700f),
                category));
        }

        return new RegionTemplate
        {
            Name = "noord-holland",
            ElevationGrid = new float[1, 1],
            GridOriginLat = 52.50,
            GridOriginLon = 4.95,
            GridCellSizeMetres = 30.0,
            Towns = towns,
        };
    }

    [Fact]
    public void BuildDiscoverPrompt_ContainsRegionNameAndSettlementInfo()
    {
        var region = CreateTestRegion();
        var prompt = TownCurator.BuildDiscoverPrompt(region, DefaultParams);

        Assert.Contains("noord-holland", prompt);
        Assert.Contains("destruction", prompt);
        Assert.Contains("submit_towns", prompt);
        // No town list in prompt — LLM uses world knowledge
        Assert.DoesNotContain("Town0", prompt);
        Assert.DoesNotContain("Town19", prompt);
    }

    [Fact]
    public void BuildDiscoverPrompt_ContainsToolCallInstruction()
    {
        var region = CreateTestRegion();
        var prompt = TownCurator.BuildDiscoverPrompt(region, DefaultParams);

        Assert.Contains("Call the submit_towns function", prompt);
        Assert.DoesNotContain("JSON array", prompt);
        Assert.DoesNotContain("World seed", prompt);
    }

    [Fact]
    public void BuildDiscoverPrompt_UsesFantasyParams()
    {
        var fantasy = new TownGenerationParams
        {
            Genre = "Fantasy",
            ThemeDescription = "A medieval realm.",
            Roles = ["market_town", "fortress", "wizard_tower", "harbour"],
            NamingInstruction = "a fantasy rename",
        };

        var region = CreateTestRegion();
        var prompt = TownCurator.BuildDiscoverPrompt(region, fantasy);

        Assert.Contains("Fantasy", prompt);
        Assert.Contains("A medieval realm.", prompt);
        Assert.Contains("a fantasy rename", prompt);
        Assert.Contains("destruction", prompt);
        Assert.DoesNotContain("post-apocalyptic", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCuratedTowns_LooksUpFromTemplate()
    {
        var region = CreateTestRegion();
        var entries = Enumerable.Range(0, 10).Select(i => new CuratedTownDto
        {
            GameName = $"Haven-{i}",
            RealName = $"Town{i}",
            Description = "A settlement.",
            Size = "Town",
            Inhabitants = 5000 + i * 1000,
            Destruction = "Moderate",
        }).ToList();

        var towns = TownCurator.BuildCuratedTowns(entries, region, DefaultParams);

        Assert.Equal(10, towns.Count);
        Assert.Equal("Haven-0", towns[0].GameName);
        Assert.Equal("Town0", towns[0].RealName);
        // Coordinates come from the template, not the LLM
        Assert.Equal(52.30, towns[0].Latitude);
        Assert.Equal(4.80, towns[0].Longitude);
        Assert.Equal(new Vector2(0, 0), towns[0].GamePosition);
    }

    [Fact]
    public void BuildCuratedTowns_SkipsUnmatchedNames()
    {
        var region = CreateTestRegion();
        var entries = new List<CuratedTownDto>
        {
            new() { GameName = "A", RealName = "Town0", Description = "d", Size = "Town", Inhabitants = 5000, Destruction = "Moderate" },
            new() { GameName = "B", RealName = "NonExistent", Description = "d", Size = "Village", Inhabitants = 1000, Destruction = "Light" },
            new() { GameName = "C", RealName = "Town1", Description = "d", Size = "Town", Inhabitants = 3000, Destruction = "Heavy" },
        };

        var towns = TownCurator.BuildCuratedTowns(entries, region, DefaultParams);

        Assert.Equal(2, towns.Count);
        Assert.Equal("Town0", towns[0].RealName);
        Assert.Equal("Town1", towns[1].RealName);
    }

    [Fact]
    public void BuildCuratedTowns_ParsesDestructionLevel()
    {
        var region = CreateTestRegion();
        var entries = new List<CuratedTownDto>
        {
            new() { GameName = "A", RealName = "Town0", Description = "d", Size = "Town", Inhabitants = 5000, Destruction = "Devastated" },
            new() { GameName = "B", RealName = "Town1", Description = "d", Size = "Village", Inhabitants = 1000, Destruction = "bogus" },
        };

        var towns = TownCurator.BuildCuratedTowns(entries, region, DefaultParams);
        Assert.Equal(DestructionLevel.Devastated, towns[0].Destruction);
        Assert.Equal(DestructionLevel.Moderate, towns[1].Destruction); // invalid → default
    }

    [Fact]
    public void BuildCuratedTowns_UsesTemplateBoundaryPolygon()
    {
        var boundary = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) };
        var region = new RegionTemplate
        {
            Name = "test",
            ElevationGrid = new float[1, 1],
            GridOriginLat = 52.0,
            GridOriginLon = 4.0,
            GridCellSizeMetres = 30.0,
            Towns = [new TownEntry("TestTown", 52.5, 4.9, 50000, new Vector2(100, 200), TownCategory.Town, boundary)],
        };

        var entries = new List<CuratedTownDto>
        {
            new() { GameName = "Haven", RealName = "TestTown", Description = "d", Size = "Town", Inhabitants = 5000, Destruction = "Moderate" },
        };

        var towns = TownCurator.BuildCuratedTowns(entries, region, DefaultParams);
        Assert.Single(towns);
        Assert.NotNull(towns[0].BoundaryPolygon);
        Assert.Equal(new Vector2(100, 200), towns[0].GamePosition);
    }

    [Fact]
    public void StripMarkdownFences_NoFences_ReturnsUnchanged()
    {
        var input = "[{\"key\":\"value\"}]";
        var output = TownCurator.StripMarkdownFences(input);
        Assert.Equal(input, output);
    }

    [Fact]
    public void StripMarkdownFences_WithFences_StripsCorrectly()
    {
        var inner = "[{\"key\":\"value\"}]";
        var wrapped = $"```json\n{inner}\n```";
        var output = TownCurator.StripMarkdownFences(wrapped);
        Assert.Equal(inner, output);
    }

    [Fact]
    public async Task DiscoverAsync_CallsToolAndReturnsTowns()
    {
        var region = CreateTestRegion();
        var entries = Enumerable.Range(0, 10).Select(i => new CuratedTownDto
        {
            GameName = $"Haven-{i}",
            RealName = $"Town{i}",
            Description = "Settlement.",
            Size = "Town",
            Inhabitants = 5000 + i * 1000,
            Destruction = "Moderate",
        }).ToList();

        string? capturedPrompt = null;
        Task<string> FakeLlm(string p, CancellationToken ct) => Task.FromResult("");
        async Task ToolLlm(string p, IList<AIFunction> tools, CancellationToken ct)
        {
            capturedPrompt = p;
            var tool = tools.First(t => t.Name == "submit_towns");
            var jsonElement = JsonSerializer.SerializeToElement(entries);
            await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["towns"] = jsonElement }), ct);
        }

        var curator = new TownCurator(FakeLlm, ToolLlm);
        var towns = await curator.DiscoverAsync(region);

        Assert.NotNull(capturedPrompt);
        Assert.Contains("noord-holland", capturedPrompt);
        Assert.Equal(10, towns.Count);
    }

    [Fact]
    public async Task DiscoverAsync_ThrowsWithoutToolCall()
    {
        var region = CreateTestRegion();
        Task<string> FakeLlm(string p, CancellationToken ct) => Task.FromResult("");

        var curator = new TownCurator(FakeLlm);
        await Assert.ThrowsAsync<InvalidOperationException>(() => curator.DiscoverAsync(region));
    }

    [Fact]
    public async Task DiscoverAsync_UsesToolCallWhenAvailable()
    {
        var region = CreateTestRegion();
        var entries = Enumerable.Range(0, 10).Select(i => new CuratedTownDto
        {
            GameName = $"T{i}",
            RealName = $"Town{i}",
            Description = "d",
            Size = "Town",
            Inhabitants = 5000,
            Destruction = "Moderate",
        }).ToList();

        bool textCalled = false, toolCalled = false;
        Task<string> TextLlm(string p, CancellationToken ct) { textCalled = true; return Task.FromResult(""); }
        async Task ToolLlm(string p, IList<AIFunction> tools, CancellationToken ct)
        {
            toolCalled = true;
            // Simulate the LLM calling the submit_towns tool with typed data
            var tool = tools.First(t => t.Name == "submit_towns");
            var jsonElement = JsonSerializer.SerializeToElement(entries);
            await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["towns"] = jsonElement }), ct);
        }

        var curator = new TownCurator(TextLlm, ToolLlm);
        var towns = await curator.DiscoverAsync(region);

        Assert.True(toolCalled, "DiscoverAsync should use tool delegate when available");
        Assert.False(textCalled, "DiscoverAsync should not use text delegate when tool is available");
        Assert.Equal(10, towns.Count);
    }

    [Fact]
    public async Task DiscoverAsync_LogsSentAndReceived()
    {
        var region = CreateTestRegion();
        var entries = Enumerable.Range(0, 10).Select(i => new CuratedTownDto
        {
            GameName = $"H{i}",
            RealName = $"Town{i}",
            Description = "d",
            Size = "Town",
            Inhabitants = 5000,
            Destruction = "Moderate",
        }).ToList();

        var logs = new List<(string dir, string msg)>();
        Task<string> FakeLlm(string p, CancellationToken ct) => Task.FromResult("");
        async Task ToolLlm(string p, IList<AIFunction> tools, CancellationToken ct)
        {
            var tool = tools.First(t => t.Name == "submit_towns");
            var jsonElement = JsonSerializer.SerializeToElement(entries);
            await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["towns"] = jsonElement }), ct);
        }

        var curator = new TownCurator(FakeLlm, ToolLlm, log: (dir, msg) => logs.Add((dir, msg)));
        await curator.DiscoverAsync(region);

        Assert.Equal(2, logs.Count);
        Assert.Equal("→ Sent", logs[0].dir);
        Assert.Equal("← Received", logs[1].dir);
    }

    // --- LatLonToMetres tests (utility kept) ---

    [Fact]
    public void LatLonToMetres_SamePoint_ReturnsZero()
    {
        var result = TownCurator.LatLonToMetres(52.5, 4.9, 52.5, 4.9);
        Assert.Equal(0, result.X, 1.0);
        Assert.Equal(0, result.Y, 1.0);
    }

    [Fact]
    public void LatLonToMetres_KnownDistance_WithinTolerance()
    {
        var result = TownCurator.LatLonToMetres(52.38, 4.63, 52.37, 4.90);
        var dist = result.Length();
        Assert.InRange(dist, 15_000, 22_000);
    }

    [Fact]
    public void LatLonToMetres_OneDegreeLat_About111km()
    {
        var result = TownCurator.LatLonToMetres(53.0, 5.0, 52.0, 5.0);
        Assert.InRange(result.Y, 110_000, 112_000);
        Assert.Equal(0, result.X, 100.0);
    }

    // --- TownGenerationParams.LoadFromManifest ---

    [Fact]
    public void LoadFromManifest_MissingFile_ReturnsDefault()
    {
        var result = TownGenerationParams.LoadFromManifest("/nonexistent/path");
        Assert.Equal("Post-Apocalyptic", result.Genre);
    }
}
