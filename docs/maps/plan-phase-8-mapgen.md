# Phase 8 — Map Generator (Copilot SDK + MAUI UI)

> **Status:** Not Started  
> **Date:** 2026-04-03  
> **Depends on:** Phase 7 (Blueprint data model & compiler), [06-copilot-sdk-map-generation.md](06-copilot-sdk-map-generation.md)  
> **Scope:** Terrain-only map generation. Quests, factions, NPCs, and entities are deferred to future phases.

---

## Goal

Two new projects that let a designer describe a real-world location and receive a compiled `MapBlueprint` JSON:

1. **`Oravey2.MapGen`** — Class library. Copilot SDK integration, prompt building, custom tools, blueprint validation, generation service. No UI dependency.
2. **`Oravey2.MapGen.App`** — .NET MAUI Windows app. MVVM UI for configuring generation parameters, viewing streaming output, inspecting/editing the generated blueprint, and saving output. Based on the `Brinell.Samples.Maui.App` architecture.

```
Designer fills form  →  MapGen generates blueprint  →  Preview + save
        │                        │                          │
   MAUI App (UI)          Oravey2.MapGen (lib)        JSON output
```

---

## Terrain-Only Blueprint Scope

For this phase, the `MapBlueprint` the LLM generates is limited to **static world geometry**:

```jsonc
{
  "$schema": "map-blueprint-v1",
  "name": "...",
  "description": "...",
  "source": { "realWorldLocation": "...", "notes": "..." },
  "dimensions": { "chunksWide": N, "chunksHigh": N },
  "terrain": { "baseElevation": N, "regions": [...], "surfaces": [...] },
  "water": { "rivers": [...], "lakes": [...] },
  "roads": [...],
  "buildings": [...],
  "zones": [...],
  "playerStart": { "chunkX": N, "chunkY": N, "localTileX": N, "localTileY": N },
  "timeOfDay": "...",
  "weatherDefault": "..."
}
```

**Excluded for now:** `entities.npcs[]`, `entities.enemyGroups[]`, `entities.containers[]`, `questHooks[]`. The schema classes will have nullable fields for these so the structure is forward-compatible, but the system prompt explicitly tells the LLM to omit them.

---

## Project 1 — `Oravey2.MapGen` (class library)

### Location

```
src/Oravey2.MapGen/
├── Oravey2.MapGen.csproj
├── Services/
│   ├── MapGeneratorService.cs          # CopilotClient wrapper, session management
│   ├── PromptBuilder.cs                # System + user prompt construction
│   └── BlueprintCollector.cs           # Streaming event → MapBlueprint extraction
├── Tools/
│   ├── ValidateBlueprintTool.cs        # validate_blueprint custom tool
│   ├── LookupAssetTool.cs             # lookup_asset custom tool
│   ├── CheckOverlapTool.cs            # check_overlap custom tool
│   ├── CheckWalkabilityTool.cs        # check_walkability custom tool
│   └── ListPrefabsTool.cs            # list_available_prefabs custom tool
├── Assets/
│   ├── IAssetRegistry.cs              # Interface for asset lookup
│   ├── AssetRegistry.cs               # JSON-backed asset catalog
│   └── asset-catalog.json             # Embedded resource: known prefabs, meshes, loot tables
├── Validation/
│   ├── IBlueprintValidator.cs         # Interface (reuses Phase 7 validator if available)
│   └── TerrainBlueprintValidator.cs   # Terrain-scope validation rules
├── Models/
│   ├── MapGenerationRequest.cs        # Input: location, dimensions, constraints
│   ├── GenerationProgress.cs          # Progress events for UI consumption
│   └── GenerationResult.cs            # Output: blueprint + validation + metadata
└── Spatial/
    ├── SpatialUtils.cs                # Overlap detection, walkability checks
    └── BuildingFootprint.cs           # Footprint geometry for overlap tool
```

### Step 8.1 — Project setup & models

**File:** `src/Oravey2.MapGen/Oravey2.MapGen.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="GitHub.Copilot.SDK" Version="0.2.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Oravey2.Core\Oravey2.Core.csproj" />
  </ItemGroup>
</Project>
```

**File:** `src/Oravey2.MapGen/Models/MapGenerationRequest.cs`

```csharp
public sealed record MapGenerationRequest
{
    public required string LocationName { get; init; }
    public required string GeographyDescription { get; init; }
    public required string PostApocContext { get; init; }
    public required int ChunksWide { get; init; }
    public required int ChunksHigh { get; init; }
    public required int MinLevel { get; init; }
    public required int MaxLevel { get; init; }
    public required string DifficultyDescription { get; init; }
    public required string[] Factions { get; init; }
    public string? Model { get; init; }                      // e.g. "gpt-4.1"
    public string TimeOfDay { get; init; } = "Dawn";
    public string WeatherDefault { get; init; } = "overcast";
}
```

**File:** `src/Oravey2.MapGen/Models/GenerationProgress.cs`

```csharp
public sealed record GenerationProgress
{
    public required GenerationPhase Phase { get; init; }
    public required string Message { get; init; }
    public string? StreamDelta { get; init; }                // Streaming text chunk
    public string? ToolName { get; init; }                   // Tool call in progress
    public string? ToolResult { get; init; }                 // Tool result summary
}

public enum GenerationPhase
{
    Initializing,
    Prompting,
    Streaming,
    ToolCall,
    Validating,
    Fixing,
    Complete,
    Error
}
```

**File:** `src/Oravey2.MapGen/Models/GenerationResult.cs`

```csharp
public sealed record GenerationResult
{
    public required bool Success { get; init; }
    public MapBlueprint? Blueprint { get; init; }
    public string? RawJson { get; init; }
    public string[]? ValidationErrors { get; init; }
    public string? ErrorMessage { get; init; }
    public string? SessionId { get; init; }                  // For session resume
    public TimeSpan Elapsed { get; init; }
}
```

**Tests:** `tests/Oravey2.Tests/MapGen/MapGenerationRequestTests.cs`
- Construct with all required fields → no exception
- Default Model is null
- Default TimeOfDay is "Dawn"

---

### Step 8.2 — Asset registry

**File:** `src/Oravey2.MapGen/Assets/IAssetRegistry.cs`

```csharp
public interface IAssetRegistry
{
    IReadOnlyList<AssetEntry> Search(string assetType, string query);
    IReadOnlyList<AssetEntry> ListPrefabs(string category);
    bool Exists(string assetType, string id);
}

public sealed record AssetEntry(string Id, string Description, string[] Tags);
```

**File:** `src/Oravey2.MapGen/Assets/AssetRegistry.cs`

Loads from embedded `asset-catalog.json`. Categories: `building`, `terrain_mesh`, `surface`. (No NPC, enemy, container, loot_table categories yet — those come later.)

**File:** `src/Oravey2.MapGen/Assets/asset-catalog.json`

```jsonc
{
  "building": [
    { "id": "buildings/ruined_office.glb", "description": "Multi-story ruined office block", "tags": ["large", "ruin"] },
    { "id": "buildings/radio_tower.glb", "description": "Small communication tower", "tags": ["small", "infrastructure"] },
    { "id": "buildings/elder_house.glb", "description": "Two-story residential building", "tags": ["large", "residential"] }
    // ... populated from docs/maps/05-free-3d-asset-sources.md
  ],
  "surface": [
    { "id": "Asphalt", "description": "Cracked road surface", "tags": ["road"] },
    { "id": "Concrete", "description": "Broken concrete", "tags": ["urban"] },
    { "id": "Dirt", "description": "Packed dirt ground", "tags": ["natural"] },
    { "id": "Grass", "description": "Overgrown grass", "tags": ["natural"] },
    { "id": "Rock", "description": "Exposed rock", "tags": ["natural"] },
    { "id": "Rubble", "description": "Debris and rubble", "tags": ["urban", "damaged"] }
  ]
}
```

**Tests:** `tests/Oravey2.Tests/MapGen/AssetRegistryTests.cs`
- Search("building", "office") → returns ruined_office
- Search("surface", "road") → returns Asphalt
- ListPrefabs("building") → returns all buildings
- Exists("surface", "Asphalt") → true
- Exists("surface", "Nonexistent") → false
- Search with empty query → returns all in category

---

### Step 8.3 — Terrain-scope validator

**File:** `src/Oravey2.MapGen/Validation/TerrainBlueprintValidator.cs`

Subset of Phase 7's `BlueprintValidator` — only checks terrain-relevant fields:

| Check | Rule |
|-------|------|
| Dimensions | `chunksWide > 0`, `chunksHigh > 0` |
| Bounds | All region coordinates within dimensions |
| Building overlap | No two building footprints share tiles |
| Road bounds | Road path points within chunk grid |
| Zone bounds | Zone `chunkRange` within dimensions |
| Player start | Within dimensions |
| Surface types | All surface type names exist in asset registry |
| Building meshes | All `meshAsset` values exist in asset registry |

**Tests:** `tests/Oravey2.Tests/MapGen/TerrainBlueprintValidatorTests.cs`
- Valid terrain-only blueprint → no errors
- Zero dimensions → error
- Building outside bounds → error
- Overlapping buildings → error
- Unknown surface type → error
- Player start outside grid → error

---

### Step 8.4 — Spatial utilities

**File:** `src/Oravey2.MapGen/Spatial/SpatialUtils.cs`

```csharp
public static class SpatialUtils
{
    public static IReadOnlyList<(string A, string B)> FindOverlaps(
        IReadOnlyList<BuildingFootprint> buildings);

    public static bool IsTileWithinBounds(
        int chunkX, int chunkY, int localTileX, int localTileY,
        int chunksWide, int chunksHigh, int tilesPerChunk = 16);

    public static bool IsTileOnWater(
        int chunkX, int chunkY, int localTileX, int localTileY,
        WaterBlueprint? water);

    public static bool IsTileOnBuilding(
        int chunkX, int chunkY, int localTileX, int localTileY,
        IReadOnlyList<BuildingFootprint> buildings);
}
```

**Tests:** `tests/Oravey2.Tests/MapGen/SpatialUtilsTests.cs`
- Two non-overlapping buildings → no conflicts
- Two overlapping buildings → returns pair
- Tile within bounds → true
- Tile outside bounds → false
- Tile on river → is water
- Tile on building footprint → true

---

### Step 8.5 — Custom tools

Five tool classes, each a thin wrapper exposing an `AIFunction` that calls into the registry/validator/spatial utils.

| File | Tool Name | Calls Into |
|------|-----------|------------|
| `Tools/ValidateBlueprintTool.cs` | `validate_blueprint` | `TerrainBlueprintValidator` |
| `Tools/LookupAssetTool.cs` | `lookup_asset` | `IAssetRegistry.Search()` |
| `Tools/CheckOverlapTool.cs` | `check_overlap` | `SpatialUtils.FindOverlaps()` |
| `Tools/CheckWalkabilityTool.cs` | `check_walkability` | `SpatialUtils` composite check |
| `Tools/ListPrefabsTool.cs` | `list_available_prefabs` | `IAssetRegistry.ListPrefabs()` |

Each tool class exposes:

```csharp
public sealed class ValidateBlueprintTool
{
    public AIFunction Build(IBlueprintValidator validator);
}
```

**Tests:** `tests/Oravey2.Tests/MapGen/ToolTests/` — unit test each tool's handler method directly (no SDK needed):
- `ValidateBlueprintToolTests.cs` — valid JSON → `{valid:true}`, invalid → errors listed
- `LookupAssetToolTests.cs` — known asset returns match, unknown returns empty
- `CheckOverlapToolTests.cs` — overlapping footprints detected
- `CheckWalkabilityToolTests.cs` — water tile → not walkable, open tile → walkable
- `ListPrefabsToolTests.cs` — returns correct category entries

---

### Step 8.6 — Prompt builder

**File:** `src/Oravey2.MapGen/Services/PromptBuilder.cs`

Two methods:

```csharp
public sealed class PromptBuilder
{
    public string BuildSystemPrompt();                              // Schema + rules
    public string BuildUserPrompt(MapGenerationRequest request);   // Location + constraints
}
```

The system prompt:
- Includes the terrain-only subset of the MapBlueprint schema
- Instructs the LLM to **omit** entities, questHooks
- Instructs tool usage order: list prefabs → generate → validate → fix
- Requires output in a `\`\`\`json` code fence

**Tests:** `tests/Oravey2.Tests/MapGen/PromptBuilderTests.cs`
- System prompt contains "map-blueprint-v1"
- System prompt contains "omit" or "exclude" for entities
- User prompt contains location name
- User prompt contains dimensions
- User prompt contains factions

---

### Step 8.7 — Blueprint collector

**File:** `src/Oravey2.MapGen/Services/BlueprintCollector.cs`

Subscribes to session events and extracts the final blueprint JSON:

```csharp
public sealed class BlueprintCollector
{
    public event Action<GenerationProgress>? OnProgress;

    public Task<GenerationResult> CollectAsync(
        CopilotSession session,
        string userPrompt,
        CancellationToken ct = default);
}
```

- Streams `AssistantMessageDeltaEvent` → fires `OnProgress` with `StreamDelta`
- Tracks `ToolExecutionStartEvent` / `ToolExecutionCompleteEvent` → fires `OnProgress` with `ToolName`
- On `SessionIdleEvent` → extracts JSON from accumulated response, deserializes to `MapBlueprint`
- On `SessionErrorEvent` → returns `GenerationResult` with error

**Tests:** (integration-level, may require mocking the session events)
- `BlueprintCollectorTests.cs`
  - JSON inside code fence → extracted correctly
  - JSON without code fence → extracted correctly
  - No JSON → error result
  - Error event → error result with message

---

### Step 8.8 — MapGeneratorService

**File:** `src/Oravey2.MapGen/Services/MapGeneratorService.cs`

The top-level orchestrator. Owns the `CopilotClient`, creates sessions, wires tools and collector.

```csharp
public sealed class MapGeneratorService : IAsyncDisposable
{
    public event Action<GenerationProgress>? OnProgress;

    public MapGeneratorService(IAssetRegistry assets, IBlueprintValidator validator);

    public Task<GenerationResult> GenerateAsync(
        MapGenerationRequest request,
        CancellationToken ct = default);

    public Task<GenerationResult> RefineAsync(
        string sessionId,
        string refinementPrompt,
        CancellationToken ct = default);

    public ValueTask DisposeAsync();
}
```

- `GenerateAsync`: creates session → sends prompt → collects result
- `RefineAsync`: resumes session → sends follow-up prompt → collects result
- Disables built-in tools via `ExcludedTools = ["*"]` and permission handler
- Selects model based on `request.Model` or defaults to `"gpt-4.1"`

**Tests:** (requires Copilot CLI available — mark with `[Trait("Category", "Integration")]`)
- `MapGeneratorServiceTests.cs`
  - Service creates and disposes without error
  - Generate with mock request → returns a result (success or error)

---

## Project 2 — `Oravey2.MapGen.App` (MAUI Windows app)

### Location

```
src/Oravey2.MapGen.App/
├── Oravey2.MapGen.App.csproj
├── App.xaml / App.xaml.cs
├── MauiProgram.cs
├── Converters/
│   └── Converters.cs
├── Models/
│   └── (reuses Oravey2.MapGen models)
├── ViewModels/
│   ├── GeneratorViewModel.cs        # Main generation form + output
│   ├── BlueprintPreviewViewModel.cs # JSON tree view of generated blueprint
│   └── SettingsViewModel.cs         # Model selection, BYOK, CLI path
├── Views/
│   ├── GeneratorView.xaml / .cs     # Form + streaming output log
│   ├── BlueprintPreviewView.xaml / .cs   # JSON preview + zone/building summary
│   └── SettingsView.xaml / .cs      # Settings panel
├── Pages/
│   └── MainPage.xaml / .cs          # TabbedPage host (Generate | Preview | Settings)
├── Platforms/Windows/
│   └── App.xaml.cs
└── Resources/
    ├── AppIcon/
    ├── Splash/
    └── Styles/
        ├── Colors.xaml
        └── Styles.xaml
```

### Step 8.9 — MAUI project setup

**File:** `src/Oravey2.MapGen.App/Oravey2.MapGen.App.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net10.0-windows10.0.19041.0</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseMaui>true</UseMaui>
    <SingleProject>true</SingleProject>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Maui.Controls" />
    <PackageReference Include="Microsoft.Extensions.Logging.Debug" />
    <PackageReference Include="CommunityToolkit.Maui" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Oravey2.MapGen\Oravey2.MapGen.csproj" />
    <ProjectReference Include="..\Oravey2.Core\Oravey2.Core.csproj" />
  </ItemGroup>
</Project>
```

**File:** `src/Oravey2.MapGen.App/MauiProgram.cs`

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
               .UseMauiCommunityToolkit()
               .ConfigureFonts(fonts =>
               {
                   fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
               });

        // Services
        builder.Services.AddSingleton<IAssetRegistry, AssetRegistry>();
        builder.Services.AddSingleton<IBlueprintValidator, TerrainBlueprintValidator>();
        builder.Services.AddSingleton<MapGeneratorService>();

        // ViewModels
        builder.Services.AddTransient<GeneratorViewModel>();
        builder.Services.AddTransient<BlueprintPreviewViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
```

Follows the Brinell.Samples.Maui.App pattern:
- `App.xaml.cs` sets `MainPage = new MainPage()`
- `MainPage` is a `TabbedPage` with three tabs
- ViewModels use a common base class with `INotifyPropertyChanged` via `SetProperty<T>`

---

### Step 8.10 — GeneratorViewModel & View

**GeneratorViewModel** — The main form + generation logic:

```csharp
public sealed class GeneratorViewModel : BaseViewModel
{
    // --- Input fields (bound two-way) ---
    public string LocationName { get; set; }
    public string GeographyDescription { get; set; }
    public string PostApocContext { get; set; }
    public int ChunksWide { get; set; } = 4;
    public int ChunksHigh { get; set; } = 4;
    public int MinLevel { get; set; } = 1;
    public int MaxLevel { get; set; } = 5;
    public string DifficultyDescription { get; set; }
    public string Factions { get; set; }          // Comma-separated, split on send
    public string TimeOfDay { get; set; } = "Dawn";
    public string WeatherDefault { get; set; } = "overcast";

    // --- Output (bound one-way) ---
    public string StreamingLog { get; }            // Appended during generation
    public string StatusMessage { get; }
    public bool IsGenerating { get; }
    public string? LastGeneratedJson { get; }
    public string? LastSessionId { get; }

    // --- Commands ---
    public ICommand GenerateCommand { get; }       // Async, disabled while generating
    public ICommand CancelCommand { get; }         // Cancels in-progress generation
    public ICommand SaveBlueprintCommand { get; }  // Save JSON to file
    public ICommand CopyJsonCommand { get; }       // Copy JSON to clipboard
}
```

**GeneratorView.xaml** — Layout:

```
┌──────────────────────────────────────────────┐
│  Location: [________________________]        │
│  Geography: [multiline_______________]       │
│  Post-Apoc: [multiline_______________]       │
│                                              │
│  Chunks: [4] wide × [4] high                │
│  Level range: [1] – [5]                      │
│  Difficulty: [________________________]      │
│  Factions: [________________________]        │
│  Time: [Dawn ▼]   Weather: [overcast ▼]      │
│                                              │
│  [ Generate ]  [ Cancel ]                    │
│                                              │
│  ── Streaming Log ──────────────────────     │
│  │ Initializing session...              │    │
│  │ Tool: list_available_prefabs         │    │
│  │ Generating terrain regions...        │    │
│  │ Tool: validate_blueprint → valid ✓   │    │
│  └──────────────────────────────────────┘    │
│                                              │
│  Status: Complete (12.3s)                    │
│  [ Save Blueprint ]  [ Copy JSON ]           │
└──────────────────────────────────────────────┘
```

The `OnProgress` event from `MapGeneratorService` dispatches to the UI thread and appends to `StreamingLog`.

---

### Step 8.11 — BlueprintPreviewViewModel & View

**BlueprintPreviewViewModel** — Shows the generated blueprint in a structured view:

```csharp
public sealed class BlueprintPreviewViewModel : BaseViewModel
{
    public string? BlueprintJson { get; set; }        // Raw JSON (set from GeneratorVM)
    public string? MapName { get; }
    public string? MapDescription { get; }
    public string? Dimensions { get; }                // "4×4 chunks (64×64 tiles)"
    public int TerrainRegionCount { get; }
    public int RoadCount { get; }
    public int BuildingCount { get; }
    public int ZoneCount { get; }
    public ObservableCollection<string> ValidationErrors { get; }

    public ICommand RevalidateCommand { get; }        // Re-run validator on current JSON
}
```

**BlueprintPreviewView.xaml** — Layout:

```
┌──────────────────────────────────────────────┐
│  Map: Sector 7 — Downtown Portland Ruins     │
│  4×4 chunks (64×64 tiles)                    │
│                                              │
│  Terrain Regions: 3   Roads: 2               │
│  Buildings: 5         Zones: 3               │
│                                              │
│  [ Revalidate ]                              │
│                                              │
│  ── Validation ──                            │
│  ✓ No errors                                 │
│                                              │
│  ── Raw JSON ───────────────────────────     │
│  │ {                                   │     │
│  │   "$schema": "map-blueprint-v1",    │     │
│  │   "name": "Sector 7..."            │     │
│  │   ...                               │     │
│  └─────────────────────────────────────┘     │
└──────────────────────────────────────────────┘
```

---

### Step 8.12 — SettingsViewModel & View

**SettingsViewModel** — Configuration panel:

```csharp
public sealed class SettingsViewModel : BaseViewModel
{
    public string SelectedModel { get; set; } = "gpt-4.1";
    public ObservableCollection<string> AvailableModels { get; }  // gpt-4.1, claude-sonnet-4.5, o4-mini

    // BYOK
    public bool UseBYOK { get; set; }
    public string? ProviderType { get; set; }     // openai, azure, anthropic
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }           // masked in UI

    // CLI
    public string? CliPath { get; set; }          // Override Copilot CLI path

    public ICommand SaveSettingsCommand { get; }
    public ICommand TestConnectionCommand { get; }
}
```

Settings persist to `Preferences` (MAUI) or a local JSON file.

---

### Step 8.13 — BaseViewModel (shared)

**File:** `src/Oravey2.MapGen.App/ViewModels/BaseViewModel.cs`

Minimal MVVM base following Brinell.Samples.Shared `ParentViewModel` pattern:

```csharp
public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
```

---

## Step Summary

| Step | Project | Deliverable | Tests |
|------|---------|-------------|-------|
| 8.1 | MapGen | csproj + request/progress/result models | `MapGenerationRequestTests.cs` |
| 8.2 | MapGen | `IAssetRegistry` + `AssetRegistry` + catalog JSON | `AssetRegistryTests.cs` |
| 8.3 | MapGen | `TerrainBlueprintValidator` | `TerrainBlueprintValidatorTests.cs` |
| 8.4 | MapGen | `SpatialUtils` + `BuildingFootprint` | `SpatialUtilsTests.cs` |
| 8.5 | MapGen | 5 custom tool classes | `ToolTests/*.cs` (5 files) |
| 8.6 | MapGen | `PromptBuilder` | `PromptBuilderTests.cs` |
| 8.7 | MapGen | `BlueprintCollector` | `BlueprintCollectorTests.cs` |
| 8.8 | MapGen | `MapGeneratorService` | `MapGeneratorServiceTests.cs` (integration) |
| 8.9 | MapGen.App | MAUI csproj + App + MauiProgram + MainPage (tabs) | Manual verification |
| 8.10 | MapGen.App | GeneratorViewModel + GeneratorView | Manual verification |
| 8.11 | MapGen.App | BlueprintPreviewViewModel + BlueprintPreviewView | Manual verification |
| 8.12 | MapGen.App | SettingsViewModel + SettingsView | Manual verification |
| 8.13 | MapGen.App | BaseViewModel | — (trivial base) |

---

## Implementation Order

```
8.1 ──→ 8.2 ──→ 8.3 ──→ 8.4 ──→ 8.5 ──→ 8.6 ──→ 8.7 ──→ 8.8
                                                              │
8.9 ──→ 8.13 ──→ 8.12 ──→ 8.10 ──→ 8.11 ◄───────────────────┘
                                                (links to generator service)
```

Steps 8.1–8.4 (models, registry, validator, spatial) have no SDK dependency and can be built and tested immediately. Steps 8.5–8.8 require the Copilot SDK NuGet. Steps 8.9–8.13 can proceed in parallel once models exist.

---

## Verification

```powershell
# Unit tests (no Copilot CLI needed)
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~MapGen" --verbosity quiet

# Integration tests (requires Copilot CLI)
dotnet test tests/Oravey2.Tests --filter "Category=Integration" --verbosity quiet

# Build MAUI app
dotnet build src/Oravey2.MapGen.App -c Debug

# Run MAUI app
dotnet run --project src/Oravey2.MapGen.App
```

---

## Future Extensions (Out of Scope)

These are explicitly deferred and will be separate phases:

- **Entity generation** — NPCs, enemies, containers (`entities.*` in schema)
- **Quest hook generation** — `questHooks[]` in schema
- **Faction system integration** — faction definitions, territory assignment
- **Blueprint-to-compiler pipeline** — connecting `MapGeneratorService` output to Phase 7 compiler
- **Map preview rendering** — 2D/3D tile preview in the MAUI app
- **Session history** — list/resume past generation sessions
- **Batch generation** — generate multiple maps from a CSV/spreadsheet of locations
