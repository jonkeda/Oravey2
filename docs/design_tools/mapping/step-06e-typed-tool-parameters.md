# Step 06e — Typed AIFunction Parameters

## Background

The `submit_towns` and `submit_town` AIFunctions in `TownCurator` currently
accept a `string json` parameter that is manually deserialized. This is
fragile — the LLM can produce malformed JSON that doesn't match the expected
schema, leading to runtime `JsonException`s.

`AIFunctionFactory.Create` supports complex typed parameters natively. When a
parameter is `List<T>` or a class, the factory generates the correct JSON
schema and the framework deserializes the LLM's response into the .NET type
automatically. This eliminates an entire class of errors.

Changes needed:

1. Extract `LlmTownEntry` from a `private sealed class` to an `internal`
   top-level class so it can be used as an AIFunction parameter type.
2. Change `submit_towns` to accept `List<LlmTownEntry>` instead of `string`.
3. Change `submit_town` to accept `LlmTownEntry` instead of `string`.
4. Remove the manual `JsonSerializer.Deserialize` calls inside tool lambdas.
5. Update tests that simulate tool invocation to pass typed objects instead of
   JSON strings via `AIFunctionArguments`.

---

## Change 1 — Extract `LlmTownEntry` to a standalone internal class

### Problem

`LlmTownEntry` is `private sealed class` nested inside `TownCurator`. It
cannot be referenced by `AIFunctionFactory.Create` for schema generation
since the factory needs to expose the type in the generated JSON schema, and
it cannot be used in test code either.

### Design

Move `LlmTownEntry` out of `TownCurator` into its own file
`Generation/LlmTownEntry.cs`. Make it `internal` so tests (via
`InternalsVisibleTo`) and the AIFunction factory can reference it. Remove
`Latitude`, `Longitude`, and `EstimatedPopulation` properties since Discover
mode looks those up from the template — the LLM should not provide them.

```csharp
// Generation/LlmTownEntry.cs
using System.ComponentModel;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Schema class for LLM tool calls. Properties map to the JSON schema
/// that AIFunctionFactory exposes to the model.
/// </summary>
internal sealed class LlmTownEntry
{
    [Description("A thematic name for the settlement")]
    public string GameName { get; set; } = "";

    [Description("Must match a town name from the available list exactly")]
    public string RealName { get; set; } = "";

    [Description("Settlement role, e.g. trading_hub, military_outpost")]
    public string Role { get; set; } = "";

    [Description("Faction name appropriate for the role")]
    public string Faction { get; set; } = "";

    [Description("Threat level (1 = safe, 10 = deadly)")]
    public int ThreatLevel { get; set; }

    [Description("1–2 sentence description of the settlement")]
    public string Description { get; set; } = "";
}
```

`[Description]` attributes are picked up by `AIFunctionFactory` and included
in the JSON schema exposed to the LLM, giving it clear guidance per field.

`TownCurator` keeps a reference to `LlmTownEntry` internally — the Mode B
(`CurateAsync`) text-based fallback still deserializes from raw JSON using
this same type, but Mode B's `ParseResponse` will need to handle the extra
`Latitude`/`Longitude` fields in the JSON (they're ignored since lookup is
from the template). Use `JsonExtensionData` or simply keep the properties
as optional but do not add `[Description]` to them — they won't appear in
the tool schema.

**Decision**: Keep `Latitude` and `Longitude` on `LlmTownEntry` (without
`[Description]`) so `ParseResponse` (Mode B text fallback) can still
deserialize them. The tool schema won't advertise them, but the text fallback
still works.

Revised class:

```csharp
internal sealed class LlmTownEntry
{
    [Description("A thematic name for the settlement")]
    public string GameName { get; set; } = "";

    [Description("Must match a town name from the available list exactly")]
    public string RealName { get; set; } = "";

    [Description("Settlement role, e.g. trading_hub, military_outpost")]
    public string Role { get; set; } = "";

    [Description("Faction name appropriate for the role")]
    public string Faction { get; set; } = "";

    [Description("Threat level (1 = safe, 10 = deadly)")]
    public int ThreatLevel { get; set; }

    [Description("1–2 sentence description of the settlement")]
    public string Description { get; set; } = "";

    // Used by Mode B text fallback only — not exposed in tool schema
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
```

---

## Change 2 — Typed `submit_towns` in `DiscoverAsync`

### Problem

Current code:

```csharp
var submitTool = AIFunctionFactory.Create(
    ([Description("JSON array of town objects")] string json) =>
    {
        _log?.Invoke("← Received", json);
        captured = ParseDiscoverResponse(json, region, _params);
        return "Accepted.";
    },
    "submit_towns",
    "Submit the selected towns as a JSON array. Call this exactly once.");
```

The LLM receives a schema saying the parameter is `string`. It can put
anything in it. Deserialization failures happen at runtime.

### Design

Replace with a typed `List<LlmTownEntry>` parameter:

```csharp
var submitTool = AIFunctionFactory.Create(
    ([Description("The selected towns")] List<LlmTownEntry> towns) =>
    {
        _log?.Invoke("← Received", JsonSerializer.Serialize(towns, JsonOptions));
        captured = BuildCuratedTowns(towns, region, _params);
        return "Accepted.";
    },
    "submit_towns",
    "Submit the selected towns. Call this exactly once.");
```

The framework now generates a JSON schema like:

```json
{
  "type": "object",
  "properties": {
    "towns": {
      "type": "array",
      "description": "The selected towns",
      "items": {
        "type": "object",
        "properties": {
          "gameName": { "type": "string", "description": "A thematic name..." },
          "realName": { "type": "string", "description": "Must match..." },
          ...
        }
      }
    }
  }
}
```

The LLM sees the full schema per-field. `AIFunctionFactory` deserializes the
array into `List<LlmTownEntry>` before invoking the lambda. If the LLM
produces malformed JSON, the framework returns an error to the LLM so it can
retry — our code never sees invalid data.

**New helper** `BuildCuratedTowns` replaces `ParseDiscoverResponse` for the
tool path (the text-fallback path still uses `ParseDiscoverResponse` with raw
JSON):

```csharp
private static List<CuratedTown> BuildCuratedTowns(
    List<LlmTownEntry> entries, RegionTemplate region, TownGenerationParams p)
{
    var townLookup = BuildTownLookup(region);
    var result = new List<CuratedTown>();

    foreach (var e in entries)
    {
        if (!townLookup.TryGetValue(e.RealName, out var match))
            continue;

        result.Add(new CuratedTown(
            e.GameName, e.RealName, match.Latitude, match.Longitude,
            match.GamePosition, e.Role, e.Faction,
            Math.Clamp(e.ThreatLevel, p.MinThreat, p.MaxThreat),
            e.Description, match.BoundaryPolygon));
    }

    return result;
}
```

---

## Change 3 — Typed `submit_town` in `RerollTownAsync`

### Problem

Same issue — `string json` parameter with manual deserialization.

### Design

```csharp
var submitTool = AIFunctionFactory.Create(
    ([Description("The replacement town")] LlmTownEntry town) =>
    {
        _log?.Invoke("← Received", JsonSerializer.Serialize(town, JsonOptions));
        captured = BuildCuratedTown(town, townLookup, p);
        return "Accepted.";
    },
    "submit_town",
    "Submit the replacement town.");
```

No other changes needed — `BuildCuratedTown` already accepts `LlmTownEntry`.

---

## Change 4 — Update test tool invocations

### Problem

Tests simulate the LLM calling the tool via `AIFunctionArguments` with a
`{ ["json"] = fakeJson }` dictionary. With typed parameters, the key changes
to `"towns"` (for `submit_towns`) or `"town"` (for `submit_town`) and the
value should be a `JsonElement` or the serialized list.

### Design

Test helper changes:

```csharp
// Before
await tool.InvokeAsync(new AIFunctionArguments(
    new Dictionary<string, object?> { ["json"] = fakeJson }), ct);

// After — submit_towns
var entries = Enumerable.Range(0, 10).Select(i => new LlmTownEntry
{
    GameName = $"T{i}", RealName = $"Town{i}", Role = "trading_hub",
    Faction = $"F{i}", ThreatLevel = Math.Clamp(i + 1, 1, 10),
    Description = "d",
}).ToList();

// AIFunctionFactory deserializes JsonElement → List<LlmTownEntry>
var jsonElement = JsonSerializer.SerializeToElement(entries);
await tool.InvokeAsync(new AIFunctionArguments(
    new Dictionary<string, object?> { ["towns"] = jsonElement }), ct);

// After — submit_town
var entry = new LlmTownEntry { ... };
var jsonElement = JsonSerializer.SerializeToElement(entry);
await tool.InvokeAsync(new AIFunctionArguments(
    new Dictionary<string, object?> { ["town"] = jsonElement }), ct);
```

---

## Summary of file changes

| File | Change |
|------|--------|
| `Generation/LlmTownEntry.cs` | **New file** — extracted from `TownCurator`, add `[Description]` attrs |
| `Generation/TownCurator.cs` | Remove nested `LlmTownEntry` class. Change `submit_towns` lambda to accept `List<LlmTownEntry>`. Change `submit_town` lambda to accept `LlmTownEntry`. Add `BuildCuratedTowns` helper. Log serialized entries instead of raw JSON string. |
| `TownCuratorDiscoverTests.cs` | Update tool invocation tests: pass `JsonElement` with key `"towns"` instead of `"json"`. Use `LlmTownEntry` objects. |
| `TownCuratorTests.cs` | Update any tool invocation tests for `submit_town` similarly. |

---

## Risk

- **AIFunctionFactory schema generation**: The factory must be able to
  serialize `LlmTownEntry` to JSON schema. This is standard — flat POCO
  with primitive properties. No risk.
- **`[Description]` on properties**: Only affects the tool schema sent to the
  LLM. If the LLM ignores descriptions, behaviour is unchanged — the schema
  structure (`type`, `properties`) already constrains the response.
- **Text-based fallback**: `ParseResponse` and `ParseDiscoverResponse` still
  deserialize from raw JSON strings. They continue to use
  `JsonSerializer.Deserialize<List<LlmTownEntry>>` with the same class.
  `Latitude`/`Longitude` properties without `[Description]` are harmless.
- **Test breakage**: Only the `AIFunctionArguments` key name and value type
  change. The test structure is identical.
