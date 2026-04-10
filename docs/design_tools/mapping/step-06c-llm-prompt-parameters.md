# Step 06c — LLM Prompt Parameters

## Goal

Extract the hardcoded values from `TownCurator` prompts into a parameter
object so the LLM calls are genre-aware and user-tuneable from the UI.

Currently every prompt bakes in `"post-apocalyptic"`, a fixed roles list,
threat ranges, and town-count limits. A Fantasy content pack would still
generate raider dens and survivor camps.

---

## Parameter Record

```csharp
namespace Oravey2.MapGen.Generation;

/// <summary>
/// Parameters passed to every LLM town-generation prompt.
/// </summary>
public sealed record TownGenerationParams
{
    // --- Genre / theme ---

    /// <summary>Display name of the genre, e.g. "Post-Apocalyptic", "Fantasy".</summary>
    public required string Genre { get; init; }

    /// <summary>1–2 sentence flavour text injected into the system/prompt preamble.</summary>
    public required string ThemeDescription { get; init; }

    /// <summary>What to call settlements — "settlement", "village", "outpost", etc.</summary>
    public string SettlementNoun { get; init; } = "settlement";

    // --- Settlement roles ---

    /// <summary>
    /// Allowed roles the LLM may assign. Must contain at least 4 entries.
    /// Rendered as a bracketed list in the prompt.
    /// </summary>
    public required IReadOnlyList<string> Roles { get; init; }

    // --- Town counts ---

    public int MinTowns { get; init; } = 8;
    public int MaxTowns { get; init; } = 15;

    // --- Spacing ---

    /// <summary>Minimum distance between towns in metres (used in Validate).</summary>
    public double MinSpacingMetres { get; init; } = 15_000;

    // --- Threat ---

    public int MinThreat { get; init; } = 1;
    public int MaxThreat { get; init; } = 10;

    /// <summary>Upper bound of the "safe" tier (inclusive).</summary>
    public int SafeThreshold { get; init; } = 3;

    /// <summary>Upper bound of the "moderate" tier (inclusive).</summary>
    public int ModerateThreshold { get; init; } = 6;

    /// <summary>Max threat level for the starting / largest town.</summary>
    public int StartingTownMaxThreat { get; init; } = 2;

    // --- Naming style ---

    /// <summary>
    /// Instruction for how to rename real places, e.g.
    /// "a post-apocalyptic rename (keep recognizable)" or
    /// "a fantasy rename using archaic English".
    /// </summary>
    public string NamingInstruction { get; init; } =
        "a post-apocalyptic rename (keep recognizable)";
}
```

---

## Presets per content pack

Add an optional `townGeneration` section to the content-pack manifest. This
keeps game-design choices next to the art and scenario data they belong with.

### Post-Apocalyptic (`oravey2.apocalyptic/manifest.json`)

```jsonc
"townGeneration": {
  "genre": "Post-Apocalyptic",
  "themeDescription": "A ruined civilisation. Survivors fight over scrap, fuel, and clean water. Nature reclaims the ruins.",
  "settlementNoun": "settlement",
  "roles": [
    "trading_hub", "military_outpost", "survivor_camp", "raider_den",
    "tech_haven", "farming_community", "religious_settlement", "medical_center"
  ],
  "namingInstruction": "a post-apocalyptic rename (keep recognizable)"
}
```

### Fantasy (`oravey2.fantasy/manifest.json`)

```jsonc
"townGeneration": {
  "genre": "Fantasy",
  "themeDescription": "A medieval realm of magic and swordcraft. Kingdoms vie for power while dark forces stir in the wild.",
  "settlementNoun": "settlement",
  "roles": [
    "market_town", "fortress", "wizard_tower", "thieves_guild",
    "temple_city", "farming_village", "mining_outpost", "harbour"
  ],
  "namingInstruction": "a fantasy rename using archaic or elvish-sounding words"
}
```

When no `townGeneration` section is present, fall back to the current
apocalyptic defaults so existing packs keep working.

---

## Loading

`PipelineWizardViewModel` already reads the content-pack manifest path from
`PipelineState.ContentPackPath`. Add a small helper:

```csharp
internal static TownGenerationParams LoadFromManifest(string contentPackPath)
{
    var manifestPath = Path.Combine(contentPackPath, "manifest.json");
    if (!File.Exists(manifestPath))
        return Defaults.Apocalyptic;

    using var stream = File.OpenRead(manifestPath);
    var doc = JsonDocument.Parse(stream);

    if (!doc.RootElement.TryGetProperty("townGeneration", out var tg))
        return Defaults.Apocalyptic;

    return new TownGenerationParams
    {
        Genre            = tg.GetProperty("genre").GetString()!,
        ThemeDescription = tg.GetProperty("themeDescription").GetString()!,
        SettlementNoun   = tg.TryGetProperty("settlementNoun", out var sn)
                               ? sn.GetString()! : "settlement",
        Roles            = tg.GetProperty("roles").EnumerateArray()
                               .Select(r => r.GetString()!).ToList(),
        NamingInstruction = tg.TryGetProperty("namingInstruction", out var ni)
                               ? ni.GetString()!
                               : "a post-apocalyptic rename (keep recognizable)",
    };
}
```

---

## Prompt integration

### `BuildDiscoverPrompt` — before (hardcoded)

```
You are creating a post-apocalyptic RPG world. The region is "{regionName}".
...
- role: one of [trading_hub, military_outpost, ...]
```

### `BuildDiscoverPrompt` — after (parameterised)

```csharp
internal static string BuildDiscoverPrompt(
    string regionName,
    double southLat, double westLon,
    double northLat, double eastLon,
    int seed,
    TownGenerationParams p)
{
    var roleList = string.Join(", ", p.Roles);

    return $$"""
        You are creating a {{p.Genre}} RPG world. {{p.ThemeDescription}}
        The region is "{{regionName}}".
        Bounding box: south {{southLat:F4}}, west {{westLon:F4}}, north {{northLat:F4}}, east {{eastLon:F4}}.
        World seed: {{seed}}

        Using your own knowledge of this region, INVENT {{p.MinTowns}}–{{p.MaxTowns}} real-world locations
        that would be interesting {{p.SettlementNoun}}s. Include cities, towns, and smaller places.
        Provide accurate real-world latitude/longitude for each.

        For each location, provide:
        - gameName: {{p.NamingInstruction}}
        - realName: the real-world place name
        - latitude, longitude: accurate coordinates within the bounding box
        - role: one of [{{roleList}}]
        - faction: a faction name appropriate for the role
        - threatLevel: {{p.MinThreat}}–{{p.MaxThreat}} (ensure a gradient from safe to dangerous)
        - description: 1–2 sentences about the {{p.SettlementNoun}}
        - estimatedPopulation: approximate real-world population

        Requirements:
        - At least one {{p.SettlementNoun}} in each threat range: {{p.MinThreat}}–{{p.SafeThreshold}} (safe), {{p.SafeThreshold + 1}}–{{p.ModerateThreshold}} (moderate), {{p.ModerateThreshold + 1}}–{{p.MaxThreat}} (dangerous)
        - No two locations closer than ~{{p.MinSpacingMetres / 1000:F0}} km apart
        - Mix of large and small {{p.SettlementNoun}}s
        - The starting area (largest town) should be threat level {{p.MinThreat}}–{{p.StartingTownMaxThreat}}

        Respond with ONLY a JSON array. No markdown, no explanation.
        """;
}
```

Same pattern applies to `BuildPrompt` (Mode B) and `RerollTownAsync`.

### `Validate` — use params instead of constants

```csharp
internal static void Validate(List<CuratedTown> towns, TownGenerationParams p)
{
    if (towns.Count < p.MinTowns)
        throw new InvalidOperationException(
            $"LLM selected only {towns.Count} towns, minimum is {p.MinTowns}");
    if (towns.Count > p.MaxTowns)
        towns.RemoveRange(p.MaxTowns, towns.Count - p.MaxTowns);

    for (int i = 0; i < towns.Count; i++)
        for (int j = i + 1; j < towns.Count; j++)
        {
            var dist = Vector2.Distance(towns[i].GamePosition, towns[j].GamePosition);
            if (dist < p.MinSpacingMetres)
            {
                towns.RemoveAt(j);
                j--;
            }
        }
}
```

---

## TownCurator constructor change

```csharp
public sealed class TownCurator
{
    private readonly Func<string, CancellationToken, Task<string>> _llmCall;
    private readonly TownGenerationParams _params;

    public TownCurator(
        Func<string, CancellationToken, Task<string>> llmCall,
        TownGenerationParams? generationParams = null)
    {
        _llmCall = llmCall;
        _params = generationParams ?? Defaults.Apocalyptic;
    }
}
```

All three public methods (`CurateAsync`, `DiscoverAsync`, `RerollTownAsync`)
thread `_params` through to their respective `Build*Prompt` and `Validate`
calls instead of referencing the old `const` fields.

---

## VM / wiring

`TownSelectionStepViewModel` gains a `TownGenerationParams` property set
during `Initialize`:

```csharp
public void Initialize(PipelineState state, string dataRoot,
                        TownGenerationParams generationParams)
{
    _state = state;
    _dataRoot = dataRoot;
    _generationParams = generationParams;
}
```

`PipelineWizardViewModel` loads params from the manifest and passes them in.

---

## UI exposure (optional, future)

The parameters object makes it easy to add an "Advanced" expander on the
Town Selection step later:

| Control | Bound property |
|---------|---------------|
| Stepper | `MinTowns` / `MaxTowns` |
| Slider  | `MinSpacingMetres` (5–50 km) |
| Editor  | `NamingInstruction` |
| Chips   | `Roles` (add / remove) |

Not required for step-06c — just storing the params and threading them
through the prompts is sufficient.

---

## Defaults

```csharp
public static class Defaults
{
    public static readonly TownGenerationParams Apocalyptic = new()
    {
        Genre = "Post-Apocalyptic",
        ThemeDescription = "A ruined civilisation. Survivors fight over scrap, fuel, and clean water.",
        SettlementNoun = "settlement",
        Roles = [
            "trading_hub", "military_outpost", "survivor_camp", "raider_den",
            "tech_haven", "farming_community", "religious_settlement", "medical_center"
        ],
        NamingInstruction = "a post-apocalyptic rename (keep recognizable)",
    };
}
```

---

## File changes

| File | Change |
|------|--------|
| `TownGenerationParams.cs` (new) | Record + `Defaults` class |
| `TownCurator.cs` | Accept `TownGenerationParams`, replace constants and hardcoded strings |
| `TownSelectionStepViewModel.cs` | Accept and store `TownGenerationParams`, pass to `TownCurator` |
| `PipelineWizardViewModel.cs` | Load params from manifest, pass to step VM |
| `manifest.json` (Apocalyptic) | Add `townGeneration` section |
| `manifest.json` (Fantasy) | Add `townGeneration` section |

## Test changes

| File | Change |
|------|--------|
| `TownCuratorTests.cs` | Update `BuildDiscoverPrompt` / `BuildPrompt` / `Validate` calls to pass `TownGenerationParams`, add test for Fantasy roles showing up in prompt text |
