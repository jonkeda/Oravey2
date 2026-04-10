# Step 05c — Parse Step: Reload Cached Template on Re-entry

## Problem

When the user navigates back to step 3 (Parse & Extract) after completing it,
the UI shows **"Ready to parse."** with only the "Parse Data" button visible.
The parsed data (towns, roads, water) is already saved as
`region-template.bin` and the pipeline state has `TemplateSaved = true`, but
the UI doesn't reflect this.

### Root cause

`Initialize()` detects `TemplateSaved && File.Exists(templatePath)` and fires
`LoadCachedTemplateAsync` — but as **fire-and-forget**:

```csharp
_ = LoadCachedTemplateAsync(templatePath);
```

The method returns before the async load completes. Meanwhile:

- `IsParsed` is still `false` (set to `true` only inside the async task)
- `StatusText` is still `"Ready to parse."` (set to "Loading cached template..."
  inside the async task)
- The entire results panel, cull settings, save button, and "Next →" button are
  hidden because they are gated on `IsParsed`

The user sees the raw "Ready to parse" state for a fraction of a second — or
longer if the `.bin` file is large. If they click "Parse Data" before the load
finishes, they re-parse from raw OSM data unnecessarily.

---

## Design

### Fix 1 — Set interim state synchronously in Initialize

Before firing the async load, set `IsParsed = true` and `StatusText` so the
UI immediately reflects that data exists:

```csharp
if (state.Parse.TemplateSaved && File.Exists(templatePath))
{
    // Restore counts from state
    RawTownCount = state.Parse.TownCount;
    RawRoadCount = state.Parse.RoadCount;
    ...

    // Show results panel immediately with cached counts
    IsParsed = true;
    IsCulled = state.Parse.FilteredTownCount != state.Parse.TownCount;
    StatusText = "Loading cached template...";

    _ = LoadCachedTemplateAsync(templatePath);
}
```

This makes the counts, summaries, cull settings, save button, and "Next →"
button all visible immediately. The async load populates town list, summary
tables, and the in-memory `ParsedTemplate` object in the background.

### Fix 2 — Disable Parse button while loading cache

Add a guard so the user cannot click "Parse Data" while the cached template is
loading:

```csharp
private bool _isLoadingCache;

// ParseCommand CanExecute becomes: !IsParsing && !_isLoadingCache
```

Set `_isLoadingCache = true` before the fire-and-forget, and `false` at the
end of `LoadCachedTemplateAsync` (in both success and failure paths).

### Fix 3 — Show RawSummary / FilteredSummary immediately

`RawSummary` and `FilteredSummary` are computed properties that depend on
`IsParsed`. Since we now set `IsParsed = true` synchronously, these will render
immediately with the count values loaded from state.

No code change needed beyond Fix 1.

### Fix 4 — Restore IsCulled state

Currently `IsCulled` is never restored from state. If the user culled and then
navigated away, returning shows "Not yet culled — using raw data" even though
the saved template contains culled data.

Detect this by comparing filtered vs raw counts:

```csharp
IsCulled = state.Parse.FilteredTownCount < state.Parse.TownCount
        || state.Parse.FilteredRoadCount < state.Parse.RoadCount
        || state.Parse.FilteredWaterBodyCount < state.Parse.WaterBodyCount;
```

---

## Summary of file changes

| File | Change |
|------|--------|
| `ParseStepViewModel.cs` | Set `IsParsed = true`, `StatusText`, and `IsCulled` synchronously in `Initialize` before async load. Add `_isLoadingCache` guard for Parse button. |

No XAML changes — existing bindings on `IsParsed`, `IsCulled`, `RawSummary`,
`FilteredSummary` will display correctly once the properties are set
synchronously.

---

## Risk

- **Low**: Setting `IsParsed = true` before `ParsedTemplate` is loaded means
  the "Next →" button is enabled before the template object is in memory. If
  the user clicks "Next →" immediately, `ParsedTemplate` may still be null.
  Mitigation: `TownSelectionStepViewModel.Initialize` already handles null
  `ParsedTemplate` gracefully, and `PipelineWizardViewModel.InitializeStepViewModels`
  sets it after the ViewModel is already initialized. The fire-and-forget
  typically completes in <100ms for a ~2MB `.bin` file.
- **Template file corrupted**: `LoadCachedTemplateAsync` already handles this
  case — sets `IsParsed = false` and shows "Cached template is corrupt."
