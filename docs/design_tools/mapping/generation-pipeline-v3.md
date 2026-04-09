# Generation Pipeline v3 — Architecture

## Overview

v3 replaces the monolithic "run everything at once → `world.db`" approach with
a **step-by-step, file-based pipeline**. Each step produces JSON artifacts and
3D assets into a content-pack project. The game loads these files at runtime —
no intermediate `world.db` needed during authoring.

The pipeline is **interactive first, batch later**: every step can be executed
for a single town or asset, reviewed in the UI, and only later run in bulk.

```
┌────────────────────────────────────────────────────────────────────┐
│  MapGen App — Generation Pipeline v3                               │
│                                                                    │
│  ① Region Select ──► ② SRTM Download ──► ③ OSM Download           │
│                                                                    │
│  ④ Parse & Extract (lazy UI) ──► ⑤ LLM Town Selection (2 modes)   │
│                                                                    │
│  ⑥ LLM Feature Design (per town) ──► ⑦ Town Map Condensation      │
│                                                                    │
│  ⑧ Meshy 3D Assets (per asset) ──► ⑨ Content Pack Assembly        │
│                                                                    │
│  Output: content/Oravey2.Apocalyptic.NL.NH/ (JSON + .glb files)   │
└────────────────────────────────────────────────────────────────────┘
```

---

## Design principles

| Principle | Rationale |
|-----------|-----------|
| **Files over database** | JSON artifacts are diffable, inspectable, and versionable. The game converts to SQLite at load time. |
| **One town at a time** | Each town can be designed, previewed, and re-generated independently before committing to a bulk run. |
| **Lazy heavy UI** | Parsed OSM/SRTM data and map previews are shown only on demand via explicit "Show" buttons — never auto-rendered. |
| **Two LLM modes** | Town selection supports both "ask the LLM to invent" and "let the LLM pick from parsed OSM data". |
| **Content-pack output** | Generated data lands in a `.csproj` content pack following the existing `Oravey2.Apocalyptic` conventions. |

---

## Step ①  Region Selection

**No changes from v2.** `GeofabrikService` → `GeofabrikIndex` →
`RegionPickerDialog` returns a `GeofabrikRegion` / `RegionPreset`.

---

## Step ②  SRTM Download

**No changes from v2.** `DataDownloadService.DownloadSrtmTilesAsync()` —
button-triggered, progress bar in UI.

---

## Step ③  OSM Download

**No changes from v2.** `DataDownloadService.DownloadOsmExtractAsync()` —
button-triggered, progress bar in UI.

---

## Step ④  Parse & Extract (lazy UI)

**What happens:** SRTM and OSM data are parsed into in-memory
`RegionTemplate`.

**Change from v2:** The parsed data visualization (town list, road network,
water bodies on a map canvas) is **not rendered automatically**. Instead:

- Parsing runs and produces the `RegionTemplate` in memory.
- A summary line shows: `"Parsed: 847 towns, 12,340 road segments, 234 water
  bodies"`.
- A **"Show Details"** button opens the full data view on demand.
- The map preview (`RegionTemplateMapDrawable`) is behind a separate
  **"Show Map"** button.

This eliminates the slow initial render that blocks the pipeline.

### Pre-filtering

`FeatureCuller` still runs as a pre-filter (drop hamlets < 50 pop, residential
roads, tiny ponds) to reduce LLM token usage. The culler is automatic — no
manual cull dialog needed.

---

## Step ⑤  LLM Town Selection — Two Modes

### Mode A: "Invent from knowledge" (open-ended)

The LLM receives only the region name and bounding box, **not** the parsed
town list. It invents interesting locations based on its own knowledge of the
area.

```
Prompt:
  You are designing a post-apocalyptic RPG set in {regionName}.
  Bounding box: {south},{west} to {north},{east}.

  Using your knowledge of this real-world area, suggest 8–15 settlements
  that would make interesting post-apocalyptic locations. Consider:
  - Major cities and historic towns
  - Strategic locations (ports, bridges, crossroads)
  - Places with distinctive character (university towns, industrial zones, fishing villages)

  For each, provide: realName, gameName, latitude, longitude, role, faction,
  threatLevel (1–10), description, estimatedPopulation.

  Respond with ONLY a JSON array.
```

**Use case:** When you want creative freedom, or for regions where OSM data is
sparse.

### Mode B: "Pick from list" (curated selection)

Same as v2 `TownCurator` — the LLM receives the full parsed town list and
selects from it.

```
Prompt (existing):
  Here are all real towns in this region:
  {townList with name, pop, category, lat, lon}

  Select 8–15 towns for the game...
```

**Use case:** When you want to ensure accuracy against real OSM data.

### Both modes output

`List<CuratedTown>` — same record type. The UI shows the result as a
selectable list where you can:
- Accept / reject individual towns
- Re-roll a single town or the whole list
- Manually edit fields

---

## Step ⑥  LLM Feature Design (per town)

**What happens:** For each curated town, the LLM designs its unique features:
landmark building, key locations, layout style, environmental hazards.

**This is the same as v2 Step ⑥** but with a key UX change: **each town is
designed individually**, not as a batch. The user picks a town from the list,
clicks "Design", reviews the result, and can re-generate.

### Output: `TownDesign` (new record)

```csharp
public sealed record TownDesign(
    string TownName,
    LandmarkBuilding Landmark,
    List<KeyLocation> KeyLocations,
    string LayoutStyle,                // "grid" | "radial" | "organic" | "linear" | "clustered" | "compound"
    List<EnvironmentalHazard> Hazards
);

public sealed record LandmarkBuilding(
    string Name,
    string VisualDescription,          // for Meshy prompt
    string SizeCategory                // "medium", "large"
);

public sealed record KeyLocation(
    string Name,
    string Purpose,                    // "shop", "quest-giver", "dungeon-entrance", etc.
    string VisualDescription,
    string SizeCategory
);

public sealed record EnvironmentalHazard(
    string Type,                       // "radiation", "flooding", "collapse"
    string Description,
    string LocationHint                // "north edge", "centre", etc.
);
```

### Persistence

Each town design is saved immediately as:
```
content/Oravey2.Apocalyptic.NL.NH/towns/{gameName}/design.json
```

---

## Step ⑦  Town Map Condensation

**What happens:** The town's real-world OSM footprint is condensed into a
compact game-scale tile map. Roads become 1-tile-wide paths, building
footprints snap to a grid, open spaces are proportionally shrunk.

### Output per town

```
content/Oravey2.Apocalyptic.NL.NH/towns/{gameName}/layout.json     — tile grid
content/Oravey2.Apocalyptic.NL.NH/towns/{gameName}/buildings.json   — placed buildings
content/Oravey2.Apocalyptic.NL.NH/towns/{gameName}/props.json       — placed props
```

Map preview is available on demand (**"Show Map"** button).

---

## Step ⑧  Meshy 3D Assets (per asset)

**What happens:** Unique 3D assets are generated via the Meshy API based on
the `VisualDescription` fields from step ⑥.

**Key change from v2:** Assets are created **one by one**, not in bulk. The UI
shows a queue/list of needed assets. For each:

1. Show the prompt text (from `LandmarkBuilding.VisualDescription` or
   `KeyLocation.VisualDescription`)
2. User clicks **"Generate"** → calls `MeshyClient.CreateTextTo3DAsync()`
3. Preview the result (thumbnail from Meshy API)
4. Accept → asset saved to content pack; Reject → re-generate with tweaked
   prompt

### Batch mode (later)

A **"Generate All"** button processes the full queue sequentially, with
auto-accept or a review step after each.

### Output

```
content/Oravey2.Apocalyptic.NL.NH/assets/meshes/{assetId}.glb
content/Oravey2.Apocalyptic.NL.NH/assets/meshes/{assetId}.meta.json
```

The `.meta.json` records the Meshy task ID, prompt used, generation date, and
acceptance status.

---

## Step ⑨  Content Pack Assembly

**What happens:** All generated JSON and assets are already in the content-pack
folder. This step:

1. Generates/updates `manifest.json` and `catalog.json`
2. Generates `scenarios/{regionName}.json` linking towns together
3. Validates referential integrity (every building references an existing mesh,
   every town has a design, etc.)
4. Optionally builds the `.csproj` NuGet package

The game loads the content pack at startup and converts it to a runtime
`world.db` via the existing content-pack loader.

---

## Data flow summary

```
OSM PBF ──► OsmParser ──► RegionTemplate (in memory)
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
        Mode A: LLM invents            Mode B: LLM picks
              │                               │
              └───────────┬───────────────────┘
                          ▼
                  List<CuratedTown>
                          │
                    ┌─────┴─────┐  (per town)
                    ▼           ▼
              TownDesign    Town Map Layout
                    │           │
                    ▼           ▼
              Meshy Assets   layout.json + buildings.json
                    │           │
                    └─────┬─────┘
                          ▼
            content/Oravey2.Apocalyptic.NL.NH/
                          │
                          ▼
                   Game runtime loader → world.db
```
