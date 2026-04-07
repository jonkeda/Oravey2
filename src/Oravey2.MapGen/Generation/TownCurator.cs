using System.Numerics;
using System.Text.Json;
using Oravey2.MapGen.WorldTemplate;

namespace Oravey2.MapGen.Generation;

public sealed class TownCurator
{
    private const int MinTownsPerRegion = 8;
    private const int MaxTownsPerRegion = 15;
    private const double MinSpacingMetres = 15_000;

    private readonly Func<string, CancellationToken, Task<string>> _llmCall;

    public TownCurator(Func<string, CancellationToken, Task<string>> llmCall)
    {
        _llmCall = llmCall;
    }

    public async Task<CuratedRegion> CurateAsync(
        RegionTemplate region,
        int seed,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt(region, seed);

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

        var towns = ParseResponse(response, region);
        Validate(towns);

        var allPositions = towns.Select(t => t.GamePosition).ToArray();
        var boundsMin = new Vector2(allPositions.Min(p => p.X) - 1000, allPositions.Min(p => p.Y) - 1000);
        var boundsMax = new Vector2(allPositions.Max(p => p.X) + 1000, allPositions.Max(p => p.Y) + 1000);

        return new CuratedRegion(region.Name, boundsMin, boundsMax, towns);
    }

    internal static string BuildPrompt(RegionTemplate region, int seed)
    {
        var townList = string.Join("\n", region.Towns.Select(t =>
            $"- {t.Name}: pop {t.Population}, category {t.Category}, lat {t.Latitude:F4} lon {t.Longitude:F4}"));

        return $$"""
            You are creating a post-apocalyptic RPG world. The region is "{{region.Name}}".
            World seed: {{seed}}

            Here are all real towns in this region:
            {{townList}}

            Select {{MinTownsPerRegion}}–{{MaxTownsPerRegion}} towns for the game. For each, provide:
            - gameName: a post-apocalyptic rename (keep recognizable)
            - realName: the original town name
            - latitude, longitude: from the list above
            - role: one of [trading_hub, military_outpost, survivor_camp, raider_den, tech_haven, farming_community, religious_settlement, medical_center]
            - faction: a faction name appropriate for the role
            - threatLevel: 1–10 (ensure a gradient from safe to dangerous)
            - description: 1–2 sentences about the settlement

            Requirements:
            - Include at least one settlement of each threat range: 1–3 (safe), 4–6 (moderate), 7–10 (dangerous)
            - No two selected towns should be closer than ~15 km apart
            - Prefer larger towns but include some small settlements for variety
            - The starting area (largest town) should be threat level 1–2

            Respond with ONLY a JSON array. No markdown, no explanation.
            Example format:
            [
              {"gameName":"Haven","realName":"Purmerend","latitude":52.50,"longitude":4.95,"role":"trading_hub","faction":"Haven Guard","threatLevel":1,"description":"A fortified market town..."}
            ]
            """;
    }

    internal static List<CuratedTown> ParseResponse(string json, RegionTemplate region)
    {
        json = json.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline >= 0) json = json[(firstNewline + 1)..];
            var lastFence = json.LastIndexOf("```");
            if (lastFence >= 0) json = json[..lastFence];
            json = json.Trim();
        }

        var entries = JsonSerializer.Deserialize<List<LlmTownEntry>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("LLM returned invalid JSON for town curation");

        var townLookup = region.Towns.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        var result = new List<CuratedTown>();
        foreach (var e in entries)
        {
            var boundary = townLookup.TryGetValue(e.RealName, out var match) ? match.BoundaryPolygon : null;
            var gamePos = match?.GamePosition ?? new Vector2((float)(e.Longitude * 1000), (float)(e.Latitude * 1000));

            result.Add(new CuratedTown(
                GameName: e.GameName,
                RealName: e.RealName,
                Latitude: e.Latitude,
                Longitude: e.Longitude,
                GamePosition: gamePos,
                Role: e.Role,
                Faction: e.Faction,
                ThreatLevel: Math.Clamp(e.ThreatLevel, 1, 10),
                Description: e.Description,
                BoundaryPolygon: boundary));
        }

        return result;
    }

    internal static void Validate(List<CuratedTown> towns)
    {
        if (towns.Count < MinTownsPerRegion)
            throw new InvalidOperationException($"LLM selected only {towns.Count} towns, minimum is {MinTownsPerRegion}");
        if (towns.Count > MaxTownsPerRegion)
            towns.RemoveRange(MaxTownsPerRegion, towns.Count - MaxTownsPerRegion);

        // Check minimum spacing
        for (int i = 0; i < towns.Count; i++)
        {
            for (int j = i + 1; j < towns.Count; j++)
            {
                var dist = Vector2.Distance(towns[i].GamePosition, towns[j].GamePosition);
                if (dist < MinSpacingMetres)
                {
                    // Remove the smaller/less interesting one
                    towns.RemoveAt(j);
                    j--;
                }
            }
        }
    }

    private sealed class LlmTownEntry
    {
        public string GameName { get; set; } = "";
        public string RealName { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Role { get; set; } = "";
        public string Faction { get; set; } = "";
        public int ThreatLevel { get; set; }
        public string Description { get; set; } = "";
    }
}
