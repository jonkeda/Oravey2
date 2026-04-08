# Step 05 — WorldTemplate Tab XAML & Registration

**Work streams:** WS-UI (MAUI XAML layout)
**Depends on:** Step 04 (WorldTemplateViewModel)
**User-testable result:** Launch MapGen.App → WorldTemplate tab is the first tab, shows source section with preset dropdown, SRTM/OSM paths, Browse and Download buttons, and Parse Data button.

---

## Goals

1. Create `WorldTemplateView.xaml` with the source/download section layout.
2. Register the new view and ViewModel in `MauiProgram.cs`.
3. Add the tab to `MainPage.xaml` as the first tab.
4. Wire data bindings to `WorldTemplateViewModel`.
5. Implement Browse button file/folder pickers.

---

## Problem

The WorldTemplate tab needs a MAUI XAML page with the layout described in the design. It must be registered in the DI container and added to the `TabbedPage`.

---

## Tasks

### 5.1 — WorldTemplateView XAML

File: `src/Oravey2.MapGen.App/Views/WorldTemplateView.xaml`

- [ ] Create XAML page as `ContentPage`
- [ ] Match existing app style (dark theme, consistent padding/margins)

**Region & Data Sources section:**
- [ ] `Picker` bound to `Presets` with `SelectedItem="{Binding SelectedPreset}"` and `ItemDisplayBinding="{Binding DisplayName}"`
- [ ] "Save Preset…" button → `SavePresetCommand`
- [ ] SRTM row: `Entry` bound to `SrtmDirectory`, "Browse" button (folder picker), "⬇" download button → `DownloadSrtmCommand`
- [ ] SRTM status label: shows tile count from directory scan
- [ ] OSM PBF row: `Entry` bound to `OsmFilePath`, "Browse" button (file picker, `.osm.pbf` filter), "⬇" download button → `DownloadOsmCommand`
- [ ] OSM status label: shows file size and last modified date
- [ ] Region Name: `Entry` bound to `RegionName`
- [ ] "Parse Data" button → `ParseCommand`, disabled when `IsBusy` or paths empty

**Download progress (visible when downloading):**
- [ ] `ProgressBar` bound to `DownloadPercent / 100`
- [ ] Label showing `CurrentDownload.FileName` and bytes
- [ ] "Cancel" button

**Build section (bottom):**
- [ ] Output path: `Entry` bound to `OutputPath`, "Browse…" button
- [ ] Summary label bound to `Summary`
- [ ] Culled summary label bound to `CulledSummary`
- [ ] "Build WorldTemplate" button → `BuildCommand`
- [ ] Log area: `Editor` bound to `LogText`, read-only, scrollable

### 5.2 — WorldTemplateView Code-Behind

File: `src/Oravey2.MapGen.App/Views/WorldTemplateView.xaml.cs`

- [ ] Constructor: set `BindingContext` from DI (`WorldTemplateViewModel`)
- [ ] Browse SRTM: `FolderPicker` → set `ViewModel.SrtmDirectory`
- [ ] Browse OSM: `FilePicker` with filter `["*.osm.pbf", "*.pbf"]` → set `ViewModel.OsmFilePath`
- [ ] Browse Output: `FilePicker` in save mode → set `ViewModel.OutputPath`

### 5.3 — Register in DI

File: `src/Oravey2.MapGen.App/MauiProgram.cs`

- [ ] Register `IDataDownloadService` → `DataDownloadService` as singleton
- [ ] Register `WorldTemplateViewModel` as transient
- [ ] Register `WorldTemplateView` as transient
- [ ] Register `HttpClient` via `AddHttpClient` (if not already registered)

### 5.4 — Add Tab to MainPage

File: `src/Oravey2.MapGen.App/Pages/MainPage.xaml`

- [ ] Add `WorldTemplateView` as the **first** tab:
  ```xml
  <views:WorldTemplateView Title="WorldTemplate" />
  <views:GeneratorView Title="Generate" />
  ...
  ```
- [ ] Verify namespace `views` is declared

### 5.5 — Repurpose Generate Tab

- [ ] Rename `GeneratorView` title to "Generate" (keep as-is if already named)
- [ ] Add a label: "Select a WorldTemplate file to generate a game world."
- [ ] This tab will be connected to `WorldGenerator` later — for now just placeholder text

### 5.6 — Smoke Test

- [ ] Launch app → WorldTemplate tab is visible and selected by default
- [ ] Preset dropdown shows "Noord-Holland"
- [ ] Selecting preset fills SRTM dir, OSM file path, region name, output path
- [ ] Browse buttons open native file/folder pickers
- [ ] Parse Data button is disabled until SRTM dir and OSM file are set
- [ ] Download buttons are visible and clickable (actual download tested later)

---

## Verify

```bash
dotnet build src/Oravey2.MapGen.App
```

**User test:** Launch app. The WorldTemplate tab appears first. Select "Noord-Holland" from the preset dropdown. Fields auto-fill. Click Browse buttons — native dialogs appear. Parse Data is disabled (no data files exist yet). Build section visible at bottom with log area.
