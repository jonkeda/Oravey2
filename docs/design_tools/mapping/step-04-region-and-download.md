# Step 04 — Region Selection & Data Download Steps

## Goal

Implement the first two wizard steps: region picker and SRTM/OSM downloads.
These wire up existing services to the new wizard UI.

## Deliverables

### 4.1 `RegionStepView` / `RegionStepViewModel`

- **[Select Region]** button → opens existing `RegionPickerDialog`
- Displays selected region card: name, bounding box, OSM URL
- **Content-pack target selector**: dropdown of existing content packs under
  `content/`, or **[Create New]** button that creates the folder structure from
  step 01 on the fly
- Stores selection in `PipelineState.Region`
- **[Next →]** enabled when region + target pack are set

### 4.2 `DownloadStepView` / `DownloadStepViewModel`

- Two side-by-side cards:
  - **SRTM:** file count, total size, status, **[Download]** + progress bar
  - **OSM:** file name, size, status, **[Download]** + progress bar
- Uses existing `DataDownloadService.DownloadSrtmTilesAsync()` and
  `DownloadOsmExtractAsync()`
- Status checks existing files in `data/regions/{name}/` on load
- Updates `PipelineState.Download` on completion
- **[Next →]** enabled when both are downloaded

### 4.3 Wire up DI

Register `DataDownloadService`, `GeofabrikService`, `PipelineStateService` in
the app's service provider.

### 4.4 Tests

- ViewModel tests: step completion logic, Next button enablement
- No new service tests needed (existing services already tested)

## Dependencies

- Step 02 (pipeline state)
- Step 03 (wizard shell)
- Existing: `GeofabrikService`, `RegionPickerDialog`, `DataDownloadService`

## Estimated scope

- Modified files: 2 views + 2 VMs (flesh out stubs from step 03)
- Modified files: 1–2 (DI registration)
