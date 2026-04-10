# Step 06d — Curator Simplify & LLM Logging

## Background

Step 06b introduced several fixes (LatLonToMetres, ExtractJson*, retry loop,
tool-based calling). After testing, several changes are needed:

1. The LLM invents inaccurate lat/lon — look them up from the parsed template.
2. The `submit_result` tool is buried inside `CopilotLlmService` — expose an
   AIFunction callback so domain code controls the tool definition.
3. Min/max spacing validation causes more problems than it solves — remove it.
4. There is no visibility into what is sent/received from the LLM.
5. `ExtractJsonArray` / `ExtractJsonObject` (Fix 3) are unnecessary with
   tool-based calling — remove.
6. The retry loop (Fix 4) adds complexity without value — remove.

---

## Change 1 — Look up lat/lon from the template by realName

### Problem

In Discover mode the LLM fabricates lat/lon values. These are often inaccurate
(wrong side of the bounding box, or outside it entirely). The parsed
`RegionTemplate` already contains accurate lat/lon for every OSM town.

### Design

**Discover mode now requires the parsed `RegionTemplate`**, same as Mode B.

`DiscoverAsync` signature changes:

```csharp
public async Task<List<CuratedTown>> DiscoverAsync(
    RegionTemplate region,
    int seed,
    CancellationToken ct = default)
```

The prompt no longer asks the LLM for lat/lon. Instead it provides the list of
real town names (from the template) and asks the LLM to pick from them — similar
to Mode B but the LLM chooses which towns are interesting (invents game names,
roles, factions, etc.) rather than being given the full data.

`ParseDiscoverResponse` does a case-insensitive lookup on `region.Towns` by
`Name` to get lat, lon, `GamePosition`, and `BoundaryPolygon`.  
If a `realName` doesn't match any town in the template, the entry is **skipped**
(logged as unmatched).

The `LlmTownEntry` class drops the `Latitude`, `Longitude`, and
`EstimatedPopulation` properties for Discover mode responses.

`RerollTownAsync` likewise takes `RegionTemplate` and looks up coordinates by
realName.

`LatLonToMetres` is no longer needed for Discover mode (template already has
`GamePosition`). It can remain as an internal utility but is not called in the
main Discover flow.

### Prompt change (Discover)

The prompt changes from:

> "INVENT 8–15 real-world locations … Provide accurate real-world
> latitude/longitude for each."

to:

> "Here are all real towns in this region: [list from template].
> Pick 8–15 that would make interesting settlements. For each provide:
> gameName, realName (must match a name from the list), role, faction,
> threatLevel, description."

The LLM response JSON no longer contains `latitude`, `longitude`, or
`estimatedPopulation` fields.

---

## Change 2 — AIFunction callback for structured LLM output

### Problem

The `submit_result` AIFunction is currently created inside
`CopilotLlmService.CallWithToolAsync`. Domain code (TownCurator) has no control
over the tool name, description, or parameter schema. The service and domain
logic are coupled.

### Design

Introduce a new method on `CopilotLlmService`:

```csharp
/// <summary>
/// Runs a session with caller-supplied tools. Returns when the session goes idle.
/// The caller captures results via the AIFunction callbacks.
/// </summary>
public async Task CallWithToolsAsync(
    string systemMessage,
    string prompt,
    IList<AIFunction> tools,
    CancellationToken ct)
```

This method:
- Builds a `SessionConfig` with `SystemMessage`, `Tools = tools`,
  `ExcludedTools`
- Sends the prompt
- Waits for `SessionIdleEvent` or error
- Does **not** return a string — the caller's AIFunction callback captures the
  result

The existing `CallWithToolAsync` (singular) can be kept as convenience or
removed.

**Delegate type changes:**

Replace `Func<string, CancellationToken, Task<string>>` with a new delegate
type or keep the tool-based approach internal. The cleanest approach:

```csharp
// In TownCurator:
private readonly Func<string, IList<AIFunction>, CancellationToken, Task> _llmCallWithTools;
```

The TownCurator creates its own `submit_towns` AIFunction:

```csharp
List<CuratedTown>? captured = null;

var submitTool = AIFunctionFactory.Create(
    ([Description("JSON array of town objects")] string json) =>
    {
        captured = ParseDiscoverResponse(json, region);
        return "Accepted.";
    },
    "submit_towns",
    "Submit the selected towns as a JSON array.");

await _llmCallWithTools(prompt, [submitTool], ct);

return captured ?? throw new InvalidOperationException("LLM did not call submit_towns.");
```

**CopilotLlmService** exposes:

```csharp
public Func<string, IList<AIFunction>, CancellationToken, Task>
    GetToolCallDelegate(string systemMessage)
    => (prompt, tools, ct) => CallWithToolsAsync(systemMessage, prompt, tools, ct);
```

**Wiring in PipelineWizardViewModel:**

```csharp
TownSelectionStepVM.SetLlmCall(
    _llmService.GetLlmCall(),                      // text-only fallback
    _llmService.GetToolCallDelegate(toolSystemMsg)  // tool-based
);
```

---

## Change 3 — Remove spacing validation

### Problem

The `MinSpacingMetres` check in `Validate()` culls towns that are close
together. In practice, real towns near each other are intentionally interesting
(e.g. Zaandam and Amsterdam). The game can handle nearby settlements.

### Design

- Remove the spacing `for` loop from `Validate()`.
- Remove `MinSpacingMetres` from `TownGenerationParams`.
- Remove the spacing instruction from `BuildPrompt` and `BuildDiscoverPrompt`.
- Remove `CheckSpacing` from `TownSelectionStepViewModel.ValidationSummary`.
- Keep `MinTowns`/`MaxTowns` validation (count check only).

---

## Change 4 — LLM message log

### Problem

There is no visibility into what prompts are sent to the LLM and what responses
come back. Debugging LLM issues requires guessing.

### Design

Add an `ObservableCollection<LlmLogEntry>` to `TownSelectionStepViewModel`:

```csharp
public record LlmLogEntry(DateTime Timestamp, string Direction, string Content);

public ObservableCollection<LlmLogEntry> LlmLog { get; } = [];
```

`Direction` is `"→ Sent"` or `"← Received"`.

The ViewModel wraps the LLM delegate to intercept calls:

```csharp
private Func<string, CancellationToken, Task<string>> WrapWithLogging(
    Func<string, CancellationToken, Task<string>> inner)
{
    return async (prompt, ct) =>
    {
        LlmLog.Add(new(DateTime.Now, "→ Sent", prompt));
        var result = await inner(prompt, ct);
        LlmLog.Add(new(DateTime.Now, "← Received", result));
        return result;
    };
}
```

For the tool-based delegate, similar wrapping — log the prompt on send, log the
captured tool-call argument on receive.

**UI**: Add a collapsible `CollectionView` below the town list in
`TownSelectionStepView.xaml`:

```xml
<Label Text="LLM Log" FontAttributes="Bold" />
<CollectionView ItemsSource="{Binding LlmLog}" MaximumHeightRequest="300">
    <CollectionView.ItemTemplate>
        <DataTemplate>
            <VerticalStackLayout Padding="4">
                <Label Text="{Binding Direction}" FontAttributes="Bold"
                       TextColor="{StaticResource Primary}" FontSize="12" />
                <Label Text="{Binding Content}" FontSize="11"
                       FontFamily="Consolas" LineBreakMode="WordWrap" />
            </VerticalStackLayout>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

---

## Change 5 — Remove ExtractJsonArray / ExtractJsonObject (Fix 3)

With tool-based calling the LLM submits clean JSON via the AIFunction. The
`ExtractJsonArray` and `ExtractJsonObject` methods in `TownCurator` are no
longer needed.

- Delete `ExtractJsonArray` method.
- Delete `ExtractJsonObject` method.
- Remove calls to them in `ParseResponse`, `ParseDiscoverResponse`,
  `RerollTownAsync`.
- Keep `StripMarkdownFences` — still useful for text-mode fallback.
- Update tests that reference these methods.

---

## Change 6 — Remove retry loop (Fix 4)

The 3-attempt retry loop in `DiscoverAsync` adds complexity. The user can simply
click "Run LLM" again if the result is unsatisfactory.

- Replace the `for (int attempt = 0; attempt < 3; …)` loop with a single call.
- Remove the prompt-reinforcement logic.

---

## Summary of file changes

| File | Change |
|------|--------|
| `TownCurator.cs` | Change `DiscoverAsync` to accept `RegionTemplate`, lookup lat/lon by realName, accept `Func<string, IList<AIFunction>, CancellationToken, Task>`, remove `ExtractJsonArray`/`ExtractJsonObject`, remove retry loop, remove spacing from `Validate` |
| `TownGenerationParams.cs` | Remove `MinSpacingMetres` |
| `TownSelectionStepViewModel.cs` | New `LlmLog` collection, `WrapWithLogging`, update delegate types, remove `CheckSpacing` from validation, pass `RegionTemplate` to `DiscoverAsync` |
| `CopilotLlmService.cs` | Add `CallWithToolsAsync` accepting `IList<AIFunction>`, add `GetToolCallDelegate` |
| `PipelineWizardViewModel.cs` | Wire new delegate type |
| `TownSelectionStepView.xaml` | Add LLM Log section |
| `TownCuratorTests.cs` | Remove ExtractJson* tests, remove spacing tests, update Discover tests for template-based lookup |
| `TownCuratorDiscoverTests.cs` | Same |

---

## Risk

- **Discover mode now requires a parsed template**: The user must complete
  step 3 (Parse) before running Discover. This is already enforced by the
  pipeline step order. If no template is loaded, show an error.
- **Town name mismatch**: The LLM may return a `realName` that doesn't exactly
  match the template. Use case-insensitive comparison and log unmatched names.
  Future: add fuzzy matching or Levenshtein distance.
