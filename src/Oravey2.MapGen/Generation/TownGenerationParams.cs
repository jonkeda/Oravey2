namespace Oravey2.MapGen.Generation;

public sealed record TownGenerationParams
{
    public required string Genre { get; init; }

    public required string ThemeDescription { get; init; }

    public string SettlementNoun { get; init; } = "settlement";

    public required IReadOnlyList<string> Roles { get; init; }

    public int MinTowns { get; init; } = 8;
    public int MaxTowns { get; init; } = 15;

    public int MinThreat { get; init; } = 1;
    public int MaxThreat { get; init; } = 10;

    public int SafeThreshold { get; init; } = 3;
    public int ModerateThreshold { get; init; } = 6;
    public int StartingTownMaxThreat { get; init; } = 2;

    public string NamingInstruction { get; init; } =
        "a post-apocalyptic rename (keep recognizable)";

    public static readonly TownGenerationParams Apocalyptic = new()
    {
        Genre = "Post-Apocalyptic",
        ThemeDescription = "A ruined civilisation. Survivors fight over scrap, fuel, and clean water.",
        SettlementNoun = "settlement",
        Roles =
        [
            "trading_hub", "military_outpost", "survivor_camp", "raider_den",
            "tech_haven", "farming_community", "religious_settlement", "medical_center"
        ],
        NamingInstruction = "a post-apocalyptic rename (keep recognizable)",
    };

    public static TownGenerationParams LoadFromManifest(string contentPackPath)
    {
        var manifestPath = Path.Combine(contentPackPath, "manifest.json");
        if (!File.Exists(manifestPath))
            return Apocalyptic;

        using var stream = File.OpenRead(manifestPath);
        var doc = System.Text.Json.JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("townGeneration", out var tg))
            return Apocalyptic;

        return new TownGenerationParams
        {
            Genre = tg.GetProperty("genre").GetString()!,
            ThemeDescription = tg.GetProperty("themeDescription").GetString()!,
            SettlementNoun = tg.TryGetProperty("settlementNoun", out var sn)
                                 ? sn.GetString()! : "settlement",
            Roles = tg.GetProperty("roles").EnumerateArray()
                                 .Select(r => r.GetString()!).ToList(),
            NamingInstruction = tg.TryGetProperty("namingInstruction", out var ni)
                                 ? ni.GetString()!
                                 : "a post-apocalyptic rename (keep recognizable)",
            MinTowns = tg.TryGetProperty("minTowns", out var mn) ? mn.GetInt32() : 8,
            MaxTowns = tg.TryGetProperty("maxTowns", out var mx) ? mx.GetInt32() : 15,
        };
    }
}
