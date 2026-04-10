# RCA-006: Assets Step UI Shows Empty / Unchanged After Redesign

**Date:** 2026-04-10  
**Severity:** Functional / Medium  
**Affected:** Step 7 — 3D Assets (`AssetsStepView`)

---

## Symptom

After implementing the step-09d redesign (town-grouped building list replacing
the flat asset queue), the UI still shows the old layout: an empty left panel
with no towns/buildings visible, and the right detail panel with empty
bindings. Rebuilt the app — no visual change.

---

## Root Cause: ViewModel never wired to the view

### Cause 1 — No VM passed to AssetsStepView constructor

In `PipelineWizardView.xaml.cs`, step 7 is registered as:

```csharp
_viewModel.RegisterStepViewFactory(7, () => new AssetsStepView());
```

Every other step passes its ViewModel:

```csharp
// Steps 1–6 all pass their VM:
new RegionStepView(_viewModel.RegionStepVM, _services)
new TownDesignStepView(_viewModel.TownDesignStepVM)
new TownMapsStepView(_viewModel.TownMapsStepVM)
```

`AssetsStepView` is created with no arguments. Its `BindingContext` is never
set. All XAML bindings resolve to `null` — only static content (header, button
text, labels) renders.

### Cause 2 — No AssetsStepVM property on PipelineWizardViewModel

`PipelineWizardViewModel` has properties for steps 1–6:

```csharp
public RegionStepViewModel RegionStepVM { get; }
public DownloadStepViewModel DownloadStepVM { get; }
public ParseStepViewModel ParseStepVM { get; }
public TownSelectionStepViewModel TownSelectionStepVM { get; }
public TownDesignStepViewModel TownDesignStepVM { get; }
public TownMapsStepViewModel TownMapsStepVM { get; }
// ← No AssetsStepViewModel property
```

The constructor does not accept or store `AssetsStepViewModel`. Although it's
registered in `MauiProgram.cs` via DI:

```csharp
builder.Services.AddTransient<AssetsStepViewModel>();
```

…it's never resolved or injected.

### Cause 3 — No Load() call, no StepCompleted hookup

Steps 1–6 each have their `.StepCompleted = OnStepCompleted` wired in the
`PipelineWizardViewModel` constructor, and their `.Load(state)` called when
navigating to the step. Step 7 has none of this — even if the VM existed, it
would never receive the pipeline state or content pack path.

### Why it appeared to "work" before

The XAML renders static elements (header label "⑦ 3D Assets", subtitle,
buttons) without any BindingContext. The right-panel cards also render their
borders/backgrounds. Since bins empty collections (`FilteredTowns`) to
`BindableLayout.ItemsSource`, the list area is simply empty — not visually
broken, just vacant. This makes it look like the old UI "unchanged" rather
than a binding failure.

---

## Fix Required

1. **Add `AssetsStepViewModel` to `PipelineWizardViewModel`:**
   - Add constructor parameter + property `AssetsStepVM`
   - Wire `AssetsStepVM.StepCompleted = OnStepCompleted`

2. **Update `AssetsStepView` to accept VM:**
   - Add constructor parameter `(AssetsStepViewModel vm)`
   - Set `BindingContext = vm` in constructor

3. **Update step factory registration:**
   ```csharp
   _viewModel.RegisterStepViewFactory(7, () =>
   {
       _viewModel.AssetsStepVM.Load(_viewModel.GetState());
       return new AssetsStepView(_viewModel.AssetsStepVM);
   });
   ```

4. **Same issue likely applies to Step 8 (`AssemblyStepView`)** — also
   created with `new AssemblyStepView()` and no VM.

---

## Why This Wasn't Caught

- Unit tests test the ViewModel in isolation (constructing it directly) — they
  don't test the DI wiring or view-VM binding.
- The MAUI build succeeds even with unbound XAML — compiled bindings only
  check that the `x:DataType` type has the property at compile time, not that
  a BindingContext instance exists at runtime.
- The app renders without exceptions — null bindings silently produce empty
  values.
