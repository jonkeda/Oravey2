# Step 05 — Parse & Extract Step (Lazy UI)

## Goal

Implement the parse step with on-demand data visualization. Parsing runs
quickly; the heavy rendering is only triggered by explicit user action.

## Deliverables

### 5.1 `ParseStepView` / `ParseStepViewModel`

- **[Parse]** button → runs `OsmParser.ParsePbf()` + `SrtmParser.ParseHgtFile()`
  + `FeatureCuller` (pre-filter with hardcoded minimal settings: drop hamlets
  < 50 pop, residential roads, water < 0.01 km²)
- **Summary line** after parse:
  ```
  Parsed: 847 towns · 12,340 roads · 234 water bodies · 4 SRTM tiles
  Pre-filtered to: 142 towns · 3,201 roads · 87 water bodies
  ```
- **[Show Town List]** → expands a `CollectionView` with columns:
  Name, Population, Category, Lat, Lon. Sortable by column.
- **[Show Summary]** → expands road-count-by-class and water-count-by-type tables
- **[Show Map Preview]** → renders `RegionTemplateMapDrawable` in an
  expandable panel. Uses `GraphicsView` with the existing drawable. Only
  instantiated when button is clicked.
- **[Next →]** enabled when parse is complete

### 5.2 Hold `RegionTemplate` in memory

The parsed `RegionTemplate` is stored on the ViewModel (or in a scoped
service) for use by step 06. It is **not** serialized to disk — the raw
download files in `data/regions/` are the persistent cache.

### 5.3 Performance guard

- Map drawable is created lazily (not in constructor)
- Town list `CollectionView` uses virtualization (`ItemsLayout` with recycling)
- If the user navigates away from the step, the map drawable is disposed

### 5.4 Tests

- ViewModel tests: parse triggers summary update, button states
- Integration test: parse a small test `.pbf` + `.hgt`, verify counts

## Dependencies

- Step 04 (download step provides the files to parse)
- Existing: `OsmParser`, `SrtmParser`, `FeatureCuller`,
  `RegionTemplateMapDrawable`

## Estimated scope

- Modified files: 1 view + 1 VM (flesh out stubs)
- Possibly a small `ParseResultSummary` record for binding
