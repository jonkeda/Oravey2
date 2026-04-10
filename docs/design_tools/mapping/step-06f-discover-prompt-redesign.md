# Step 06f — Discover Prompt Redesign

## Background

`BuildDiscoverPrompt` was originally written for the text-based LLM path
where the model had to return a JSON array in its response. Now that
Discover mode uses typed AIFunctions (`submit_towns` with
`List<LlmTownEntry>`), the prompt has multiple problems:

1. **Town list in the prompt is redundant.** The LLM (GitHub Copilot) has
   world knowledge about the region. Passing 100+ towns as a bulleted list
   wastes tokens. The LLM should pick settlements it knows exist in the
   real-world region. We already handle unmatched names by skipping them in
   `BuildCuratedTowns`, so stale picks are harmless.

2. **"Respond with a JSON array" is wrong.** The tool path doesn't read the
   text response — it captures data via the `submit_towns` AIFunction. The
   prompt should tell the LLM to call the function, not format JSON.

3. **World seed serves no purpose.** LLMs are non-deterministic. Passing a
   seed number does nothing for reproducibility. Remove it from the Discover
   prompt. (Keep it in Mode B `BuildPrompt` where it's used as a creative
   randomiser — but that's a separate concern.)

4. **"towns" is too narrow.** The template contains hamlets, villages, towns,
   and cities. The prompt should say "settlements" (or use `SettlementNoun`)
   and explicitly let the LLM pick any size.

5. **`ParseDiscoverResponse` is only for the text fallback.** The tool path
   now goes through `BuildCuratedTowns(List<LlmTownEntry>, ...)`. The
   text-based fallback (and `ParseDiscoverResponse`) can be removed from
   Discover mode entirely — if there's no `_toolCall`, throw
   `InvalidOperationException`. This simplifies the code and removes a code
   path that nobody uses in production.

---

## Change 1 — Rewrite `BuildDiscoverPrompt` for tool-calling

### Problem

Current prompt dumps the full town list and asks for a JSON array response:

```csharp
Here are all real towns available in this region:
{{townList}}

Pick {{p.MinTowns}}–{{p.MaxTowns}} towns ...
Respond with ONLY a JSON array. No markdown, no explanation.
```

### Design

New prompt relies on the LLM's world knowledge and instructs it to call the
`submit_towns` function:

```csharp
internal static string BuildDiscoverPrompt(RegionTemplate region, int seed, TownGenerationParams p)
{
    var roleList = string.Join(", ", p.Roles);

    return $$"""
        You are creating a {{p.Genre}} RPG world. {{p.ThemeDescription}}
        The region is "{{region.Name}}".

        Pick {{p.MinTowns}}–{{p.MaxTowns}} real-world settlements from this region.
        They can be any size — hamlets, villages, towns, or cities.
        Choose a mix that would make an interesting {{p.Genre}} game world.

        For each {{p.SettlementNoun}}:
        - gameName: {{p.NamingInstruction}}
        - realName: the real-world name of the settlement
        - role: one of [{{roleList}}]
        - faction: a faction name appropriate for the role
        - threatLevel: {{p.MinThreat}}–{{p.MaxThreat}} (ensure a gradient from safe to dangerous)
        - description: 1–2 sentences about the {{p.SettlementNoun}}

        Requirements:
        - Spread across threat ranges: {{p.MinThreat}}–{{p.SafeThreshold}} (safe), {{p.SafeThreshold + 1}}–{{p.ModerateThreshold}} (moderate), {{p.ModerateThreshold + 1}}–{{p.MaxThreat}} (dangerous)
        - Include a mix of settlement sizes for variety
        - The largest settlement should be threat level {{p.MinThreat}}–{{p.StartingTownMaxThreat}} (starting area)

        Call the submit_towns function with your selections.
        """;
}
```

Key differences:
- No town list — saves tokens, lets LLM use world knowledge.
- No seed — removed.
- "settlements" and "any size" — not restricted to towns.
- "Call the submit_towns function" — explicit instruction.
- No JSON format example — the `LlmTownEntry` schema on the AIFunction
  already tells the LLM what fields to provide.

The `seed` parameter stays on the method signature for API compatibility
but is no longer included in the prompt. `DiscoverAsync` callers don't
need to change.

---

## Change 2 — Remove the text-based fallback from `DiscoverAsync`

### Problem

`DiscoverAsync` has two code paths: the `_toolCall` path (typed
AIFunction) and a text-based fallback using `_llmCall` + 
`ParseDiscoverResponse`. The text fallback:
- Is never used in production (the app always provides `_toolCall`).
- Requires maintaining `ParseDiscoverResponse`.
- Produces worse results (no schema enforcement).

### Design

Remove the fallback. `DiscoverAsync` requires `_toolCall`:

```csharp
public async Task<List<CuratedTown>> DiscoverAsync(
    RegionTemplate region,
    int seed,
    CancellationToken ct = default)
{
    if (_toolCall is null)
        throw new InvalidOperationException(
            "DiscoverAsync requires tool-calling support.");

    var prompt = BuildDiscoverPrompt(region, seed, _params);
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

    return captured
        ?? throw new InvalidOperationException("LLM did not call submit_towns tool.");
}
```

---

## Change 3 — Remove `ParseDiscoverResponse`

### Problem

`ParseDiscoverResponse` exists solely for the text-based fallback in
Discover mode. With the fallback removed, it has no callers.

### Design

Delete `ParseDiscoverResponse`. The tool path uses `BuildCuratedTowns`
(which takes `List<LlmTownEntry>` directly). Tests that exercise
`ParseDiscoverResponse` should be rewritten to test `BuildCuratedTowns`
instead, or test the full `DiscoverAsync` flow through the tool delegate.

Make `BuildCuratedTowns` `internal static` so tests can call it directly:

```csharp
internal static List<CuratedTown> BuildCuratedTowns(
    List<LlmTownEntry> entries, RegionTemplate region, TownGenerationParams p)
```

---

## Change 4 — Update `LlmTownEntry.RealName` description

### Problem

The `[Description]` on `RealName` says "Must match a town name from the
available list exactly" — but there is no list in the prompt any more.

### Design

```csharp
[Description("The real-world name of the settlement")]
public string RealName { get; set; } = "";
```

---

## Change 5 — Update tests

### Problem

- `ParseDiscoverResponse_*` tests no longer have a target method.
- `DiscoverAsync_CallsLlmAndReturnsTowns` tests the text fallback path.
- `BuildDiscoverPrompt_*` tests check for town list content.

### Design

| Test | Action |
|------|--------|
| `ParseDiscoverResponse_LooksUpFromTemplate` | Rewrite → test `BuildCuratedTowns` with `List<LlmTownEntry>` |
| `ParseDiscoverResponse_SkipsUnmatchedNames` | Rewrite → test `BuildCuratedTowns` |
| `ParseDiscoverResponse_HandlesMarkdownFences` | Delete (no raw JSON to strip) |
| `ParseDiscoverResponse_ClampsThreatLevel` | Rewrite → test `BuildCuratedTowns` |
| `ParseDiscoverResponse_UsesTemplateBoundaryPolygon` | Rewrite → test `BuildCuratedTowns` |
| `DiscoverAsync_CallsLlmAndReturnsTowns` | Rewrite → test with tool delegate (text fallback removed) |
| `BuildDiscoverPrompt_ContainsRegionNameAndTowns` | Update assertions — no town list, check region name |
| `BuildDiscoverPrompt_ContainsPickInstructions` | Update — check "Call the submit_towns function" |
| `BuildDiscoverPrompt_UsesFantasyParams` | Still valid — check genre/theme in prompt |

---

## Summary of file changes

| File | Change |
|------|--------|
| `Generation/TownCurator.cs` | Rewrite `BuildDiscoverPrompt` (no town list, no seed, no JSON instructions). Remove text fallback from `DiscoverAsync`. Delete `ParseDiscoverResponse`. Make `BuildCuratedTowns` `internal static`. |
| `Generation/LlmTownEntry.cs` | Update `RealName` description. |
| `TownCuratorDiscoverTests.cs` | Rewrite `ParseDiscoverResponse_*` → `BuildCuratedTowns_*`. Update prompt tests. Rewrite text-fallback Discover test → tool-based test. |

---

## Risk

- **LLM picks settlements not in the template.** This already happens and
  is handled — `BuildCuratedTowns` skips unmatched `realName`s. If the LLM
  picks too many unmatched names, the result count drops below `MinTowns`
  and `Validate` throws. The caller can retry. A future improvement could
  pass the count of available template settlements to help the LLM
  calibrate, but this is not blocking.
- **Removing text fallback breaks test-only code paths.** The text fallback
  was only ever used in tests. Tests should use the tool delegate like
  production code.
- **Seed removal.** Callers still pass seed but it's ignored. No API break.
  If seed is needed later for a different purpose, the parameter is still
  there.
