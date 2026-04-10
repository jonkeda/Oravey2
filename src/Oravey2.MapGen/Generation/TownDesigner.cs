using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Oravey2.MapGen.Generation;

public sealed class TownDesigner
{
    private readonly Func<string, CancellationToken, Task<string>> _llmCall;
    private readonly Func<string, IList<AIFunction>, CancellationToken, Task>? _toolCall;
    private readonly Action<string, string>? _log;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly string[] ValidLayoutStyles =
        ["grid", "radial", "organic", "linear", "clustered", "compound"];

    public TownDesigner(
        Func<string, CancellationToken, Task<string>> llmCall,
        Func<string, IList<AIFunction>, CancellationToken, Task>? toolCall = null,
        Action<string, string>? log = null)
    {
        _llmCall = llmCall;
        _toolCall = toolCall;
        _log = log;
    }

    public async Task<TownDesign> DesignAsync(
        CuratedTown town,
        string regionContext,
        int seed,
        CancellationToken ct = default)
    {
        if (_toolCall is not null)
            return await DesignWithToolCallAsync(town, regionContext, seed, ct);

        return await DesignWithTextCallAsync(town, regionContext, seed, ct);
    }

    private async Task<TownDesign> DesignWithToolCallAsync(
        CuratedTown town,
        string regionContext,
        int seed,
        CancellationToken ct)
    {
        var prompt = BuildPrompt(town, regionContext, seed);
        _log?.Invoke("→ Sent", prompt);

        TownDesign? captured = null;

        var submitTool = AIFunctionFactory.Create(
            (LlmTownDesignEntry design) =>
            {
                _log?.Invoke("← Received", JsonSerializer.Serialize(design, JsonOptions));
                captured = BuildTownDesign(town.GameName, design);
                return "Accepted.";
            },
            "submit_town_design",
            "Submit the town design. Call this exactly once.");

        await _toolCall!(prompt, [submitTool], ct);

        return captured ?? throw new InvalidOperationException("LLM did not call submit_town_design tool.");
    }

    private async Task<TownDesign> DesignWithTextCallAsync(
        CuratedTown town,
        string regionContext,
        int seed,
        CancellationToken ct)
    {
        var prompt = BuildPrompt(town, regionContext, seed);
        _log?.Invoke("→ Sent", prompt);

        var response = await _llmCall(prompt, ct);
        _log?.Invoke("← Received", response);

        return ParseTextResponse(town.GameName, response);
    }

    internal static string BuildPrompt(CuratedTown town, string regionContext, int seed) => $$"""
        You are designing a single town for a post-apocalyptic RPG.

        Region context: {{regionContext}}
        World seed: {{seed}}

        Town to design:
        - Name: {{town.GameName}} (real name: {{town.RealName}})
        - Role: {{town.Role}}
        - Faction: {{town.Faction}}
        - Threat level: {{town.ThreatLevel}} (1 = safe, 10 = deadly)
        - Description: {{town.Description}}

        Design this town with:
        1. One landmark building — the most iconic structure (name, visual description for 3D asset generation, size: small/medium/large)
        2. 3–8 key locations — places the player visits (name, purpose, visual description, size)
           Purposes: shop, quest_giver, crafting, medical, barracks, tavern, storage, other
        3. Layout style — one of: grid, radial, organic, linear, clustered, compound
        4. 0–3 environmental hazards — dangers in the environment (type, description, location hint)
           Types: flooding, radiation, collapse, fire, toxic, wildlife, other

        The visual descriptions should be detailed enough for a 3D artist to create the asset.
        Keep the design consistent with the town's role, faction, and threat level.
        """;

    internal static TownDesign BuildTownDesign(string townName, LlmTownDesignEntry entry)
    {
        var landmark = new LandmarkBuilding(
            entry.LandmarkName,
            entry.LandmarkVisualDescription,
            NormalizeSizeCategory(entry.LandmarkSizeCategory));

        var keyLocations = entry.KeyLocations
            .Select(k => new KeyLocation(k.Name, k.Purpose, k.VisualDescription, NormalizeSizeCategory(k.SizeCategory)))
            .ToList();

        var layoutStyle = NormalizeLayoutStyle(entry.LayoutStyle);

        var hazards = entry.Hazards
            .Take(3)
            .Select(h => new EnvironmentalHazard(h.Type, h.Description, h.LocationHint))
            .ToList();

        return new TownDesign(townName, landmark, keyLocations, layoutStyle, hazards);
    }

    internal static TownDesign ParseTextResponse(string townName, string response)
    {
        // Try to extract JSON from the response
        var json = ExtractJson(response);
        var entry = JsonSerializer.Deserialize<LlmTownDesignEntry>(json, JsonOptions)
                    ?? throw new InvalidOperationException("Failed to parse LLM design response.");
        return BuildTownDesign(townName, entry);
    }

    private static string ExtractJson(string text)
    {
        // Find the first { and last }
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end < 0 || end <= start)
            throw new InvalidOperationException("No JSON object found in LLM response.");
        return text[start..(end + 1)];
    }

    private static string NormalizeSizeCategory(string size) =>
        size?.ToLowerInvariant() switch
        {
            "small" => "small",
            "medium" => "medium",
            "large" => "large",
            _ => "medium",
        };

    private static string NormalizeLayoutStyle(string style)
    {
        var lower = style?.ToLowerInvariant() ?? "organic";
        return ValidLayoutStyles.Contains(lower) ? lower : "organic";
    }
}
