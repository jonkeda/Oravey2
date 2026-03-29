# Testing Oravey2 with Brinell.Stride

Brinell.Stride is a UI automation testing framework for Stride Engine games. It communicates with the running game over a **named pipe** (`Brinell.Stride.Automation`), sending JSON commands and receiving responses. This allows tests to query UI element state, click buttons, enter text, simulate keyboard input, and wait for game conditions — all without needing physical mouse/keyboard focus.

## Architecture Overview

```
┌──────────────────────┐       Named Pipe        ┌──────────────────────┐
│   Test Process        │◄───────────────────────►│   Oravey2.Windows    │
│   (xUnit + Brinell)  │   JSON commands/resp     │   (Stride Game)      │
│                       │                          │                      │
│  StrideTestFixture    │                          │  AutomationServer    │
│    └─ StrideGameDriver│                          │  (must be added)     │
│       └─ NamedPipe    │                          │                      │
│    └─ StrideTestContext                          │                      │
│       └─ Page Objects │                          │                      │
│          └─ Controls  │                          │                      │
└──────────────────────┘                          └──────────────────────┘
```

**Key point:** Brinell.Stride is the *test client* side. The game must host a corresponding **automation server** that listens on the named pipe, processes commands (GetState, Click, SimulateKeyPress, etc.), and returns `AutomationResponse` JSON.

## Prerequisites

Before writing tests, Oravey2 needs:

1. **Automation server in-game** — A named pipe server inside `Oravey2.Windows` that:
   - Listens on pipe name `Brinell.Stride.Automation`
   - Accepts `AutomationCommand` JSON messages
   - Resolves UI elements by `AutomationId`
   - Returns `AutomationResponse` JSON with element state, action results, etc.
   
2. **Automation IDs on UI elements** — Every testable UI element needs a unique `AutomationId` string so Brinell controls can locate them.

3. **`--automation` launch flag** — The game should accept this argument to enable the automation server (so it's only active during testing).

## Project Setup

### 1. Create the test project

```
tests/
  Oravey2.UITests/
    Oravey2.UITests.csproj
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.9.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Brinell\srcnew\Brinell.Stride\Brinell.Stride.csproj" />
    <ProjectReference Include="..\..\src\Oravey2.Core\Oravey2.Core.csproj" />
  </ItemGroup>
</Project>
```

### 2. Create the test fixture

Inherit from `StrideTestFixtureBase` and point it at the game executable:

```csharp
using Brinell.Stride.Context;
using Brinell.Stride.Testing;

namespace Oravey2.UITests;

public class OraveyTestFixture : StrideTestFixtureBase
{
    protected override string GetDefaultAppPath()
    {
        // Path relative to test output or absolute
        var solutionDir = FindSolutionDirectory();
        return Path.Combine(solutionDir,
            "src", "Oravey2.Windows", "bin", "Debug", "net10.0", "Oravey2.Windows.exe");
    }

    protected override StrideTestContextOptions CreateOptions()
    {
        var options = base.CreateOptions();
        options.StartupTimeoutMs = 20000;  // Games can take a while to start
        options.DefaultTimeoutMs = 5000;
        return options;
    }
}
```

You can also set the `STRIDE_APP_PATH` environment variable instead of hardcoding the path.

## Writing Page Objects

Page objects encapsulate a screen/panel in the game. They inherit from `PageObjectBase<TSelf>` using the Curiously Recurring Template Pattern (CRTP) for fluent method chaining.

### Example: Main Menu Page

```csharp
using Brinell.Stride.Controls;
using Brinell.Stride.Interfaces;
using Brinell.Stride.Pages;

namespace Oravey2.UITests.Pages;

public class MainMenuPage : PageObjectBase<MainMenuPage>
{
    // Root element automation ID — used by IsLoaded() check
    public override string AutomationId => "MainMenuPanel";

    // Declare controls by their automation IDs
    public Button<MainMenuPage> NewGameButton { get; }
    public Button<MainMenuPage> LoadGameButton { get; }
    public Button<MainMenuPage> SettingsButton { get; }
    public Button<MainMenuPage> QuitButton { get; }

    public MainMenuPage(IStrideTestContext context) : base(context)
    {
        NewGameButton = new Button<MainMenuPage>(this, "NewGameButton");
        LoadGameButton = new Button<MainMenuPage>(this, "LoadGameButton");
        SettingsButton = new Button<MainMenuPage>(this, "SettingsButton");
        QuitButton = new Button<MainMenuPage>(this, "QuitButton");
    }
}
```

### Example: Settings Page with various controls

```csharp
using Brinell.Stride.Controls;
using Brinell.Stride.Interfaces;
using Brinell.Stride.Pages;

namespace Oravey2.UITests.Pages;

public class SettingsPage : PageObjectBase<SettingsPage>
{
    public override string AutomationId => "SettingsPanel";

    public Slider<SettingsPage> VolumeSlider { get; }
    public CheckBox<SettingsPage> FullscreenToggle { get; }
    public EditText<SettingsPage> PlayerNameInput { get; }
    public Button<SettingsPage> ApplyButton { get; }
    public Button<SettingsPage> BackButton { get; }

    public SettingsPage(IStrideTestContext context) : base(context)
    {
        VolumeSlider = new Slider<SettingsPage>(this, "VolumeSlider");
        FullscreenToggle = new CheckBox<SettingsPage>(this, "FullscreenToggle");
        PlayerNameInput = new EditText<SettingsPage>(this, "PlayerNameInput");
        ApplyButton = new Button<SettingsPage>(this, "ApplyButton");
        BackButton = new Button<SettingsPage>(this, "BackButton");
    }
}
```

## Writing Tests

### Basic test structure

```csharp
using Xunit;

namespace Oravey2.UITests;

public class MainMenuTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void MainMenu_NewGameButton_IsVisible()
    {
        var menu = new Pages.MainMenuPage(_fixture.Context);
        menu.WaitReady();

        Assert.True(menu.NewGameButton.IsVisible());
    }

    [Fact]
    public void MainMenu_ClickNewGame_StartsExploring()
    {
        var menu = new Pages.MainMenuPage(_fixture.Context);
        menu.WaitReady();

        menu.NewGameButton.Click();

        // Wait for game world to load
        _fixture.Context.WaitFor(
            () => !_fixture.Context.IsGameBusy(),
            timeoutMs: 10000,
            description: "game world loaded");
    }
}
```

### Fluent chaining

Controls return the containing page for chaining:

```csharp
[Fact]
public void Settings_CanConfigureAndApply()
{
    var settings = new Pages.SettingsPage(_fixture.Context);
    settings.WaitReady();

    settings
        .VolumeSlider.SetValue(75)
        .FullscreenToggle.Uncheck()
        .PlayerNameInput.SetText("Survivor")
        .ApplyButton.Click();
}
```

### Keyboard input

```csharp
[Fact]
public void Player_CanMoveWithKeyboard()
{
    _fixture.Context.WaitForGameReady();

    // Hold W key to move forward for 500ms
    _fixture.Context.HoldKey(Brinell.Stride.Infrastructure.VirtualKey.W, 500);

    // Press Escape to open menu
    _fixture.Context.PressKey(Brinell.Stride.Infrastructure.VirtualKey.Escape);
}
```

### Wait helpers

```csharp
// Wait for a condition with timeout
_fixture.Context.WaitFor(
    () => _fixture.Context.ElementIsVisible("InventoryPanel"),
    timeoutMs: 5000,
    description: "inventory opened");

// Wait for game to finish loading
_fixture.Context.WaitForGameReady(timeoutMs: 15000);
```

### Screenshots on failure

```csharp
[Fact]
public void SomeTest_TakesScreenshotOnFailure()
{
    try
    {
        // ... test code ...
    }
    catch
    {
        _fixture.Context.SaveScreenshot(
            $"TestResults/Screenshots/SomeTest_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        throw;
    }
}
```

## Available Control Types

| Control | Class | Key Methods |
|---------|-------|-------------|
| Button | `Button<TScope>` | `Click()`, `IsVisible()`, `IsEnabled()`, `WaitEnabled()` |
| CheckBox | `CheckBox<TScope>` | `Check()`, `Uncheck()`, `Toggle()`, `IsChecked()` |
| ToggleButton | `ToggleButton<TScope>` | `Check()`, `Uncheck()`, `Toggle()`, `IsChecked()` |
| EditText | `EditText<TScope>` | `SetText()`, `Clear()`, `Enter()`, `Append()`, `Focus()` |
| TextBlock | `TextBlock<TScope>` | `GetText()`, `AssertText()`, `WaitText()` |
| Slider | `Slider<TScope>` | `SetValue()`, `GetValue()`, `Increment()`, `Decrement()` |
| ComboBox | `ComboBox<TScope>` | Selection methods |
| ListBox | `ListBox<TScope>` | Selection methods |
| Image | `Image<TScope>` | `IsVisible()`, `IsExists()` |
| Panel | `Panel<TScope>` | Container — `IsVisible()`, `WaitVisible()` |
| ProgressBar | `ProgressBar<TScope>` | `GetValue()`, range methods |

All controls share these common methods from `ControlBase`:
- `IsExists()`, `IsVisible()`, `IsEnabled()`, `IsClickable()`
- `WaitVisible()`, `WaitEnabled()`, `WaitClickable()`
- `AssertVisible()`, `AssertEnabled()`, `AssertClickable()`

## Communication Protocol

Commands and responses are JSON over named pipe (`Brinell.Stride.Automation`), one JSON object per line.

### Command format

```json
{
  "type": "Query|Action|Wait|GameQuery",
  "method": "GetState|Click|SimulateKeyPress|...",
  "target": "AutomationId or null",
  "args": [/* optional arguments */],
  "timeoutMs": 10000
}
```

### Response format

```json
{
  "success": true,
  "result": { /* ElementState object or other data */ },
  "error": null,
  "stackTrace": null
}
```

### Commands the game server must handle

| Command | Type | Description |
|---------|------|-------------|
| `GetState` | Query | Return `ElementState` JSON for a UI element |
| `IsGameReady` | Query | Return `true` when game is loaded and interactive |
| `IsBusy` | Query | Return `true` during loading/transitions |
| `Click` | Action | Raise click/tap on the target element |
| `SetElementText` | Action | Set text content of an editable element |
| `SetSliderValue` | Action | Set numeric value on a slider |
| `SetToggleValue` | Action | Set checked/unchecked on a toggle |
| `SimulateKeyPress` | Action | Simulate a single key press (arg: Stride key name) |
| `SimulateKeyHold` | Action | Simulate holding a key (args: key name, duration ms) |
| `SimulateKeyCombination` | Action | Simulate key combo (args: modifier, key) |
| `SelectAll` | Action | Select all text in an editable element |
| `TakeScreenshot` | Action | Return base64-encoded screenshot |
| `Exit` | Action | Gracefully shut down the game |

## Running Tests

```powershell
# Build the game first
dotnet build src/Oravey2.Windows/Oravey2.Windows.csproj

# Run UI tests (game is launched automatically by the fixture)
dotnet test tests/Oravey2.UITests/Oravey2.UITests.csproj

# Or specify game path via environment variable
$env:STRIDE_APP_PATH = "path/to/Oravey2.Windows.exe"
dotnet test tests/Oravey2.UITests/Oravey2.UITests.csproj
```

## Next Steps

1. **Implement the automation server** in `Oravey2.Windows` — a `NamedPipeServerStream` listener that processes `AutomationCommand` JSON, resolves Stride UI elements, and returns `AutomationResponse`
2. **Add `AutomationId` properties** to all Stride UI elements that need testing
3. **Create page objects** for each game screen (main menu, inventory, dialogue, etc.)
4. **Create the `Oravey2.UITests` project** and add test classes
