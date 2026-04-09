# Step 02 — Pipeline State Model

## Goal

Create the `PipelineState` model and persistence logic so that pipeline
progress survives app restarts and every subsequent step can read/write its
status.

## Deliverables

### 2.1 `PipelineState` record (in `Oravey2.MapGen`)

```csharp
public sealed class PipelineState
{
    public string RegionName { get; set; }
    public string ContentPackPath { get; set; }       // e.g. "content/Oravey2.Apocalyptic.NL.NH"
    public int CurrentStep { get; set; }               // 1–8

    public RegionStepState Region { get; set; }
    public DownloadStepState Download { get; set; }
    public ParseStepState Parse { get; set; }
    public TownSelectionStepState TownSelection { get; set; }
    public TownDesignStepState TownDesign { get; set; }
    public TownMapsStepState TownMaps { get; set; }
    public AssetsStepState Assets { get; set; }
    public AssemblyStepState Assembly { get; set; }
}
```

Each step-state class tracks `Completed` (bool) plus step-specific fields
(counts, lists of completed town names, etc.) — see `mapgen-ui-v3.md` for the
full schema.

### 2.2 `PipelineStateService`

```csharp
public sealed class PipelineStateService
{
    Task<PipelineState> LoadAsync(string regionName);
    Task SaveAsync(PipelineState state);
}
```

- Reads/writes `data/regions/{name}/pipeline-state.json`
- Uses `System.Text.Json` with indented formatting for readability
- Creates the file on first save if it doesn't exist

### 2.3 Unit tests

- Round-trip serialize/deserialize
- Load returns default state when file doesn't exist
- Save creates intermediate directories
- Step advancement logic (can only advance when current step is completed)

## Dependencies

- Step 01 (content pack path is stored in state)

## Estimated scope

- New files: ~4 (state model, service, tests)
- In `Oravey2.MapGen` project
