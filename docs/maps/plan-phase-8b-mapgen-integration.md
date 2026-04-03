# Phase 8B — MapGen Integration & UX Polish

> **Status:** Not Started
> **Date:** 2026-04-03
> **Depends on:** Phase 8 (MapGen class library + MAUI app skeleton)

---

## Goal

Wire the Copilot SDK into `MapGeneratorService`, add export settings, and polish the save workflow.

---

## Step 8B.1 — Add Copilot SDK to MapGen

**File:** `src/Oravey2.MapGen/Oravey2.MapGen.csproj`

Add:
```xml
<PackageReference Include="GitHub.Copilot.SDK" Version="0.2.*" />
```

Wire `MapGeneratorService.GenerateAsync()` to:
1. Create `CopilotClient` with `CliPath` from settings
2. Create session with `Streaming = true`, model from request, system prompt via `SystemMessage`
3. Register 5 custom tools via `AIFunctionFactory.Create` (`validate_blueprint`, `lookup_asset`, `check_overlap`, `check_walkability`, `list_available_prefabs`)
4. Subscribe to session events → fire `OnProgress`
5. On `SessionIdleEvent` → extract JSON via `BlueprintCollector.CollectFromResponse()`
6. Return `GenerationResult`

Wire `RefineAsync()` to `client.ResumeSessionAsync()`.

**Tests:** `MapGeneratorServiceTests.cs` — integration test with `[Trait("Category", "Integration")]`

---

## Step 8B.2 — Default export location setting

**File:** `src/Oravey2.MapGen.App/ViewModels/SettingsViewModel.cs`

Add properties:
- `string ExportPath` — default folder for saving blueprints (default: `My Documents/Oravey2/Blueprints`)
- `ICommand BrowseExportPathCommand` — opens a folder picker dialog

Persist via `Preferences.Set("ExportPath", ...)` in `SaveSettings()` / `LoadSettings()`.

**File:** `src/Oravey2.MapGen.App/Views/SettingsView.xaml`

Add below the CLI path section:
```
Export Location: [_______________________________] [Browse]
```

The Browse button uses `FolderPicker` from CommunityToolkit.Maui.

---

## Step 8B.3 — Save Blueprint uses export location

**File:** `src/Oravey2.MapGen.App/ViewModels/GeneratorViewModel.cs`

Update `SaveBlueprintAsync()`:
1. Read `ExportPath` from `Preferences` (fallback: `My Documents/Oravey2/Blueprints`)
2. Ensure directory exists
3. Save as `{LocationName}_{timestamp}.json`
4. Show full path in `StatusMessage`

---

## Implementation Order

```
8B.1 (SDK wiring) → 8B.2 (export setting) → 8B.3 (save uses export path)
```

Steps 8B.2 and 8B.3 are independent of 8B.1 and can proceed first.

---

## Verification

```powershell
# Unit tests
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~MapGen"

# Integration tests (requires Copilot CLI + auth)
dotnet test tests/Oravey2.Tests --filter "Category=Integration"

# Manual: run app, configure export path, generate, save
dotnet run --project src/Oravey2.MapGen.App
```
