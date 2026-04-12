---
applyTo: "src/Oravey2.MapGen.App/**"
description: "AutomationId naming conventions and test instrumentation rules for the MapGen MAUI app. Use when creating or editing Views and ViewModels."
---

# MapGen Test Instrumentation Guidelines

The MapGen app is tested via Brinell.Maui (FlaUI on Windows). Every interactive element must be discoverable by automation. Follow these rules when creating or editing views.

## AutomationId naming

Every `Button`, `Entry`, `Label` (status/output), `Picker`, and other interactive control **must** have an `AutomationId`.

### Pattern: `{StepPrefix}{Role}`

| Element type | Example | Rule |
|---|---|---|
| Button | `AssemblyExportToDbButton` | `{Step}{Action}Button` |
| Entry / text input | `AssemblyScenarioName` | `{Step}{FieldName}` |
| Status label | `AssemblyStatusText` | `{Step}StatusText` |
| Validation summary | `AssemblyValidationSummary` | `{Step}ValidationSummary` |
| Picker | `LayoutTilesetPicker` | `{Step}{FieldName}Picker` |

`{StepPrefix}` matches the step name without "Step" (e.g., `Assembly`, `Layout`, `Zones`).

### Wizard-level IDs

| Element | AutomationId | Notes |
|---|---|---|
| Sidebar step item | `WizardStep{N}` | Bound via `PipelineStepInfo.AutomationId` |
| Content area | `WizardStepContent` | The `ContentView` hosting the active step |
| Busy sentinel | `UITest_IsBusy` | Hidden label — see below |

## UITest_IsBusy sentinel

`PipelineWizardView.xaml` contains a hidden `Label`:

```xml
<Label AutomationId="UITest_IsBusy"
       Text="{Binding IsBusy}"
       HeightRequest="1" WidthRequest="1" Opacity="0.01" />
```

**Important:** Do not use `IsVisible="False"` or `Opacity="0"` with `HeightRequest="0"` — MAUI removes zero-area and invisible elements from the UIA automation tree, making them unfindable by FlaUI. Use `HeightRequest="1"` with `Opacity="0.01"` instead.

## TabbedPage limitation

FlaUI **cannot traverse into TabbedPage content** on WinUI3. When running under UI test automation (`MAPGEN_AUTO_LOAD_REGION` env var set), `App.xaml.cs` uses `PipelineWizardView` directly instead of wrapping it in the `TabbedPage`-based `MainPage`.

Brinell.Maui's `WaitIdle()` polls this label to detect when the app is busy. **Do not remove it.**

### Wiring IsBusy for new steps

When a step ViewModel has a long-running operation:

1. The step VM must expose an `IsRunning` (or `IsBusy`) property.
2. `PipelineWizardViewModel` subscribes to `PropertyChanged` on the step VM.
3. The handler forwards the step's busy state to the wizard's `IsBusy`:

```csharp
stepVm.PropertyChanged += (s, e) =>
{
    if (e.PropertyName == "IsRunning")
        IsBusy = stepVm.IsRunning;
};
```

## Intermediate StatusText

Long-running commands (export, validation, generation) must set `StatusText` **before** the `try` block so automation can detect the in-progress state:

```csharp
StatusText = "Exporting to database...";
IsRunning = true;
try
{
    await DoWork();
    StatusText = "Export complete.";
}
catch (Exception ex)
{
    StatusText = $"Error: {ex.Message}";
}
finally
{
    IsRunning = false;
}
```

## Do not hardcode delays in tests

Tests must use `WaitIdle()` or poll `UITest_IsBusy` / `StatusText` — never `Task.Delay` or `Thread.Sleep`.
