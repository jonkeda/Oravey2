# M1 Phase 1.2 — Refactor: Move Game Logic from Windows to Core

**Goal:** Extract all game orchestration logic from `Oravey2.Windows` into `Oravey2.Core` so that future platform targets (Android, iOS) only need a thin launcher.

**Depends on:** M1 Phase 1 complete

---

## 1. Problem Statement

Currently `Oravey2.Windows` contains ~700 lines of game logic that must be duplicated for every platform:

| File | Lines | What it does | Should be in |
|------|-------|-------------|-------------|
| `Program.cs` | ~260 | Service registration, menu wiring, save/load helpers, death penalty, scene setup, automation | **Core** (except `Game()` instantiation + platform logging) |
| `ScenarioLoader.cs` | ~420 | Entity creation, player setup, enemy spawn, UI overlay wiring, combat wiring | **Core** |
| `OraveyAutomationHandler.cs` | ~1100 | Game query dispatch, all handler methods | **Core** (automation is cross-platform) |

When we add `Oravey2.Android`, we'd need to copy/maintain all of this in a second launcher. Instead, the platform project should only contain:

1. `Game()` instantiation + `game.Run()`
2. Platform-specific `IInputProvider` (touch vs keyboard)
3. Platform-specific logging configuration
4. Platform-specific package references (`Stride.CommunityToolkit.Windows` vs `.Android`)

---

## 2. Target Architecture

```
Oravey2.Core/
├── Bootstrap/
│   ├── GameBootstrapper.cs         ← NEW: orchestrates full game startup
│   ├── BootstrapConfig.cs          ← NEW: platform-provided options
│   └── ScenarioLoader.cs           ← MOVED from Windows (unchanged logic)
├── Automation/
│   ├── AutomationContracts.cs      ← EXISTS
│   └── OraveyAutomationHandler.cs  ← MOVED from Windows
├── ... (all existing Core folders)

Oravey2.Windows/
├── Program.cs                       ← SHRINKS to ~30 lines
├── Oravey2.Windows.csproj           ← Platform packages only

Oravey2.Android/ (future)
├── Program.cs                       ← ~30 lines, same pattern
├── Oravey2.Android.csproj
```

---

## 3. New Types

### 3.1 BootstrapConfig

```csharp
namespace Oravey2.Core.Bootstrap;

/// <summary>
/// Platform-provided configuration for game startup.
/// </summary>
public sealed class BootstrapConfig
{
    /// <summary>Platform input provider (keyboard, touch, gamepad).</summary>
    public required IInputProvider InputProvider { get; init; }

    /// <summary>Logger factory for platform-specific logging.</summary>
    public required ILoggerFactory LoggerFactory { get; init; }

    /// <summary>Whether to enable the automation server (for UI tests).</summary>
    public bool AutomationEnabled { get; init; }

    /// <summary>CLI arguments (Windows) or intent extras (Android).</summary>
    public string[] Args { get; init; } = [];
}
```

### 3.2 GameBootstrapper

```csharp
namespace Oravey2.Core.Bootstrap;

/// <summary>
/// Orchestrates full game startup. Called by each platform's Program.cs.
/// Contains ALL game logic wiring — platforms only provide config.
/// </summary>
public sealed class GameBootstrapper
{
    public void Start(Scene rootScene, Game game, BootstrapConfig config);
}
```

The `Start` method contains everything currently in `Program.cs`'s `Start()` local function:
- Service registration
- Scene infrastructure (compositor, light, skybox, font, camera)
- ScenarioLoader creation
- Menu entity creation + callback wiring
- SaveLoadScript wiring
- Death penalty subscription
- Automation server setup (if enabled)
- Normal mode: show start menu / Automation mode: load scenario + skip menu

---

## 4. Move Plan

### Wave 1: Move ScenarioLoader to Core

| Action | From | To |
|--------|------|----|
| Move file | `src/Oravey2.Windows/ScenarioLoader.cs` | `src/Oravey2.Core/Bootstrap/ScenarioLoader.cs` |
| Update namespace | `Oravey2.Windows` | `Oravey2.Core.Bootstrap` |
| Update usings | `Program.cs`, `OraveyAutomationHandler.cs` | Add `using Oravey2.Core.Bootstrap;` |

**No logic changes.** ScenarioLoader already uses only `Oravey2.Core.*` types + Stride engine types. Both are referenced by Core's .csproj.

### Wave 2: Move OraveyAutomationHandler to Core

| Action | From | To |
|--------|------|----|
| Move file | `src/Oravey2.Windows/OraveyAutomationHandler.cs` | `src/Oravey2.Core/Automation/OraveyAutomationHandler.cs` |
| Update namespace | `Oravey2.Windows` | `Oravey2.Core.Automation` |
| Add package ref | `Oravey2.Core.csproj` | `Brinell.Automation` project reference |

**Prerequisite:** Core needs a reference to `Brinell.Automation` for `IAutomationHandler`, `AutomationCommand`, `AutomationResponse`. Currently only Windows references it.

### Wave 3: Create GameBootstrapper in Core

| Action | File |
|--------|------|
| Create | `src/Oravey2.Core/Bootstrap/BootstrapConfig.cs` |
| Create | `src/Oravey2.Core/Bootstrap/GameBootstrapper.cs` |

Extract the entire `Start()` local function body from `Program.cs` into `GameBootstrapper.Start()`. The only differences:

- `ILoggerFactory` comes from `BootstrapConfig` instead of being created inline
- `IInputProvider` comes from `BootstrapConfig` instead of `new KeyboardMouseInputProvider()`
- Automation enabled check comes from `BootstrapConfig.AutomationEnabled` instead of `StrideAutomationExtensions.IsAutomationEnabled()`
- CLI args come from `BootstrapConfig.Args`

### Wave 4: Shrink Windows Program.cs

After waves 1–3, `Program.cs` becomes:

```csharp
using Microsoft.Extensions.Logging;
using Oravey2.Core.Bootstrap;
using Oravey2.Core.Input;
using Stride.Engine;

using var game = new Game();

game.Run(start: (Scene rootScene) =>
{
    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder
            .SetMinimumLevel(LogLevel.Debug)
            .AddSimpleConsole(options =>
            {
                options.TimestampFormat = "HH:mm:ss.fff ";
                options.SingleLine = true;
            });
    });

    var config = new BootstrapConfig
    {
        InputProvider = new KeyboardMouseInputProvider(),
        LoggerFactory = loggerFactory,
        AutomationEnabled = Brinell.Automation.StrideAutomationExtensions.IsAutomationEnabled(),
        Args = Environment.GetCommandLineArgs(),
    };

    new GameBootstrapper().Start(rootScene, game, config);
});
```

**~30 lines.** All game logic lives in Core.

---

## 5. Dependency Changes

### Oravey2.Core.csproj additions

```xml
<ItemGroup>
  <ProjectReference Include="..\..\Brinell\srcnew\Brinell.Automation\Brinell.Automation.csproj" />
</ItemGroup>
```

### Oravey2.Windows.csproj removals

The `Brinell.Automation` project reference stays in Windows too (needed for `StrideAutomationExtensions.IsAutomationEnabled()` + `game.UseAutomation()`), but the handler itself moves to Core.

Actually, `game.UseAutomation()` is an extension method on `Game` from Brinell. This call should remain in `GameBootstrapper.Start()` which lives in Core (Core already refs Stride.Engine which provides `Game`). So:

- Core refs: `Brinell.Automation` (for handler + `UseAutomation` + contracts)
- Windows refs: `Brinell.Automation` (only for `StrideAutomationExtensions.IsAutomationEnabled()` static check)

---

## 6. Files Changed Summary

| Wave | Action | File | Notes |
|------|--------|------|-------|
| 1 | Move | `ScenarioLoader.cs` → `Core/Bootstrap/` | Namespace change only |
| 1 | Edit | `Program.cs` | Update using |
| 1 | Edit | `OraveyAutomationHandler.cs` | Update using |
| 2 | Move | `OraveyAutomationHandler.cs` → `Core/Automation/` | Namespace change only |
| 2 | Edit | `Oravey2.Core.csproj` | Add Brinell.Automation ref |
| 2 | Edit | `Program.cs` | Update using |
| 3 | Create | `Core/Bootstrap/BootstrapConfig.cs` | New |
| 3 | Create | `Core/Bootstrap/GameBootstrapper.cs` | New (extracted from Program.cs) |
| 4 | Rewrite | `Program.cs` | Shrink to ~30 lines |
| 4 | Edit | `Oravey2.Tests` | Update namespaces if any tests reference Windows types |
| 4 | Edit | `Oravey2.UITests` | Likely no changes (tests use GameQueryHelpers, not handler directly) |

---

## 7. What Stays Platform-Specific

| Concern | Why it can't be in Core |
|---------|------------------------|
| `new Game()` + `game.Run()` | Stride `Game` constructor initializes platform graphics |
| `LoggerFactory.Create(...)` | Console logging config is platform-specific (Android uses logcat) |
| `new KeyboardMouseInputProvider()` | Android uses `TouchInputProvider` |
| `StrideAutomationExtensions.IsAutomationEnabled()` | May differ per platform (env var vs intent) |
| `.csproj` package refs | `Stride.CommunityToolkit.Windows` vs `.Android` |

---

## 8. Risk Assessment

| Risk | Mitigation |
|------|-----------|
| Brinell.Automation ref in Core increases coupling | Brinell is our own library; acceptable |
| `Game` type in Core's `GameBootstrapper.Start()` signature | Core already refs `Stride.Engine` which provides `Game` |
| InternalsVisibleTo may need updates | ScenarioLoader uses `internal` on some lists — already has `InternalsVisibleTo` for Windows |
| Automation handler uses `Stride.Graphics.GeometricPrimitives` | Already available via Core's Stride.Engine ref |

---

## 9. Acceptance Criteria

1. `Oravey2.Windows/Program.cs` is ≤40 lines
2. `Oravey2.Windows/` contains only `Program.cs` + `.csproj` (no .cs game logic)
3. All 652+ unit tests pass unchanged
4. All UI smoke tests pass unchanged
5. Game launches and plays identically
6. A hypothetical `Oravey2.Android/Program.cs` could bootstrap the game with ~30 lines
