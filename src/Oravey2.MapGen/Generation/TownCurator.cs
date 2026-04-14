using System.Numerics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

public sealed class TownCurator
{
    private readonly Func<string, CancellationToken, Task<string>> _llmCall;
    private readonly Func<string, IList<AIFunction>, CancellationToken, Task>? _toolCall;
    private readonly TownGenerationParams _params;
    private readonly Action<string, string>? _log;

    public TownCurator(
        Func<string, CancellationToken, Task<string>> llmCall,
        Func<string, IList<AIFunction>, CancellationToken, Task>? toolCall = null,
        TownGenerationParams? generationParams = null,
        Action<string, string>? log = null)
    {
        _llmCall = llmCall;
        _toolCall = toolCall;
        _params = generationParams ?? TownGenerationParams.Apocalyptic;
        _log = log;
    }

    public async Task<CuratedRegion> CurateAsync(
        RegionTemplate region,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt(region, _params);
        _log?.Invoke("→ Sent", prompt);

        string response;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                response = await _llmCall(prompt, ct);
                break;
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                await Task.Delay(5_000, ct);
            }
        }

        _log?.Invoke("← Received", response);

        var towns = ParseResponse(response, region);
        Validate(towns, _params);

        var allPositions = towns.Select(t => t.GamePosition).ToArray();
        var boundsMin = new Vector2(allPositions.Min(p => p.X) - 1000, allPositions.Min(p => p.Y) - 1000);
        var boundsMax = new Vector2(allPositions.Max(p => p.X) + 1000, allPositions.Max(p => p.Y) + 1000);

        return new CuratedRegion { Name = region.Name, BoundsMin = boundsMin, BoundsMax = boundsMax, Towns = towns };
    }

    /// <summary>
    /// Mode A — LLM picks settlements from its world knowledge, returns via tool call.
    /// </summary>
    public async Task<List<CuratedTown>> DiscoverAsync(
        RegionTemplate region,
        CancellationToken ct = default)
    {
        if (_toolCall is null)
            throw new InvalidOperationException("DiscoverAsync requires tool-calling support.");

        var prompt = BuildDiscoverPrompt(region, _params);
        _log?.Invoke("→ Sent", prompt);

        List<CuratedTown>? captured = null;

        var submitTool = AIFunctionFactory.Create(
            (List<LlmTownEntry> towns) =>
            {
                _log?.Invoke("← Received", JsonSerializer.Serialize(towns, JsonOptions));
                captured = BuildCuratedTowns(towns, region, _params);
                return "Accepted.";
            },
            "submit_towns",
            "Submit the selected towns. Call this exactly once.");

        await _toolCall(prompt, [submitTool], ct);

        return captured ?? throw new InvalidOperationException("LLM did not call submit_towns tool.");
    }

    /// <summary>
    /// Re-roll a single town — asks the LLM for a replacement.
    /// </summary>
    public async Task<CuratedTown> RerollTownAsync(
        RegionTemplate region,
        List<CuratedTown> existing,
        CuratedTown toReplace,
        CancellationToken ct = default)
    {
        var p = _params;
        var existingList = string.Join("\n", existing.Where(t => t != toReplace)
            .Select(t => $"- {t.GameName} ({t.RealName}): {t.Size}, {t.Destruction}"));
        var townNames = string.Join(", ", region.Towns.Select(t => t.Name));

        var prompt = $$"""
            You are creating a {{p.Genre}} RPG world in the region "{{region.Name}}".
            {{p.ThemeDescription}}

            Available towns in this region: {{townNames}}

            The following towns are already selected:
            {{existingList}}

            Replace the town "{{toReplace.GameName}}" ({{toReplace.RealName}}) with a different {{p.SettlementNoun}}.
            Pick a town from the available list that is NOT already selected.

            Respond with a single JSON object:
            {"gameName":"...","realName":"...","description":"...","size":"Village","inhabitants":1000,"destruction":"Moderate"}

            size must be one of: Hamlet, Village, Town, City, Metropolis
            destruction must be one of: Pristine, Light, Moderate, Heavy, Devastated
            """;

        _log?.Invoke("→ Sent", prompt);

        var townLookup = BuildTownLookup(region);

        if (_toolCall is not null)
        {
            CuratedTown? captured = null;

            var submitTool = AIFunctionFactory.Create(
                (LlmTownEntry town) =>
                {
                    _log?.Invoke("← Received", JsonSerializer.Serialize(town, JsonOptions));
                    captured = BuildCuratedTown(town, townLookup, p);
                    return "Accepted.";
                },
                "submit_town",
                "Submit the replacement town.");

            await _toolCall(prompt, [submitTool], ct);
            return captured ?? throw new InvalidOperationException("LLM did not call submit_town tool.");
        }

        // Fallback: text-based call
        var response = await _llmCall(prompt, ct);
        _log?.Invoke("← Received", response);
        response = StripMarkdownFences(response);
        var e = JsonSerializer.Deserialize<LlmTownEntry>(response, JsonOptions)
            ?? throw new InvalidOperationException("LLM returned invalid JSON for town re-roll");
        return BuildCuratedTown(e, townLookup, p);
    }

    /// <summary>
    /// Enrich a single OSM town with LLM-generated metadata (GameName, Description, Destruction).
    /// </summary>
    public async Task<TownEnrichment> EnrichTownAsync(
        TownEntry osmTown,
        CancellationToken ct = default)
    {
        var p = _params;
        var prompt = $$"""
            You are a world-builder for a {{p.Genre}} game. {{p.ThemeDescription}}

            Given this real-world settlement:
            - Name: {{osmTown.Name}}
            - Category: {{osmTown.Category}}
            - Population: {{osmTown.Population}}
            - Coordinates: {{osmTown.Latitude:F4}}, {{osmTown.Longitude:F4}}

            Generate:
            1. gameName — {{p.NamingInstruction}}
            2. description — 1-2 sentences describing the settlement in the game world
            3. destruction — one of: Pristine, Light, Moderate, Heavy, Devastated

            Respond with ONLY a JSON object:
            {"gameName":"...","description":"...","destruction":"Moderate"}
            """;

        _log?.Invoke("→ Sent", prompt);

        var response = await _llmCall(prompt, ct);
        _log?.Invoke("← Received", response);
        response = StripMarkdownFences(response);

        var entry = JsonSerializer.Deserialize<LlmTownEntry>(response, JsonOptions)
            ?? throw new InvalidOperationException("LLM returned invalid JSON for town enrichment");

        return new TownEnrichment(
            entry.GameName,
            entry.Description,
            Enum.TryParse<DestructionLevel>(entry.Destruction, true, out var dl) ? dl : DestructionLevel.Moderate);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static string BuildPrompt(RegionTemplate region, TownGenerationParams p)
    {
        var townList = string.Join("\n", region.Towns.Select(t =>
            $"- {t.Name}: pop {t.Population}, category {t.Category}, lat {t.Latitude:F4} lon {t.Longitude:F4}"));

        return $$"""
            You are creating a {{p.Genre}} RPG world. {{p.ThemeDescription}}
            The region is "{{region.Name}}".

            Here are all real towns in this region:
            {{townList}}

            Select {{p.MinTowns}}–{{p.MaxTowns}} towns for the game. For each, provide:
            - gameName: {{p.NamingInstruction}}
            - realName: the original town name
            - latitude, longitude: from the list above
            - description: 1–2 sentences about the {{p.SettlementNoun}}
            - size: one of [Hamlet, Village, Town, City, Metropolis]
            - inhabitants: estimated number of inhabitants
            - destruction: one of [Pristine, Light, Moderate, Heavy, Devastated]

            Requirements:
            - Prefer larger towns but include some small {{p.SettlementNoun}}s for variety
            - Mix destruction levels for gameplay variety

            Respond with ONLY a JSON array. No markdown, no explanation.
            Example format:
            [
              {"gameName":"Haven","realName":"Purmerend","latitude":52.50,"longitude":4.95,"description":"A fortified market town...","size":"Town","inhabitants":5000,"destruction":"Moderate"}
            ]
            """;
    }

    internal static List<CuratedTown> ParseResponse(string json, RegionTemplate region)
    {
        json = StripMarkdownFences(json);

        var entries = JsonSerializer.Deserialize<List<LlmTownEntry>>(json, JsonOptions)
            ?? throw new InvalidOperationException("LLM returned invalid JSON for town curation");

        var townLookup = BuildTownLookup(region);

        var result = new List<CuratedTown>();
        foreach (var e in entries)
        {
            var boundary = townLookup.TryGetValue(e.RealName, out var match) ? match.BoundaryPolygon : null;
            var gamePos = match?.GamePosition ?? new Vector2((float)(e.Longitude * 1000), (float)(e.Latitude * 1000));
            var size = match?.Category ?? (Enum.TryParse<TownCategory>(e.Size, true, out var sz) ? sz : TownCategory.Village);
            var inhabitants = match?.Population ?? e.Inhabitants;

            result.Add(new CuratedTown
            {
                GameName = e.GameName,
                RealName = e.RealName,
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                GamePosition = gamePos,
                Description = e.Description,
                Size = size,
                Inhabitants = inhabitants,
                Destruction = Enum.TryParse<DestructionLevel>(e.Destruction, true, out var dl) ? dl : DestructionLevel.Moderate,
                BoundaryPolygon = boundary,
            });
        }

        return result;
    }

    internal static string BuildDiscoverPrompt(RegionTemplate region, TownGenerationParams p)
    {
        return $$"""
            You are creating a {{p.Genre}} RPG world. {{p.ThemeDescription}}
            The region is "{{region.Name}}".

            Pick {{p.MinTowns}}–{{p.MaxTowns}} real-world settlements from this region.
            They can be any size — hamlets, villages, towns, or cities.
            Choose a mix that would make an interesting {{p.Genre}} game world.

            For each {{p.SettlementNoun}}:
            - gameName: {{p.NamingInstruction}}
            - realName: the real-world name of the settlement
            - description: 1–2 sentences about the {{p.SettlementNoun}}
            - size: one of [Hamlet, Village, Town, City, Metropolis]
            - inhabitants: estimated number of inhabitants
            - destruction: one of [Pristine, Light, Moderate, Heavy, Devastated]

            Requirements:
            - Include a mix of settlement sizes for variety
            - Mix destruction levels for gameplay variety

            Call the submit_towns function with your selections.
            """;
    }

    internal static string StripMarkdownFences(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..];
            var lastFence = text.LastIndexOf("```");
            if (lastFence >= 0) text = text[..lastFence];
            text = text.Trim();
        }
        return text;
    }

    /// <summary>
    /// Builds a case-insensitive lookup from town name to TownEntry.
    /// Handles duplicate names by keeping the first (largest population) entry.
    /// </summary>
    private static Dictionary<string, TownEntry> BuildTownLookup(RegionTemplate region)
    {
        var lookup = new Dictionary<string, TownEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in region.Towns)
            lookup.TryAdd(t.Name, t);
        return lookup;
    }

    /// <summary>
    /// Convert lat/lon to a Vector2 in metres, relative to a reference point.
    /// Uses the Equirectangular approximation (accurate within a single region).
    /// </summary>
    internal static Vector2 LatLonToMetres(
        double lat, double lon, double refLat, double refLon)
    {
        const double R = 6_371_000; // Earth radius in metres
        double dLat = (lat - refLat) * Math.PI / 180.0;
        double dLon = (lon - refLon) * Math.PI / 180.0;
        double x = dLon * R * Math.Cos(refLat * Math.PI / 180.0);
        double y = dLat * R;
        return new Vector2((float)x, (float)y);
    }

    internal static void Validate(List<CuratedTown> towns, TownGenerationParams p)
    {
        if (towns.Count < p.MinTowns)
            throw new InvalidOperationException($"LLM selected only {towns.Count} towns, minimum is {p.MinTowns}");
        if (towns.Count > p.MaxTowns)
            towns.RemoveRange(p.MaxTowns, towns.Count - p.MaxTowns);
    }

    internal static List<CuratedTown> BuildCuratedTowns(
        List<LlmTownEntry> entries, RegionTemplate region, TownGenerationParams p)
    {
        var townLookup = BuildTownLookup(region);
        var result = new List<CuratedTown>();

        foreach (var e in entries)
        {
            if (!townLookup.TryGetValue(e.RealName, out var match))
                continue;

            result.Add(new CuratedTown
            {
                GameName = e.GameName, RealName = e.RealName, Latitude = match.Latitude, Longitude = match.Longitude,
                GamePosition = match.GamePosition, Description = e.Description,
                Size = match.Category,
                Inhabitants = match.Population,
                Destruction = Enum.TryParse<DestructionLevel>(e.Destruction, true, out var dl) ? dl : DestructionLevel.Moderate,
                BoundaryPolygon = match.BoundaryPolygon,
            });
        }

        return result;
    }

    private static CuratedTown BuildCuratedTown(
        LlmTownEntry entry, Dictionary<string, TownEntry> townLookup, TownGenerationParams p)
    {
        if (townLookup.TryGetValue(entry.RealName, out var match))
        {
            return new CuratedTown
            {
                GameName = entry.GameName, RealName = entry.RealName, Latitude = match.Latitude, Longitude = match.Longitude,
                GamePosition = match.GamePosition, Description = entry.Description,
                Size = match.Category,
                Inhabitants = match.Population,
                Destruction = Enum.TryParse<DestructionLevel>(entry.Destruction, true, out var dl) ? dl : DestructionLevel.Moderate,
                BoundaryPolygon = match.BoundaryPolygon,
            };
        }

        return new CuratedTown
        {
            GameName = entry.GameName, RealName = entry.RealName,
            Description = entry.Description,
            Size = Enum.TryParse<TownCategory>(entry.Size, true, out var sz) ? sz : TownCategory.Village,
            Inhabitants = entry.Inhabitants,
            Destruction = Enum.TryParse<DestructionLevel>(entry.Destruction, true, out var dl2) ? dl2 : DestructionLevel.Moderate,
        };
    }
}

public record TownEnrichment(string GameName, string Description, DestructionLevel Destruction);
