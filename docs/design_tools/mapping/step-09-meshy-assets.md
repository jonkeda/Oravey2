# Step 09 — Meshy 3D Asset Generation

## Goal

Generate unique 3D assets for landmark buildings and key locations using the
Meshy API. Assets are created one at a time, reviewed, and saved to the
content pack.

## Deliverables

### 9.1 Asset queue builder

New utility that scans all `design.json` files in the content pack and builds
a list of required assets:

```csharp
public sealed class AssetQueueBuilder
{
    public List<AssetRequest> BuildQueue(string contentPackPath);
}

public sealed record AssetRequest(
    string AssetId,               // derived from town + location name
    string TownName,
    string LocationName,
    string VisualDescription,     // the Meshy prompt
    string SizeCategory,
    AssetStatus Status);          // Pending, Generating, Ready, Failed

public enum AssetStatus { Pending, Generating, Ready, Failed }
```

The queue deduplicates: if two towns reference the same visual description,
they share one asset.

### 9.2 `AssetsStepView` / `AssetsStepViewModel`

- **Left panel — asset queue**:
  - Grouped by town
  - Each entry: asset name, prompt snippet (first 60 chars), status badge
  - Filter bar: All / Pending / Ready / Failed
- **Right panel — asset detail** for selected item:
  - Full prompt text (editable `Editor` control)
  - **[Generate]** → calls `MeshyClient.CreateTextTo3DAsync()`
  - Progress bar (polls `MeshyClient.GetTaskAsync()` every 5s)
  - Thumbnail preview (from Meshy response `thumbnail_url`)
  - **[Accept]** → downloads `.glb`, saves `.glb` + `.meta.json`
  - **[Reject & Re-generate]** → allows prompt editing before retry
- **Batch bar**:
  - **[Generate All Pending]** → sequential queue
  - Toggle: auto-accept vs pause-for-review after each

### 9.3 Asset download & storage

On accept:
1. Download `.glb` from Meshy `model_urls.glb`
2. Save to `content/Oravey2.Apocalyptic.NL.NH/assets/meshes/{assetId}.glb`
3. Write `.meta.json`:

```json
{
  "assetId": "havenburg-fort-kijkduin",
  "meshyTaskId": "task_abc123",
  "prompt": "A massive coastal fortress with crumbling stone walls...",
  "generatedAt": "2026-04-09T15:45:00Z",
  "status": "accepted",
  "sourceType": "text-to-3d",
  "sizeCategory": "large"
}
```

### 9.4 Update building references

After an asset is accepted, update the corresponding `buildings.json` entries
to point to the real mesh path instead of a placeholder.

### 9.5 Catalog update

Append accepted assets to `content/Oravey2.Apocalyptic.NL.NH/catalog.json`
under the appropriate category (`building`, `prop`).

### 9.6 Tests

- `AssetQueueBuilder` — scan test design files, verify queue contents and
  deduplication
- ViewModel: generate/accept flow, batch processing with cancellation
- Mock `MeshyClient` for all tests (no real API calls)

## Dependencies

- Step 07 (town designs provide the visual descriptions)
- Step 08 (building placement references the asset IDs)
- Existing: `MeshyClient`

## Estimated scope

- New files: `AssetQueueBuilder.cs`, `AssetRequest.cs`, view + VM
- Modified: `MeshyClient` (possibly add `.glb` download helper)
