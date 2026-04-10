# Step 06b — Discover Mode Improvements

## Problem

When running Mode A ("Discover") the LLM returns a valid JSON array with 8–15
towns, but after `Validate()` only 2 survive. Two root causes were identified:

### Bug 1 — GamePosition scaling vs MinSpacingMetres mismatch

`ParseDiscoverResponse` converts lat/lon to `GamePosition` with:

```csharp
GamePosition = new Vector2(
    (float)(e.Longitude * 1000),   // e.g. 4.75 → 4750
    (float)(e.Latitude  * 1000))   // e.g. 52.35 → 52350
```

`Validate()` then culls any pair closer than `MinSpacingMetres = 15_000`:

```csharp
var dist = Vector2.Distance(towns[i].GamePosition, towns[j].GamePosition);
if (dist < MinSpacingMetres) { towns.RemoveAt(j); j--; }
```

For Dutch towns ~15 km apart (≈ 0.135° latitude), the Vector2 distance is only
**~135 units**, which is far below the 15,000 threshold. Effectively every town
pair within ~15° of each other is culled — i.e. almost everything.

**Impact**: Applies to Discover mode only. Mode B (`ParseResponse`) uses
`match.GamePosition` from the parsed `RegionTemplate`, where positions are
already in proper game units.

### Bug 2 — Copilot agent session pollutes the response

`CopilotLlmService.CallAsync` opens a session with **no system message** and
**no `ExcludedTools`**. The Copilot agent session may:

- Inject its own reasoning/planning text before the JSON
- Invoke built-in tools (`read_file`, `shell`, etc.) and produce tool-call
  events that are not captured but delay the response
- Wrap the requested JSON inside conversational text like
  "Here are the towns: ``` ... ```"

`StripMarkdownFences` handles the last case only partially. Any leading text
before the fence breaks the JSON parse.

---

## Design

### Fix 1 — Correct GamePosition for Discover mode

Replace the naïve `lon * 1000` / `lat * 1000` conversion with a proper
**Haversine-based metre offset** from the region centre. This produces
`GamePosition` values in the same unit space that `MinSpacingMetres` expects.

#### Metre-offset helper (new static method on `TownCurator`)

```csharp
/// <summary>
/// Convert a lat/lon to a Vector2 in metres, relative to a reference point.
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
```

This is an equirectangular projection — good enough for a single region (error
< 0.5% at Netherlands latitudes).

#### Changes to `ParseDiscoverResponse`

Add reference point parameters (region centre):

```csharp
internal static List<CuratedTown> ParseDiscoverResponse(
    string json, double refLat, double refLon)
{
    json = StripMarkdownFences(json);
    var entries = JsonSerializer.Deserialize<List<LlmTownEntry>>(json, JsonOptions)
        ?? throw new InvalidOperationException("...");

    return entries.Select(e => new CuratedTown(
        ...
        GamePosition: LatLonToMetres(e.Latitude, e.Longitude, refLat, refLon),
        ...)).ToList();
}
```

Pass `(southLat + northLat) / 2` and `(westLon + eastLon) / 2` as the
reference from `DiscoverAsync`.

#### Changes to `RerollTownAsync`

Same conversion needs to apply when creating the replacement `CuratedTown`.
Add optional `refLat`/`refLon` parameters (defaulting to 0) and use the same
helper when in Discover mode.

#### Changes to `Validate`

No changes needed — `MinSpacingMetres = 15_000` (15 km) will now correctly
represent 15,000 metres because `GamePosition` is in metres.

### Fix 2 — Use tool-based calling for Discover

Switch from `GetLlmCall()` (plain `CallAsync`) to `GetToolCall(systemMessage)`
(`CallWithToolAsync`) for the town discovery call. This:

1. **Appends a system message** that constrains the LLM to structured output
2. **Adds the `submit_result` tool** — the LLM must call this with its JSON,
   ensuring we get clean data without markdown/prose
3. **Excludes built-in tools** (`edit_file`, `read_file`, `shell`,
   `run_command`) to prevent the agent from doing file I/O

#### Wiring change

In `PipelineWizardViewModel.ConfigureLlmService()` (or constructor), pass
**two** delegates to `TownSelectionStepVM`:

```csharp
var toolSystemMsg = """
    You are a JSON-only RPG town generator. When asked to generate towns,
    produce a JSON array and submit it via the submit_result tool.
    Do NOT read files, run commands, or perform web searches.
    Do NOT include explanation text — only the JSON array via the tool.
    """;

TownSelectionStepVM.SetLlmCall(
    textCall: _llmService.GetLlmCall(),
    toolCall: _llmService.GetToolCall(toolSystemMsg));
```

#### TownSelectionStepViewModel changes

```csharp
private Func<string, CancellationToken, Task<string>>? _llmCall;
private Func<string, CancellationToken, Task<string>>? _toolCall;

public void SetLlmCall(
    Func<string, CancellationToken, Task<string>> textCall,
    Func<string, CancellationToken, Task<string>> toolCall)
{
    _llmCall = textCall;
    _toolCall = toolCall;
}
```

#### TownCurator changes

`DiscoverAsync` and `RerollTownAsync` accept an optional `toolCall` delegate.
When provided, use it instead of the plain text `_llmCall` for calls that need
structured JSON output:

```csharp
public TownCurator(
    Func<string, CancellationToken, Task<string>> llmCall,
    Func<string, CancellationToken, Task<string>>? toolCall = null)
{
    _llmCall = llmCall;
    _toolCall = toolCall;
}
```

In `DiscoverAsync` and `RerollTownAsync`, use `_toolCall ?? _llmCall`.

### Fix 3 — Robust JSON extraction fallback

Even with the tool-based approach, add a defensive JSON extraction to
`ParseDiscoverResponse` that finds the first `[` … last `]` span if
the full text doesn't parse:

```csharp
internal static string ExtractJsonArray(string text)
{
    text = StripMarkdownFences(text);
    var start = text.IndexOf('[');
    var end = text.LastIndexOf(']');
    if (start >= 0 && end > start)
        return text[start..(end + 1)];
    return text; // let the deserializer throw
}
```

### Fix 4 — Retry on insufficient towns

If `Validate()` throws because count < 8 after spacing culling, retry up to
2 times with a reinforced prompt before surfacing the error:

```csharp
// In DiscoverAsync:
for (int attempt = 0; attempt < 3; attempt++)
{
    var response = await call(prompt, ct);
    var towns = ParseDiscoverResponse(response, refLat, refLon);
    try { Validate(towns); return towns; }
    catch when (attempt < 2)
    {
        prompt += "\n\nIMPORTANT: Your previous response had too few towns. " +
                  "You MUST return at least 8 towns spread across the region.";
    }
}
```

---

## Summary of file changes

| File | Change |
|------|--------|
| `TownCurator.cs` | Add `LatLonToMetres()`, update `ParseDiscoverResponse` signature, update `DiscoverAsync` to pass ref coords, accept optional `toolCall`, retry loop, `ExtractJsonArray` |
| `TownSelectionStepViewModel.cs` | Accept and store `_toolCall` delegate, pass to `TownCurator` |
| `PipelineWizardViewModel.cs` | Call `SetLlmCall(textCall, toolCall)` with system message |
| `CopilotLlmService.cs` | No changes (already has `GetToolCall`) |

## Test changes

| File | Change |
|------|--------|
| `TownCuratorTests.cs` | Update `ParseDiscoverResponse` calls to pass `refLat`/`refLon`, add test for `LatLonToMetres`, add test for `ExtractJsonArray`, verify spacing check passes in metre-space |

---

## Risk

- **Equirectangular approximation** is fine for regions ≤ 200 km across.
  Mercator distortion at polar latitudes (> 70°) could cause spacing to be
  off by ~10%. Acceptable for a game-town placement scenario.
- **Tool-based approach** depends on the Copilot SDK agent honouring the
  `submit_result` tool. If the agent ignores it, `CallWithToolAsync` throws
  "LLM did not call submit_result tool." The retry loop in `DiscoverAsync`
  won't help here — but the fallback `textCall` path is still available.
