using System.Numerics;
using System.Text.Json;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.Tests.Pipeline;

public class TownDesignerTests
{
    private static readonly CuratedTown SampleTown = new(
        GameName: "Havenburg",
        RealName: "Den Helder",
        Latitude: 52.96,
        Longitude: 4.76,
        GamePosition: new Vector2(4760f, 52960f),
        Description: "A fortified coastal trading settlement",
        Size: TownCategory.Town,
        Inhabitants: 5000,
        Destruction: DestructionLevel.Heavy);

    private const string SampleRegionContext = "Region: noord-holland, Lat 52.20–52.90, Lon 4.50–5.20";

    // --- BuildPrompt ---

    [Fact]
    public void BuildPrompt_IncludesTownMetadata()
    {
        var prompt = TownDesigner.BuildPrompt(SampleTown, SampleRegionContext, 42);

        Assert.Contains("Havenburg", prompt);
        Assert.Contains("Den Helder", prompt);
        Assert.Contains("Town", prompt);  // size
        Assert.Contains("Heavy", prompt); // destruction
        Assert.Contains("fortified coastal", prompt);
        Assert.Contains("noord-holland", prompt);
        Assert.Contains("42", prompt); // seed
    }

    [Fact]
    public void BuildPrompt_IncludesDesignInstructions()
    {
        var prompt = TownDesigner.BuildPrompt(SampleTown, SampleRegionContext, 42);

        Assert.Contains("landmark", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("key locations", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("layout style", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hazard", prompt, StringComparison.OrdinalIgnoreCase);
    }

    // --- BuildTownDesign ---

    [Fact]
    public void BuildTownDesign_MapsEntryCorrectly()
    {
        var entry = new LlmTownDesignEntry
        {
            Landmarks =
            [
                new LlmLandmarkEntry
                {
                    Name = "Fort Kijkduin",
                    VisualDescription = "A massive coastal fortress",
                    SizeCategory = "large",
                },
            ],
            KeyLocations =
            [
                new LlmKeyLocationEntry
                {
                    Name = "Market",
                    Purpose = "shop",
                    VisualDescription = "An old drydock market",
                    SizeCategory = "medium",
                },
                new LlmKeyLocationEntry
                {
                    Name = "Clinic",
                    Purpose = "medical",
                    VisualDescription = "A converted church",
                    SizeCategory = "small",
                },
            ],
            LayoutStyle = "compound",
            Hazards =
            [
                new LlmHazardEntry
                {
                    Type = "flooding",
                    Description = "Harbour floods at high tide",
                    LocationHint = "south-west waterfront",
                }
            ],
        };

        var design = TownDesigner.BuildTownDesign("Havenburg", entry);

        Assert.Equal("Havenburg", design.TownName);
        Assert.Equal("Fort Kijkduin", design.Landmarks[0].Name);
        Assert.Equal("large", design.Landmarks[0].SizeCategory);
        Assert.Equal(2, design.KeyLocations.Count);
        Assert.Equal("Market", design.KeyLocations[0].Name);
        Assert.Equal("shop", design.KeyLocations[0].Purpose);
        Assert.Equal("compound", design.LayoutStyle);
        Assert.Single(design.Hazards);
        Assert.Equal("flooding", design.Hazards[0].Type);
    }

    [Fact]
    public void BuildTownDesign_NormalizesInvalidSizeCategory()
    {
        var entry = new LlmTownDesignEntry
        {
            Landmarks =
            [
                new LlmLandmarkEntry
                {
                    Name = "Tower",
                    VisualDescription = "A tall tower",
                    SizeCategory = "enormous",  // invalid
                },
            ],
            KeyLocations = [],
            LayoutStyle = "grid",
            Hazards = [],
        };

        var design = TownDesigner.BuildTownDesign("Test", entry);
        Assert.Equal("medium", design.Landmarks[0].SizeCategory);
    }

    [Fact]
    public void BuildTownDesign_NormalizesInvalidLayoutStyle()
    {
        var entry = new LlmTownDesignEntry
        {
            Landmarks =
            [
                new LlmLandmarkEntry
                {
                    Name = "Tower",
                    VisualDescription = "A tall tower",
                    SizeCategory = "small",
                },
            ],
            KeyLocations = [],
            LayoutStyle = "sprawling",  // invalid
            Hazards = [],
        };

        var design = TownDesigner.BuildTownDesign("Test", entry);
        Assert.Equal("organic", design.LayoutStyle);
    }

    [Fact]
    public void BuildTownDesign_CapsHazardsAtThree()
    {
        var entry = new LlmTownDesignEntry
        {
            Landmarks =
            [
                new LlmLandmarkEntry
                {
                    Name = "Tower",
                    VisualDescription = "A tall tower",
                    SizeCategory = "small",
                },
            ],
            KeyLocations = [],
            LayoutStyle = "grid",
            Hazards =
            [
                new LlmHazardEntry { Type = "flooding", Description = "a", LocationHint = "a" },
                new LlmHazardEntry { Type = "radiation", Description = "b", LocationHint = "b" },
                new LlmHazardEntry { Type = "fire", Description = "c", LocationHint = "c" },
                new LlmHazardEntry { Type = "collapse", Description = "d", LocationHint = "d" },
            ],
        };

        var design = TownDesigner.BuildTownDesign("Test", entry, TownCategory.City);
        Assert.Equal(3, design.Hazards.Count);
    }

    // --- ParseTextResponse ---

    [Fact]
    public void ParseTextResponse_ExtractsJsonFromText()
    {
        var json = """
            Here is the design:
            {
                "landmarks": [
                    {"name": "Fort Kijkduin", "visualDescription": "A massive fortress", "sizeCategory": "large"}
                ],
                "keyLocations": [
                    {"name": "Market", "purpose": "shop", "visualDescription": "A market", "sizeCategory": "medium"}
                ],
                "layoutStyle": "compound",
                "hazards": []
            }
            That should work!
            """;

        var design = TownDesigner.ParseTextResponse("Havenburg", json);

        Assert.Equal("Havenburg", design.TownName);
        Assert.Equal("Fort Kijkduin", design.Landmarks[0].Name);
        Assert.Equal("compound", design.LayoutStyle);
    }

    [Fact]
    public void ParseTextResponse_ThrowsOnNoJson()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TownDesigner.ParseTextResponse("Test", "No JSON here"));
    }

    // --- DesignAsync with tool call ---

    [Fact]
    public async Task DesignAsync_WithToolCall_CapturesResult()
    {
        // Mock tool call: invoke the AIFunction with a valid LlmTownDesignEntry
        Func<string, IList<Microsoft.Extensions.AI.AIFunction>, CancellationToken, Task> mockToolCall =
            async (prompt, tools, ct) =>
            {
                Assert.Single(tools);
                var tool = tools[0];
                Assert.Equal("submit_town_design", tool.Name);

                // Simulate the LLM invoking the tool
                var args = new Microsoft.Extensions.AI.AIFunctionArguments(new Dictionary<string, object?>
                {
                    ["design"] = JsonDocument.Parse("""
                        {
                            "landmarks": [
                                {"name": "Fort Kijkduin", "visualDescription": "A massive coastal fortress", "sizeCategory": "large"}
                            ],
                            "keyLocations": [
                                {"name": "Market", "purpose": "shop", "visualDescription": "Drydock market", "sizeCategory": "medium"}
                            ],
                            "layoutStyle": "compound",
                            "hazards": [
                                {"type": "flooding", "description": "Harbour floods", "locationHint": "south-west"}
                            ]
                        }
                        """).RootElement
                });

                await tool.InvokeAsync(args, ct);
            };

        Func<string, CancellationToken, Task<string>> mockTextCall =
            (_, _) => Task.FromResult("");

        var designer = new TownDesigner(mockTextCall, mockToolCall);
        var design = await designer.DesignAsync(SampleTown, SampleRegionContext, 42);

        Assert.Equal("Havenburg", design.TownName);
        Assert.Equal("Fort Kijkduin", design.Landmarks[0].Name);
        Assert.Equal("compound", design.LayoutStyle);
    }

    [Fact]
    public async Task DesignAsync_WithTextCall_ParsesResponse()
    {
        var responseJson = """
            {
                "landmarks": [
                    {"name": "The Lighthouse", "visualDescription": "A crumbling lighthouse", "sizeCategory": "medium"}
                ],
                "keyLocations": [
                    {"name": "Dock", "purpose": "storage", "visualDescription": "Old wooden dock", "sizeCategory": "small"}
                ],
                "layoutStyle": "linear",
                "hazards": []
            }
            """;

        Func<string, CancellationToken, Task<string>> mockTextCall =
            (prompt, _) =>
            {
                Assert.Contains("Havenburg", prompt);
                return Task.FromResult(responseJson);
            };

        var designer = new TownDesigner(mockTextCall, toolCall: null);
        var design = await designer.DesignAsync(SampleTown, SampleRegionContext, 42);

        Assert.Equal("Havenburg", design.TownName);
        Assert.Equal("The Lighthouse", design.Landmarks[0].Name);
        Assert.Equal("linear", design.LayoutStyle);
        Assert.Single(design.KeyLocations);
        Assert.Empty(design.Hazards);
    }
}
