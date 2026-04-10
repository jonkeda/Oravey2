# Step 09d — Assets Step Redesign: Town-Driven Asset Queue

## Problem

The current Assets step (Step 09) presents a flat list of `AssetRequest`
records built by scanning `design.json` files. The user sees a `CollectionView`
with location names and prompt snippets, but there is no town grouping, no
building context, and no connection to the placed-building data from Step 08.
A user arriving from the Town Design step (07) and Town Maps step (08) expects
to see **their towns and buildings** — not an opaque asset queue.

Key shortcomings:

1. **No town grouping** — assets are a flat list; the user cannot quickly see
   which town needs work.
2. **No building context** — the landmark/key-location distinction and
   placement details (footprint, floors, condition) are invisible.
3. **No mesh status at a glance** — it is not obvious which buildings already
   have real meshes vs. primitives vs. nothing.
4. **Disconnect from Step 08** — the queue is built from `design.json` only;
   it ignores `buildings.json` placement data entirely.

## Redesigned Approach

### Data source: merge design + placement

Instead of scanning only `design.json`, the redesigned step should merge data
from **both** previous steps:

| Source | Data | Step |
|--------|------|------|
| `design.json` | Town name, landmark, key locations, visual descriptions, size categories | 07 |
| `buildings.json` | Placed building IDs, current `meshAsset` path, footprint, floors, condition | 08 |
| `props.json` | Placed props, current `meshAsset` path | 08 |

The merged model gives the view everything it needs:

```
Town "Island Haven"
├── ★ The Beacon  (landmark, large)  →  meshes/primitives/pyramid.glb  [primitive]
├── ● Harbor Dock  (key, medium)     →  meshes/primitives/cube.glb     [primitive]
├── ● Community Hall  (key, medium)  →  (none)                         [missing]
├── ● Boat Works  (key, small)       →  meshes/boat-works.glb          [ready]
└── …
```

### 9d.1 `TownAssetSummary` model

New read-only model that merges both sources per town:

```csharp
public sealed record TownAssetSummary(
    string TownName,
    string GameName,
    string LayoutStyle,
    List<BuildingAssetEntry> Buildings,
    List<PropAssetEntry> Props);

public sealed record BuildingAssetEntry(
    string BuildingId,          // from buildings.json
    string Name,                // matched from design.json
    string Role,                // "landmark" | "key" | "generic"
    string SizeCategory,        // from design.json
    string VisualDescription,   // the Meshy prompt (editable)
    string CurrentMeshPath,     // from buildings.json meshAsset
    MeshStatus MeshStatus,      // None, Primitive, Generating, Ready, Failed
    int Floors,
    float Condition);

public sealed record PropAssetEntry(
    string PropId,
    string CurrentMeshPath,
    MeshStatus MeshStatus);

public enum MeshStatus
{
    None,         // meshAsset is empty/null or file doesn't exist
    Primitive,    // meshAsset points to meshes/primitives/*
    Generating,   // Meshy task in progress
    Ready,        // real .glb exists on disk
    Failed        // last Meshy attempt failed
}
```

### 9d.2 `TownAssetScanner`

Replaces `AssetQueueBuilder` with a richer scanner:

```csharp
public sealed class TownAssetScanner
{
    /// Scans all towns in the content pack and returns merged summaries.
    public List<TownAssetSummary> Scan(string contentPackPath);
}
```

Algorithm:
1. Enumerate `towns/*/design.json` files.
2. For each town, also load `towns/*/buildings.json` and `towns/*/props.json`.
3. Match placed buildings to design entries by name.
4. Classify `MeshStatus` by inspecting the `meshAsset` path:
   - null/empty/placeholder → `None`
   - contains `primitives/` → `Primitive`
   - file exists on disk → `Ready`
   - else → `None`
5. Return one `TownAssetSummary` per town.

### 9d.3 Redesigned `AssetsStepView` layout

```
┌─────────────────────────────────────────────────────────────────────┐
│  ⑦ 3D Assets                                                       │
│  Generate unique 3D assets or assign dummy primitives.     [Next →] │
├────────────────────────┬────────────────────────────────────────────┤
│  TOWNS & BUILDINGS     │  SELECTED BUILDING DETAIL                  │
│                        │                                            │
│  Filter: [All ▾]       │  ┌─────────────────────────────────────┐  │
│                        │  │ The Beacon — Island Haven            │  │
│  ▼ Island Haven (1/8)  │  │ Role: Landmark  Size: Large          │  │
│    ★ The Beacon   🟡   │  │ Floors: 3   Condition: 0.85          │  │
│    ● Harbor Dock  🟢   │  │ Mesh: primitives/pyramid.glb  🟡     │  │
│    ● Community H  ⚫   │  └─────────────────────────────────────┘  │
│    ● Boat Works   🟢   │                                            │
│    ● Field Clinic 🟡   │  Visual Prompt                             │
│    ● Supply Wareh 🟡   │  ┌─────────────────────────────────────┐  │
│    ● Guard Garris 🟡   │  │ A massive coastal fortress with     │  │
│    ● Scrap Yard   🟡   │  │ crumbling stone walls overlooking   │  │
│                        │  │ the North Sea...                     │  │
│  ▼ Havenburg (0/5)     │  └─────────────────────────────────────┘  │
│    ★ Fort Kijkduin ⚫  │                                            │
│    ● Market Hall   ⚫   │  Generation Progress                      │
│    ● Barracks      ⚫   │  ████████░░░░░░░░  52%                   │
│    ● Dry Dock      ⚫   │                                            │
│    ● Clinic        ⚫   │  [Generate]  [Accept]  [Reject & Redo]   │
│                        │                                            │
├────────────────────────┴────────────────────────────────────────────┤
│  Towns: 5   Buildings: 32   Ready: 8   Pending: 21   Failed: 3     │
│  [Generate All Pending]  [Assign Dummy Meshes]  ☐ Auto-accept      │
│                                                          [Cancel]   │
└─────────────────────────────────────────────────────────────────────┘
```

Key changes:

- **Left panel: tree grouped by town** — each town is a collapsible group
  showing its buildings underneath. The town header shows a completion count
  (e.g., "1/8 ready"). Buildings show their role icon (★ landmark, ● key
  location) and a status dot.
- **Status dots**: ⚫ None, 🟡 Primitive, 🔵 Generating, 🟢 Ready, 🔴 Failed.
- **Right panel: building detail** — shows full building context (role, size,
  floors, condition, current mesh path) plus the editable visual prompt and
  generation controls.
- **Summary bar at bottom** — aggregate counts across all towns.
- **Filter applies to mesh status**, not the old `AssetStatus` enum.

### 9d.4 Updated `AssetsStepViewModel`

Major changes:

| Property | Type | Description |
|----------|------|-------------|
| `Towns` | `ObservableCollection<TownAssetGroup>` | Grouped town data |
| `FilteredTowns` | `ObservableCollection<TownAssetGroup>` | After status filter |
| `SelectedBuilding` | `BuildingAssetEntry` | Currently selected building |
| `SummaryText` | `string` | "Towns: 5  Buildings: 32  Ready: 8 …" |
| `FilterMode` | `MeshStatus?` | null = all, else filter to that status |

```csharp
public sealed class TownAssetGroup
{
    public string TownName { get; }
    public string GameName { get; }
    public int ReadyCount { get; }
    public int TotalCount { get; }
    public ObservableCollection<BuildingAssetEntry> Buildings { get; }
    public bool IsExpanded { get; set; }
}
```

Commands:
- `GenerateCommand` → generate for `SelectedBuilding`
- `AcceptCommand` → accept and update `buildings.json`
- `RejectCommand` → reset to Pending, allow prompt edit
- `GenerateAllPendingCommand` → sequential queue across all towns
- `AssignDummyMeshesCommand` → runs `DummyMeshAssigner` for all towns
- `CancelCommand` → cancellation token

### 9d.5 Generation & accept flow (unchanged logic, better context)

The actual Meshy API interaction is identical to Step 09:

1. **Generate**: takes `VisualDescription` from the selected
   `BuildingAssetEntry`, calls `MeshyClient.CreateTextTo3DAsync()`.
2. **Poll**: every 5 s via `MeshyClient.GetTaskAsync()`, updates progress.
3. **Accept**: downloads `.glb`, saves to `assets/meshes/{assetId}.glb`,
   writes `.meta.json`, updates `buildings.json` entry's `meshAsset` path.
4. **Reject**: resets status to `None`, user edits prompt, retries.

The only difference is richer UI feedback — the user sees the building in
context (which town, what role, how many floors, current mesh path).

### 9d.6 `buildings.json` update on accept

When a Meshy asset is accepted for a building:

```json
// Before
{ "id": "building_0", "name": "The Beacon", "meshAsset": "meshes/primitives/pyramid.glb", ... }

// After
{ "id": "building_0", "name": "The Beacon", "meshAsset": "meshes/island-haven-the-beacon.glb", ... }
```

The view model re-scans the town to update the `MeshStatus` and completion
count.

### 9d.7 Tests

- `TownAssetScanner` — scan test content pack, verify merged data, verify
  `MeshStatus` classification (none/primitive/ready)
- `TownAssetScanner` — towns without `buildings.json` still appear (buildings
  show as unplaced)
- ViewModel — selecting a building populates detail panel bindings
- ViewModel — filter by status reduces visible entries
- ViewModel — generate/accept flow updates `MeshStatus` and completion counts
- ViewModel — `AssignDummyMeshes` assigns primitives and refreshes status
- ViewModel — batch generation with cancellation
- Mock `MeshyClient` for all tests (no real API calls)

## Dependencies

- Step 07 (`design.json` — `TownDesign`, `LandmarkBuilding`, `KeyLocation`)
- Step 08 (`buildings.json` — `PlacedBuilding`, `PlacedProp`)
- Step 09b (`DummyMeshAssigner`, `PrimitiveMeshWriter`)
- Existing: `MeshyClient`

## Migration from Step 09

- `AssetQueueBuilder` → replaced by `TownAssetScanner`
- `AssetRequest` / `AssetItem` → replaced by `BuildingAssetEntry` /
  `TownAssetGroup`
- `AssetsStepView.xaml` → rewritten with grouped `CollectionView`
- `AssetsStepViewModel` → rewritten with town-grouped data
- `DummyMeshAssigner` (Step 09b) → unchanged, still invoked from the VM

## Estimated scope

- New files: `TownAssetSummary.cs`, `TownAssetScanner.cs`, `TownAssetGroup.cs`
- Rewritten: `AssetsStepView.xaml`, `AssetsStepViewModel.cs`
- Removed: `AssetQueueBuilder.cs`, `AssetRequest.cs`, `AssetItem.cs` (or
  kept as deprecated)
- Modified: none outside the assets step
