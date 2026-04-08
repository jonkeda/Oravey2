# Step 07 — Feature Lists & Auto-Cull Dialog

**Work streams:** WS-UI (Feature panels and culling dialog)
**Depends on:** Step 02 (FeatureCuller), Step 04 (ViewModel), Step 05 (XAML page), Step 06 (Map canvas)
**User-testable result:** Feature list panels show parsed towns/roads/water with checkboxes. Auto-cull dialog applies rules and updates both lists and map. Town→road dependency works.

---

## Goals

1. Inner tabs for Towns, Roads, Water in the map preview section.
2. Scrollable lists with include/exclude checkboxes, sort, search.
3. Auto-cull dialog with unified `CullSettings` editing.
4. Town→road dependency: toggling a town re-evaluates nearby roads.
5. Load/Save `.cullsettings` files from disk.

---

## Problem

After parsing, the user needs to select which features to include in the world template. There may be 85+ towns and 1200+ roads. Manual checkbox toggling is impractical, so an auto-cull dialog with configurable rules is needed. Additionally, roads depend on towns (roads near excluded towns should also be excluded).

---

## Tasks

### 7.1 — Feature List Inner Tabs

File: `src/Oravey2.MapGen.App/Views/WorldTemplateView.xaml` (modify)

- [ ] Add inner tab-like layout in the map preview section (right panel):
  - Use `SegmentedControl` or `Button` row for "Towns | Roads | Water" switching
  - Content area below shows the active feature list
- [ ] Each list is a `CollectionView` bound to the corresponding `ObservableCollection`

### 7.2 — Town List Panel

- [ ] Columns: ☑ (checkbox), Name, Category, Population, Lat, Lon
- [ ] `CheckBox` bound to `TownItem.IsIncluded`
- [ ] Sort by clicking column headers (Name, Category, Population)
- [ ] Search bar: `Entry` with text filter → `CollectionView.ItemsSource` uses filtered view
- [ ] Selected item highlights on the map
- [ ] Footer: "25 of 85 included" count label

### 7.3 — Road List Panel

- [ ] Columns: ☑, Classification, Near Town, Length (km), Points
- [ ] "Near Town" shows the name of the closest included town (or "—" if none nearby)
- [ ] Sort by classification, length, or near town
- [ ] Search bar
- [ ] Footer: "347 of 1200 included"

### 7.4 — Water List Panel

- [ ] Columns: ☑, Name, Type (lake/river/sea/canal), Area (km²)
- [ ] Sort by name, type, area
- [ ] Search bar
- [ ] Footer: "89 of 300 included"

### 7.5 — Select All / Select None Buttons

- [ ] "Select All" button → sets `IsIncluded = true` for all items in the **active** list
- [ ] "Select None" button → sets `IsIncluded = false` for all items in the **active** list
- [ ] Map invalidates after bulk toggle

### 7.6 — Auto-Cull Dialog

File: `src/Oravey2.MapGen.App/Views/AutoCullDialog.xaml`

- [ ] Modal popup (MAUI `ContentPage` displayed via `Navigation.PushModalAsync` or Community Toolkit `Popup`)
- [ ] Layout organized by section headers:

**Town Culling section:**
- [ ] `Picker` for Minimum category (Hamlet → Metropolis)
- [ ] `Entry` for Minimum population
- [ ] `Entry` for Minimum spacing (km)
- [ ] `Entry` for Maximum towns
- [ ] `CheckBox` for Always keep Cities
- [ ] `CheckBox` for Always keep Metropolis
- [ ] `RadioButton` group for Priority (Category / Population / Spacing)

**Road Culling section:**
- [ ] `Picker` for Minimum road class
- [ ] `CheckBox` for Keep roads near towns + `Entry` for proximity km
- [ ] `CheckBox` for Always keep motorways
- [ ] `CheckBox` for Simplify geometry + `Entry` for tolerance metres
- [ ] `CheckBox` for Remove dead-ends + `Entry` for min length km

**Water Culling section:**
- [ ] `Picker` or `Entry` for Minimum area (km²)
- [ ] `Entry` for Minimum river length (km)
- [ ] `CheckBox` for Always keep Sea
- [ ] `CheckBox` for Always keep Lakes

**Action buttons:**
- [ ] "Load…" → file picker for `.cullsettings` file → deserialize into dialog fields
- [ ] "Save…" → file save picker → serialize dialog fields to `.cullsettings`
- [ ] "Preview" → apply rules to current data, show result count without committing
- [ ] "Apply" → apply rules and close dialog, update all `IsIncluded` flags
- [ ] "Cancel" → close without changes

### 7.7 — Auto-Cull Dialog ViewModel

File: `src/Oravey2.MapGen.App/ViewModels/AutoCullDialogViewModel.cs`

- [ ] Properties mirror `CullSettings` fields for two-way binding
- [ ] Constructor takes current `CullSettings` → copies values to properties
- [ ] `PreviewCommand`:
  1. Build `CullSettings` from current property values
  2. Run `FeatureCuller.CullTowns()`, `.CullRoads()`, `.CullWater()` on the original data
  3. Show preview counts: "Would keep: 25 towns, 347 roads, 89 water"
- [ ] `ApplyCommand`: build `CullSettings`, return to caller
- [ ] `LoadCommand`: file picker → `CullSettings.Load()` → populate properties
- [ ] `SaveCommand`: file picker → `CullSettings.Save()` from current properties

### 7.8 — Town → Road Dependency

- [ ] When `TownItem.IsIncluded` changes:
  1. Recalculate `NearTown` for all roads: find the closest **included** town for each road
  2. If `CullSettings.RoadKeepNearTowns` is true:
     - Roads within `RoadTownProximityKm` of an included town: set `IsIncluded = true`
     - Roads not near any included town (and below `RoadMinClass`): set `IsIncluded = false`
  3. Update "Near Town" display column
  4. Debounce: batch changes when toggling multiple towns (e.g., after auto-cull)
- [ ] Dependency propagation is opt-in via a toggle: "☑ Auto-update roads when towns change"

### 7.9 — Map Synchronization

- [ ] When any `IsIncluded` flag changes → invalidate map canvas
- [ ] Use `ObservableCollection.CollectionChanged` or property change events
- [ ] Batch invalidation when auto-cull is applied (single invalidation after all flags set)

---

## Verify

```bash
dotnet build src/Oravey2.MapGen.App
```

**User test:** Launch app → parse Noord-Holland data.

1. **Feature lists:** Towns tab shows ~85 towns with checkboxes. Uncheck "Wormer" → map dims that town dot. Check it again → dot returns to normal.
2. **Auto-cull:** Click "Auto-cull…" → dialog shows current settings. Click "Preview" → shows "Would keep: 25 towns, 347 roads, 89 water". Click "Apply" → checkboxes update, map dims excluded features.
3. **Town→road dependency:** Uncheck "Alkmaar" → roads near Alkmaar dim on the map and uncheck in the road list. Re-check "Alkmaar" → roads return.
4. **Save/Load settings:** Save settings to `test.cullsettings`. Change values. Load the saved file → values restore.
