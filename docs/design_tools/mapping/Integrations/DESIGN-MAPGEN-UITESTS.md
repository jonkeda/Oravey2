# Design: MapGen MAUI UI Tests — Noord-Holland Export

## Context

The MapGen MAUI app (8-step pipeline wizard) currently has **zero
AutomationIds** and **no `UITest_IsBusy` sentinel**. Before any
Brinell.Maui UI tests can run, the app needs instrumentation.

The goal is to smoke-test the "Export to Game DB" flow for the
Noord-Holland content pack, verifying the pipeline's final step
works end-to-end in the running MAUI app.

---

## Prerequisites: App Instrumentation

### P1 — Add `UITest_IsBusy` sentinel label

Brinell.Maui's `PageObjectBase.WaitIdle()` polls a `Label` with
`AutomationId="UITest_IsBusy"`. The label's text must be `"True"`
when the page is busy and `"False"` when idle.

**File:** `src/Oravey2.MapGen.App/Views/PipelineWizardView.xaml`

Add a hidden sentinel label bound to the wizard's busy state:

```xml
<Label AutomationId="UITest_IsBusy"
       Text="{Binding IsBusy}"
       IsVisible="False" />
```

Wire `IsBusy` in `PipelineWizardViewModel` to reflect when any
step VM has `IsRunning = true`.

### P2 — Add AutomationIds to Assembly step

**File:** `src/Oravey2.MapGen.App/Views/Steps/AssemblyStepView.xaml`

| Element | AutomationId | Purpose |
|---------|-------------|---------|
| Generate Scenario button | `AssemblyGenerateScenarioButton` | Click to generate |
| Rebuild Catalog button | `AssemblyCatalogButton` | Click to rebuild |
| Update Manifest button | `AssemblyManifestButton` | Click to update |
| Validate button | `AssemblyValidateButton` | Click to validate |
| Export to Game DB button | `AssemblyExportToDbButton` | Primary test target |
| Complete button | `AssemblyCompleteButton` | Final step |
| Status text label | `AssemblyStatusText` | Assert export result |
| Scenario ID entry | `AssemblyScenarioId` | Input scenario ID |
| Scenario Name entry | `AssemblyScenarioName` | Input scenario name |
| Validation summary | `AssemblyValidationSummary` | Assert validation output |

### P3 — Add AutomationIds to wizard sidebar

**File:** `src/Oravey2.MapGen.App/Views/PipelineWizardView.xaml`

| Element | AutomationId | Purpose |
|---------|-------------|---------|
| Step 8 sidebar item | `WizardStep8` | Navigate to Assembly step |
| Step content area | `WizardStepContent` | Assert step view loaded |

### P4 — Add intermediate status text to ExportToDb

**File:** `src/Oravey2.MapGen/ViewModels/AssemblyStepViewModel.cs`

Currently `RunExportToDb()` only sets `StatusText` once at the end.
Add an intermediate update so tests can detect the operation has
started:

```csharp
internal void RunExportToDb()
{
    if (string.IsNullOrEmpty(_state.ContentPackPath)) return;
    IsRunning = true;
    StatusText = "Exporting to database...";  // ← NEW
    try
    {
        // ... existing export logic ...
        StatusText = $"Exported '{result.RegionName}' to ...";
    }
    finally { IsRunning = false; }
}
```

---

## Test Framework Setup

### Fixture

```
tests/Oravey2.UITests/MapGen/MapGenTestFixture.cs
```

```csharp
public class MapGenTestFixture : MauiTestFixtureBase
{
    protected override string GetDefaultAppPath(string platform)
    {
        var solutionDir = FindSolutionDirectory();
        return Path.Combine(solutionDir,
            "src", "Oravey2.MapGen.App", "bin", "Debug",
            "net10.0-windows10.0.19041.0", "win-x64",
            "Oravey2.MapGen.App.exe");
    }
}
```

### Page object

```
tests/Oravey2.UITests/MapGen/Pages/AssemblyStepPage.cs
```

```csharp
public class AssemblyStepPage : PageObjectBase<AssemblyStepPage>
{
    public AssemblyStepPage(IMauiTestContext ctx) : base(ctx) { }

    public override string Name => "AssemblyStep";

    // Buttons
    public Button<AssemblyStepPage> GenerateScenario
        => Button("AssemblyGenerateScenarioButton");
    public Button<AssemblyStepPage> RebuildCatalog
        => Button("AssemblyCatalogButton");
    public Button<AssemblyStepPage> UpdateManifest
        => Button("AssemblyManifestButton");
    public Button<AssemblyStepPage> Validate
        => Button("AssemblyValidateButton");
    public Button<AssemblyStepPage> ExportToDb
        => Button("AssemblyExportToDbButton");
    public Button<AssemblyStepPage> Complete
        => Button("AssemblyCompleteButton");

    // Labels
    public Label<AssemblyStepPage> StatusText
        => Label("AssemblyStatusText");
    public Label<AssemblyStepPage> ValidationSummary
        => Label("AssemblyValidationSummary");

    // Entries
    public Entry<AssemblyStepPage> ScenarioId
        => Entry("AssemblyScenarioId");
    public Entry<AssemblyStepPage> ScenarioName
        => Entry("AssemblyScenarioName");
}
```

---

## Test Class: NoordHollandExportTests

```
tests/Oravey2.UITests/MapGen/NoordHollandExportTests.cs
```

### Test 1 — Export button visible on Assembly step

```
Assembly_ExportButton_IsVisible
```

1. Navigate to step 8 (Assembly) via sidebar click
2. `WaitIdle()` — wait for step to load
3. Assert `ExportToDb` button exists and is enabled

**Pass:** Button is visible and clickable.

### Test 2 — Export produces success status

```
Assembly_ExportToDb_ShowsSuccessStatus
```

1. Navigate to step 8
2. `WaitIdle()`
3. Click `ExportToDb`
4. `WaitIdle()` — wait for export to complete
5. Read `StatusText.GetText()`
6. Assert text contains `"Exported"` and `"Noord-Holland"`

**Pass:** Status shows export success with region name.

### Test 3 — Export creates world.db

```
Assembly_ExportToDb_CreatesWorldDb
```

1. Delete any existing `world.db` next to the content pack
2. Navigate to step 8
3. `WaitIdle()`
4. Click `ExportToDb`
5. `WaitIdle()`
6. Assert `world.db` file exists on disk (check from test code)
7. Open the DB, assert region "Noord-Holland" exists

**Pass:** `world.db` created with correct region row.

### Test 4 — Export reports chunk and entity counts

```
Assembly_ExportToDb_ReportsChunkCount
```

1. Navigate to step 8, click `ExportToDb`, `WaitIdle()`
2. Read `StatusText.GetText()`
3. Parse the summary: assert chunks > 0, towns ≥ 1

**Pass:** Status contains non-zero counts.

### Test 5 — Validate before export works

```
Assembly_Validate_ShowsSummary
```

1. Navigate to step 8
2. Click `Validate`
3. `WaitIdle()`
4. Assert `ValidationSummary` label is visible
5. Assert summary text is non-empty

**Pass:** Validation produces output.

### Test 6 — Export while busy is disabled

```
Assembly_ExportToDb_DisabledWhileRunning
```

1. Navigate to step 8
2. Click `ExportToDb`
3. Immediately check `ExportToDb.IsEnabled()` — should be `false`
   (because `IsRunning = true` disables the command)
4. `WaitIdle()`
5. Check `ExportToDb.IsEnabled()` — should be `true` again

**Pass:** Button is disabled during export, re-enabled after.

### Test 7 — Screenshot after export

```
Assembly_ExportToDb_ScreenshotShowsStatus
```

1. Navigate to step 8, `ExportToDb`, `WaitIdle()`
2. Take screenshot
3. Assert screenshot file exists and is > 1KB

**Pass:** Visual record of the export result.

---

## IsBusy Synchronization Flow

```
Test code                    MAUI App
─────────                    ────────
Click ExportToDb   ───────►  IsRunning = true
                             ViewModel.IsBusy = true
                             UITest_IsBusy label = "True"
                             (export runs synchronously)
                             IsRunning = false
                             ViewModel.IsBusy = false
                             UITest_IsBusy label = "False"
WaitIdle() polls   ◄─────── returns "False"
Assert StatusText
```

`WaitIdle()` is the sole synchronization mechanism. Tests must
**never use `Thread.Sleep`** — they poll the sentinel and proceed
when idle. The `MauiTestFixtureBase` default `PageLoad` timeout
(30s) is sufficient for the export operation.

---

## Test precondition: loaded pipeline state

The Noord-Holland content pack must be fully built (steps 1–7
completed) before step 8 is reachable. Two approaches:

### Option A: Pre-built pipeline state file (recommended)

Save a `.json` pipeline state file after completing steps 1–7.
The fixture loads this state on startup so the wizard opens
directly at step 8.

### Option B: Drive through all 8 steps

Slower but fully end-to-end. Would need page objects for all
8 steps. Not recommended for smoke testing.

---

## File plan

| File | Action |
|------|--------|
| `src/Oravey2.MapGen.App/Views/PipelineWizardView.xaml` | **Modify** — add `UITest_IsBusy` sentinel |
| `src/Oravey2.MapGen.App/Views/Steps/AssemblyStepView.xaml` | **Modify** — add AutomationIds |
| `src/Oravey2.MapGen/ViewModels/AssemblyStepViewModel.cs` | **Modify** — add intermediate StatusText |
| `tests/Oravey2.UITests/MapGen/MapGenTestFixture.cs` | **New** |
| `tests/Oravey2.UITests/MapGen/Pages/AssemblyStepPage.cs` | **New** |
| `tests/Oravey2.UITests/MapGen/NoordHollandExportTests.cs` | **New** — 7 tests |

---

## Open questions

1. **Pipeline state save/load:** Does `PipelineWizardViewModel`
   support loading a saved state that jumps to step 8? If not,
   this needs to be added as a fixture prerequisite.

2. **MAUI .exe output path:** The TFM includes a platform suffix
   (`net10.0-windows10.0.19041.0`). Need to verify the exact
   output path for `MauiDriverOptions.AppPath`.

3. **FlaUI availability:** Brinell.Maui uses FlaUI on Windows.
   Confirm `Brinell.Maui.FlaUI` package is available and the
   MAUI app exposes UIA automation peers for its controls.
