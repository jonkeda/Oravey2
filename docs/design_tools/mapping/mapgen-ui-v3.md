# MapGen App UI — v3 Step-by-Step Design

## Overview

The current 6-tab layout is replaced with a **pipeline wizard** — a linear
series of pages where each step builds on the output of the previous one. The
user progresses forward through the pipeline, with the ability to go back and
re-run earlier steps.

Each step has its own dedicated UI with clear inputs, outputs, and actions.
Heavy visualizations (parsed data tables, map previews) are always
**on-demand** behind explicit buttons.

---

## Navigation layout

```
┌──────────────────────────────────────────────────────────────┐
│  MapGen — Pipeline v3                                [Settings ⚙]│
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  Sidebar (step list)          │  Main content area           │
│  ─────────────────            │                              │
│  ✅ ① Region                  │  (current step UI)           │
│  ✅ ② Download Data           │                              │
│  ✅ ③ Parse & Extract         │                              │
│  🔵 ④ Town Selection          │                              │
│     ⑤ Town Design             │                              │
│     ⑥ Town Maps               │                              │
│     ⑦ 3D Assets               │                              │
│     ⑧ Assemble Pack           │                              │
│                               │                              │
└──────────────────────────────────────────────────────────────┘

✅ = completed   🔵 = current   (blank) = locked
```

Settings (API keys, Earthdata credentials, Meshy key, export paths) remain
accessible via a gear icon — same `SettingsView` as today.

---

## Step ① Region

**UI elements:**
- Region picker button → opens existing `RegionPickerDialog`
- Selected region card: name, bounding box, OSM download URL
- Content-pack target selector: pick or create a content-pack project
  (`Oravey2.Apocalyptic.NL.NH`)
- **[Next →]** enabled when region is selected

**Output:** `RegionPreset` + target content-pack path stored in pipeline state.

---

## Step ② Download Data

**UI elements:**
- Two cards side by side:
  - **SRTM tiles:** status (downloaded / not), file sizes, **[Download]** button,
    progress bar
  - **OSM extract:** status, file size, **[Download]** button, progress bar
- Both use existing `DataDownloadService`
- **[Next →]** enabled when both downloads are complete

**Output:** Files in `data/regions/{name}/srtm/` and `data/regions/{name}/osm/`.

---

## Step ③ Parse & Extract

**UI elements:**
- **[Parse]** button — runs `OsmParser` + `SrtmParser` + `FeatureCuller`
  (pre-filter)
- Summary line after parse completes:
  ```
  Parsed: 847 towns · 12,340 road segments · 234 water bodies · 4 SRTM tiles
  Pre-filtered to: 142 towns · 3,201 road segments · 87 water bodies
  ```
- **[Show Town List]** button → expands a scrollable table:
  `Name | Population | Category | Lat | Lon`
- **[Show Road/Water]** button → expands counts-by-class summary
- **[Show Map Preview]** button → renders `RegionTemplateMapDrawable` in an
  expandable panel (lazy — only when clicked)
- **[Next →]** enabled when parse is complete

**Output:** `RegionTemplate` held in memory. Nothing written to disk at this
step.

**Performance note:** The map preview and town list are the slow parts. By
keeping them behind buttons, the parse-and-proceed flow is fast.

---

## Step ④ Town Selection

**UI elements:**
- **Mode selector** (toggle or radio):
  - **Mode A: "Discover"** — LLM invents locations from its own knowledge
  - **Mode B: "Select"** — LLM picks from the parsed OSM town list
- **[Run LLM]** button — sends the appropriate prompt
- **Results list** — card per town:
  ```
  ┌──────────────────────────────────────────────────┐
  │ ☑  Havenburg (formerly Den Helder)               │
  │     Role: military_outpost  Faction: Noordfort    │
  │     Threat: 7/10  Pop: ~56,000                    │
  │     "Former naval base, now a fortified citadel…" │
  │     [✏ Edit]  [🔄 Re-roll]  [✕ Remove]            │
  └──────────────────────────────────────────────────┘
  ```
- **[Add Town]** button — manually add a town
- **[Re-roll All]** button — re-run the LLM for the whole list
- Validation summary at bottom:
  ```
  ✅ 12 towns selected (8–15 required)
  ✅ Threat range covered: 1, 4, 7, 9
  ⚠️  Two towns < 10 km apart: Havenburg, Marsdiep
  ```
- **[Save & Next →]** — writes `data/curated-towns.json` to the content pack

**Output:** `content/Oravey2.Apocalyptic.NL.NH/data/curated-towns.json`

---

## Step ⑤ Town Design

**UI elements:**
- **Town list** (left panel) — all curated towns from step ④
  - Each shows status: `Not designed` / `Designed ✅` / `Error ❌`
- **Design panel** (right) — for the selected town:
  - Town info summary (name, role, threat)
  - **[Design Town]** button → runs LLM feature design prompt
  - Result display:
    - Landmark building card (name, description, size)
    - Key locations list (name, purpose, description)
    - Layout style badge
    - Environmental hazards list
  - **[Accept]** / **[Re-generate]** / **[Edit JSON]** buttons
- **Batch bar** (bottom):
  - **[Design All Remaining]** — processes undesigned towns sequentially,
    auto-accepting results

**Output per town:** `content/Oravey2.Apocalyptic.NL.NH/towns/{gameName}/design.json`

---

## Step ⑥ Town Maps

**UI elements:**
- **Town list** (left panel) — towns that have a design
  - Status: `No map` / `Map generated ✅`
- **Map panel** (right) — for the selected town:
  - **[Generate Map]** button → runs town map condensation
  - **[Show Preview]** button → renders the tile map (on-demand, lazy)
  - Stats: `24×18 tiles · 6 buildings · 14 props · 2 zones`
  - **[Accept]** / **[Re-generate]** / **[Edit JSON]** buttons
- **Batch bar:**
  - **[Generate All Maps]** — processes all designed towns

**Output per town:**
```
towns/{gameName}/layout.json
towns/{gameName}/buildings.json
towns/{gameName}/props.json
towns/{gameName}/zones.json
```

---

## Step ⑦ 3D Assets

**UI elements:**
- **Asset queue** (left panel) — derived from all town designs:
  - Lists every `VisualDescription` that needs a mesh
  - Groups by town, shows: asset name, prompt snippet, status
  - Status: `Pending` / `Generating…` / `Ready ✅` / `Failed ❌`
- **Asset detail** (right) — for the selected asset:
  - Full prompt text (editable)
  - **[Generate]** button → `MeshyClient.CreateTextTo3DAsync()`
  - Meshy progress bar (polling `MeshyClient.GetTaskAsync()`)
  - Thumbnail preview (from Meshy API response)
  - **[Accept]** → saves `.glb` + `.meta.json`
  - **[Reject & Re-generate]** → re-runs with same or edited prompt
- **Batch bar:**
  - **[Generate All Pending]** — sequential queue, auto-accept or pause-on-each toggle

**Output per asset:**
```
assets/meshes/{assetId}.glb
assets/meshes/{assetId}.meta.json
```

---

## Step ⑧ Assemble Pack

**UI elements:**
- **Checklist** of all content-pack files:
  ```
  ✅ manifest.json
  ✅ catalog.json (42 entries)
  ✅ scenarios/noord-holland.json
  ✅ 12/12 towns have design.json
  ✅ 12/12 towns have layout.json
  ⚠️  3 assets still pending
  ❌ Missing mesh: buildings/lighthouse.glb (referenced by town "Vuurhaven")
  ```
- **[Generate manifest]** / **[Generate catalog]** / **[Generate scenario]**
  buttons for the metadata files
- **[Validate]** button — checks referential integrity
- **[Build Package]** button — runs `dotnet pack` on the `.csproj`
- **[Open in Explorer]** — opens the content-pack folder

**Output:** Complete, validated content pack ready for game loading.

---

## Pipeline state persistence

Pipeline progress is saved to:
```
data/regions/{name}/pipeline-state.json
```

```json
{
  "regionName": "noord-holland",
  "contentPack": "content/Oravey2.Apocalyptic.NL.NH",
  "currentStep": 5,
  "steps": {
    "region": { "completed": true, "preset": "..." },
    "download": { "srtm": true, "osm": true },
    "parse": { "completed": true, "townCount": 142, "roadCount": 3201 },
    "townSelection": { "completed": true, "mode": "B", "townCount": 12 },
    "townDesign": { "designed": ["havenburg", "marsdiep", "..."], "remaining": 3 },
    "townMaps": { "generated": [], "remaining": 12 },
    "assets": { "ready": 0, "pending": 24, "failed": 0 },
    "assembly": { "validated": false }
  }
}
```

This allows the user to close and re-open the app, resuming at the exact step
they left off.

---

## Batch mode summary

| Step | Single-item action | Batch action |
|------|--------------------|--------------|
| ⑤ Town Design | [Design Town] | [Design All Remaining] |
| ⑥ Town Maps | [Generate Map] | [Generate All Maps] |
| ⑦ 3D Assets | [Generate] | [Generate All Pending] |

Batch operations show a progress indicator:
```
Designing towns: 7/12 ████████░░░░ Marsdiep…
```

All batch operations are cancellable.
