# Step 03 — Pipeline Wizard UI Shell

## Goal

Replace the current 6-tab `MainPage` with the pipeline wizard layout: a
sidebar step list + main content area. Each step page is a placeholder that
will be fleshed out in subsequent steps.

## Deliverables

### 3.1 `PipelineWizardView` (new View)

- Sidebar with step list (① Region through ⑧ Assemble)
- Each step shows: number, name, status icon (✅ completed, 🔵 current, 🔒 locked)
- Clicking a completed or current step navigates to it
- Locked steps are not clickable
- Main content area swaps in the current step's view

### 3.2 `PipelineWizardViewModel`

- Binds to `PipelineState` (from step 02)
- Properties: `CurrentStep`, `Steps` (observable collection of step metadata)
- `NavigateToStep(int)` command
- Loads pipeline state on startup, determines which steps are unlocked

### 3.3 Step placeholder views

Create 8 stub views (one per step) with step name as heading:
- `RegionStepView`
- `DownloadStepView`
- `ParseStepView`
- `TownSelectionStepView`
- `TownDesignStepView`
- `TownMapsStepView`
- `AssetsStepView`
- `AssemblyStepView`

Each shows its name and a **[Next →]** button (disabled until step logic is
implemented).

### 3.4 Update `MainPage`

Keep the existing tabs but add a new **"Pipeline v3"** tab that hosts
`PipelineWizardView`. This allows both UIs to coexist during development.
Remove old tabs only after all steps are implemented.

### 3.5 Settings access

Settings gear icon in the top-right corner of the wizard, opening the existing
`SettingsView` as a modal/popup.

## Dependencies

- Step 02 (pipeline state drives step locking/navigation)

## Estimated scope

- New files: ~18 (wizard view/VM + 8 step views + 8 step VMs)
- Modified files: 1–2 (`MainPage.xaml`, DI registration)
