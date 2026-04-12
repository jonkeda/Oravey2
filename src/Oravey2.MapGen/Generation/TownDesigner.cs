using System.Text.Json;
using Microsoft.Extensions.AI;
using Oravey2.MapGen.RegionTemplates;

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

    // Size-dependent building counts: (landmarks, minKeyLoc, maxKeyLoc, maxHazards)
    internal static (int Landmarks, int MinKeyLocations, int MaxKeyLocations, int MaxHazards) CountsForSize(TownCategory size) => size switch
    {
        TownCategory.Hamlet     => (1, 2,  3,  1),
        TownCategory.Village    => (1, 3,  5,  2),
        TownCategory.Town       => (2, 4,  7,  3),
        TownCategory.City       => (3, 6,  10, 3),
        TownCategory.Metropolis => (5, 8,  14, 4),
        _                       => (1, 3,  5,  2),
    };

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
                captured = BuildTownDesign(town.GameName, design, town.Size);
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

        return ParseTextResponse(town.GameName, response, town.Size);
    }

    internal static string BuildPrompt(CuratedTown town, string regionContext, int seed)
    {
        var (landmarkCount, minKeyLoc, maxKeyLoc, maxHazards) = CountsForSize(town.Size);

        return $$"""
            You are designing buildings for a post-apocalyptic RPG town.

            === REAL-WORLD CONTEXT ===
            Real town name: {{town.RealName}}
            Coordinates: {{town.Latitude:F4}}, {{town.Longitude:F4}}

            === GAME CONTEXT ===
            Game name: {{town.GameName}}
            Size: {{town.Size}} ({{town.Inhabitants}} inhabitants)
            Destruction level: {{town.Destruction}}
            Description: {{town.Description}}
            Region: {{regionContext}}
            World seed: {{seed}}

            === INSTRUCTIONS ===
            1. Research the real town {{town.RealName}} at ({{town.Latitude:F4}}, {{town.Longitude:F4}}).
               Identify its most notable real buildings, landmarks, and public institutions
               (churches, town halls, harbours, windmills, bridges, markets, etc.).

            2. Select {{landmarkCount}} landmark(s) and {{minKeyLoc}}–{{maxKeyLoc}} key locations based on those real buildings.

            3. For each building (landmark and key location):
               a. Name — a post-apocalyptic rename of the real building name (keep recognisable)
               b. VisualDescription — detailed visual for a 3D artist (exterior only, include damage from {{town.Destruction}})
               c. SizeCategory — small, medium, or large
               d. OriginalDescription — one sentence: the real building's name, architectural style, era, and purpose
               e. MeshyPrompt — 30–60 word text-to-3D prompt describing materials, damage state, style. End with "low-poly game asset". Exterior only.
               f. PositionHint — compass direction + nearby feature relative to town centre
                  (e.g. "north-east, near the harbour", "centre, on the main square")

            4. Choose a layout style: grid, radial, organic, linear, clustered, or compound
               (pick the one that matches the real town's street pattern)

            5. Add 0–{{maxHazards}} environmental hazards consistent with the {{town.Destruction}} destruction level.

            6. **CRITICAL: Provide spatial specification** with real-world coordinates, building footprints,
               road network, and water bodies to ground the town in geography:
               - RealWorldBounds: min/max latitude and longitude in decimal degrees
               - BuildingPlacements: center coordinates (lat/lon), footprint sizes (meters), rotation, alignment hint
               - RoadNetwork: connected nodes and edges with road width (meters)
               - WaterBodies (optional): polygon vertices, water type (river/canal/harbour/lake)
               - TerrainDescription: brief note on terrain (flat, hilly, mixed, etc.)

            Key location purposes: shop, quest_giver, crafting, medical, barracks, tavern, storage, other
            Hazard types: flooding, radiation, collapse, fire, toxic, wildlife, other
            """;
    }

    internal static TownDesign BuildTownDesign(string townName, LlmTownDesignEntry entry, TownCategory size = TownCategory.Village)
    {
        var (_, _, _, maxHazards) = CountsForSize(size);

        var landmarks = entry.Landmarks
            .Select(l => new LandmarkBuilding(
                l.Name, l.VisualDescription, NormalizeSizeCategory(l.SizeCategory),
                l.OriginalDescription, l.MeshyPrompt, l.PositionHint))
            .ToList();

        // Ensure at least one landmark
        if (landmarks.Count == 0)
            landmarks.Add(new LandmarkBuilding("Unknown Landmark", "", "medium", "", "", "centre"));

        var keyLocations = entry.KeyLocations
            .Select(k => new KeyLocation(
                k.Name, k.Purpose, k.VisualDescription, NormalizeSizeCategory(k.SizeCategory),
                k.OriginalDescription, k.MeshyPrompt, k.PositionHint))
            .ToList();

        var layoutStyle = NormalizeLayoutStyle(entry.LayoutStyle);

        var hazards = entry.Hazards
            .Take(maxHazards)
            .Select(h => new EnvironmentalHazard(h.Type, h.Description, h.LocationHint))
            .ToList();

        var design = new TownDesign(townName, landmarks, keyLocations, layoutStyle, hazards);

        // Build and validate spatial specification if provided
        if (entry.SpatialSpec is not null)
        {
            try
            {
                var spatialSpec = BuildSpatialSpecification.Build(entry.SpatialSpec, design);
                design = design with { SpatialSpec = spatialSpec };
            }
            catch (InvalidOperationException ex)
            {
                // Log validation error but don't fail — spatial spec is optional in this phase
                System.Diagnostics.Debug.WriteLine($"Spatial spec validation failed: {ex.Message}");
            }
        }

        return design;
    }

    internal static TownDesign ParseTextResponse(string townName, string response, TownCategory size = TownCategory.Village)
    {
        var json = ExtractJson(response);
        var entry = JsonSerializer.Deserialize<LlmTownDesignEntry>(json, JsonOptions)
                    ?? throw new InvalidOperationException("Failed to parse LLM design response.");
        return BuildTownDesign(townName, entry, size);
    }

    private static string ExtractJson(string text)
    {
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
