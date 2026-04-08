# Design: World Creation Flow

## Status: Draft

## Overview

This design adds a **New Game** flow that takes the player from the start menu through world creation to gameplay. The player picks a starting town from the Noord-Holland world template, watches a progress screen while the world generates, and then spawns in that town.

The design reuses the existing `StartMenuScript`, `ScenarioSelectorScript`, `WorldGenerator`, `WorldTemplate`, `ContentPackService`, and `SaveService` systems. New UI scripts follow the established Stride.UI `SyncScript` overlay pattern.

---

## Current State (What Exists)

| Component | Status | Notes |
|-----------|--------|-------|
| `StartMenuScript` | Exists | Has "New Scenario", "Continue", "Settings", "Quit" |
| `ScenarioSelectorScript` | Exists | Picks from built-in + content pack scenarios |
| `WorldGenerator` | Exists | 4-phase LLM-curated generation pipeline |
| `WorldTemplate` + `WorldTemplateBuilder` | Exists | Binary `.worldtemplate` format with towns, roads, water, elevation |
| `TownEntry` | Exists | Name, lat/lon, population, game position, category, boundary polygon |
| `ContentPackService` | Exists | Discovers + loads content packs with manifest.json |
| `SaveService` | Exists | Single save slot at `%APPDATA%/Oravey2/save.json` |
| `WorldMapStore` | Exists | SQLite storage for generated world data |
| `noordholland.worldtemplate` | Exists | Pre-built template from OSM + SRTM data |

---

## Proposed Flow

```
StartMenuScript
│
├── "Continue"  → (existing) Load saved game
├── "Settings"  → (existing) SettingsMenuScript
├── "Quit"      → (existing) Exit
│
├── "New Game"  → NEW: WorldTemplatePickerScript
│   │              Shows available .worldtemplate files
│   │              (Initially only: noordholland.worldtemplate)
│   │
│   └── Select template → NEW: TownPickerScript
│       │                   Shows towns from the selected template
│       │                   Map preview with town dots
│       │                   Town details panel (name, category, population)
│       │
│       └── Select town + "Start" → NEW: WorldGenerationProgressScript
│           │                         Phase progress bars
│           │                         Log messages scrolling
│           │                         Estimated time remaining
│           │
│           └── Generation complete → Load "generated" scenario
│                                      Player spawns at selected town
│
└── "New Scenario"  → (existing) ScenarioSelectorScript
                        (kept for debug/test scenarios)
```

---

## UI Screens

### Screen 1: Start Menu (Modified)

**File:** `StartMenuScript.cs` (modify existing)

**Changes:**
- Add a **"New Game"** button between the title and "New Scenario"
- "New Scenario" is renamed to **"Debug Scenarios"** (or moved under a submenu) to keep developer-only scenarios accessible but not prominent
- "New Game" fires `OnNewGame` callback

**Layout:**

```
┌──────────────────────────────────────────────────────┐
│                                                      │
│                    ORAVEY 2                           │
│              Post-Apocalyptic                         │
│                                                      │
│              ┌──────────────┐                        │
│              │   New Game   │                        │
│              └──────────────┘                        │
│              ┌──────────────┐                        │
│              │   Continue   │  (greyed if no save)   │
│              └──────────────┘                        │
│              ┌──────────────┐                        │
│              │   Settings   │                        │
│              └──────────────┘                        │
│              ┌──────────────┐                        │
│              │     Quit     │                        │
│              └──────────────┘                        │
│                                                      │
│         ┌────────────────────────┐                   │
│         │  Content Pack: Post-…  │  (if multiple)    │
│         └────────────────────────┘                   │
│                                                      │
│    Debug Scenarios ▸         (small text link)       │
│                                                      │
└──────────────────────────────────────────────────────┘
```

**Callbacks:**

```csharp
public Action? OnNewGame { get; set; }      // NEW
public Action? OnNewScenario { get; set; }  // existing (debug scenarios)
public Action? OnContinue { get; set; }     // existing
```

---

### Screen 2: World Template Picker

**File:** `WorldTemplatePickerScript.cs` (new)

Discovers `.worldtemplate` files from the active content pack directory and `content/` folder. For now this will typically show a single entry (Noord-Holland). The screen exists so the flow scales when more templates are added.

**Layout:**

```
┌──────────────────────────────────────────────────────┐
│                                                      │
│   SELECT REGION                                      │
│   ─────────────                                      │
│                                                      │
│   ┌─────────────────────────────────────────────┐    │
│   │  ● Noord-Holland                            │    │
│   │    Province of the Netherlands              │    │
│   │    85 towns · 1,247 roads · 312 waterways   │    │
│   ├─────────────────────────────────────────────┤    │
│   │  (more templates appear here when added)    │    │
│   └─────────────────────────────────────────────┘    │
│                                                      │
│                                                      │
│                           ┌────────┐  ┌────────┐    │
│                           │  Back  │  │  Next  │    │
│                           └────────┘  └────────┘    │
│                                                      │
└──────────────────────────────────────────────────────┘
```

**Behaviour:**
- On `Start()`, scan for `.worldtemplate` files and deserialize their headers (name, region count, town counts) using `WorldTemplateBuilder.DeserializeHeader()` — a lightweight read that only parses the header without loading the full elevation grids
- Display as a selectable list (highlight active selection)
- If only one template exists, auto-select it but still show the screen so the player sees what they're getting
- **Next** → proceeds to `TownPickerScript` with the selected template
- **Back** → returns to `StartMenuScript`

**Data model:**

```csharp
public record WorldTemplateInfo(
    string FilePath,
    string Name,
    int RegionCount,
    int TotalTowns,
    int TotalRoads,
    int TotalWaterBodies);
```

**Callbacks:**

```csharp
public Action<string>? OnTemplateSelected { get; set; }  // filePath
public Action? OnBack { get; set; }
```

---

### Screen 3: Town Picker

**File:** `TownPickerScript.cs` (new)

Shows a map of the selected region with town dots. The player picks their starting town. This is the key decision point — where in Noord-Holland does the adventure begin?

**Layout:**

```
┌──────────────────────────────────────────────────────┐
│                                                      │
│   CHOOSE STARTING TOWN          Noord-Holland        │
│   ────────────────────                               │
│                                                      │
│   ┌──────────────────────┐  ┌──────────────────────┐│
│   │                      │  │                      ││
│   │    ·  Alkmaar        │  │  PURMEREND           ││
│   │         ·  Hoorn     │  │  ──────────          ││
│   │                      │  │                      ││
│   │  · Haarlem           │  │  Category: Town      ││
│   │                      │  │  Population: 81,000  ││
│   │      ● Purmerend     │  │                      ││
│   │                      │  │  A market town on    ││
│   │          · Zaandam   │  │  the shores of a     ││
│   │                      │  │  drained polder,     ││
│   │  · Amsterdam         │  │  now a fortified     ││
│   │                      │  │  outpost against     ││
│   │                      │  │  the rising waters.  ││
│   │                      │  │                      ││
│   └──────────────────────┘  └──────────────────────┘│
│                                                      │
│   Filter: [All ▾]  Sort: [Population ▾]             │
│                                                      │
│                           ┌────────┐  ┌────────┐    │
│                           │  Back  │  │ Start  │    │
│                           └────────┘  └────────┘    │
│                                                      │
└──────────────────────────────────────────────────────┘
```

**Left panel — Region map:**
- Rendered using a `Canvas` or simple `Grid` with positioned dots
- Each town is a clickable dot, sized by `TownCategory` (Hamlet=small, City=large)
- Selected town highlighted with a ring/glow
- Map extent derived from the min/max lat/lon of all `TownEntry` records
- Continent outline drawn from `WorldTemplate.ContinentOutlines` (if available) as a border polyline
- Water bodies shown as blue patches (approximate positions from template)

**Right panel — Town details:**
- Town name (large text)
- Category badge (Hamlet / Village / Town / City / Metropolis)
- Population (from OSM data)
- Description: A short atmospheric text. Initially use a template-based sentence built from `TownCategory` + name. When LLM is available during generation, the `TownCurator` produces richer descriptions.
- Scrollable if text exceeds panel height

**Filter/Sort (optional, can be deferred):**
- Filter dropdown: All / Hamlet / Village / Town / City / Metropolis
- Sort: Alphabetical / Population / Category

**Behaviour:**
- Loads the full `WorldTemplate` via `WorldTemplateBuilder.Deserialize(filePath)`
- Flattens all `RegionTemplate.Towns` into a single list
- Default selection: the largest-population town, or Purmerend if it exists
- **Start** button is disabled until a town is selected
- **Start** → fires `OnTownSelected` with template path + `TownEntry`
- **Back** → returns to `WorldTemplatePickerScript`

**Callbacks:**

```csharp
public Action<string, TownEntry>? OnTownSelected { get; set; }  // templatePath, town
public Action? OnBack { get; set; }
```

---

### Screen 4: World Generation Progress

**File:** `WorldGenerationProgressScript.cs` (new)

Shows progress while `WorldGenerator.GenerateAsync` runs on a background thread. This screen is non-interactive (no back button) — the player watches the world being built.

**Layout:**

```
┌──────────────────────────────────────────────────────┐
│                                                      │
│             CREATING YOUR WORLD                      │
│             ───────────────────                      │
│                                                      │
│   ┌──────────────────────────────────────────────┐   │
│   │                                              │   │
│   │  "In the ruins of Noord-Holland, a new       │   │
│   │   story begins in Purmerend…"                │   │
│   │                                              │   │
│   └──────────────────────────────────────────────┘   │
│                                                      │
│   Phase 1: Curating towns                            │
│   ████████████████████░░░░░░░░░░░░░░  60%            │
│                                                      │
│   Phase 2: Generating continent        ✓             │
│   ████████████████████████████████████ 100%           │
│                                                      │
│   Phase 3: Building regions                          │
│   ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   0%           │
│                                                      │
│   Phase 4: Generating terrain                        │
│   ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   0%           │
│                                                      │
│   ┌──────────────────────────────────────────────┐   │
│   │  > Curating towns…                           │   │
│   │  > Asking LLM for Purmerend backstory…       │   │
│   │  > Purmerend curated: "The Dike Watch"       │   │
│   │  > Asking LLM for Alkmaar backstory…         │   │
│   └──────────────────────────────────────────────┘   │
│                                                      │
└──────────────────────────────────────────────────────┘
```

**Elements:**
1. **Title** — "CREATING YOUR WORLD"
2. **Flavour text** — Atmospheric sentence using the selected town name and region
3. **Phase progress bars** — Four bars (one per WorldGenerator phase). Each bar shows the phase name, a fill bar, and percentage
4. **Log panel** — Scrolling text log of generation messages. Fed by `WorldGenerator._onProgress` callback. Auto-scrolls to bottom. Shows the last ~8 lines.
5. **No cancel button** — Generation runs to completion. If it fails, show an error message with a "Back to Menu" button.

**Behaviour:**
- On `Show(templatePath, townEntry)`:
  1. Display immediately with all bars at 0%
  2. Start generation on `Task.Run`:
     ```csharp
     Task.Run(async () => {
         var template = WorldTemplateBuilder.Deserialize(templatePath);
         var store = new WorldMapStore(worldDbPath);
         store.GetOrSetMeta("start_town", selectedTownName);
         var generator = new WorldGenerator(llmCall, msg => _progressQueue.Enqueue(msg));
         await generator.GenerateAsync(template, seed, store, cts.Token);
         _generationComplete = true;
     });
     ```
  3. `Update()` polls `_progressQueue` and updates phase bars + log text
  4. When `_generationComplete` is true, fires `OnGenerationComplete`
- Phase detection: Parse progress messages for keywords ("Curating" → phase 1, "continent" → phase 2, "regions" / "roads" → phase 3, "terrain" / "chunks" → phase 4)
- Phase progress estimation: For phase 1 (LLM), count towns curated vs total. For phase 4, count chunks generated vs total (121 chunks per region)

**Error handling:**
- If `Task` faults, catch the exception, display error text in the log panel, and show a "Back to Menu" button
- Common error: LLM not available → show "LLM connection failed. Check API key configuration."

**Callbacks:**

```csharp
public Action? OnGenerationComplete { get; set; }
public Action? OnError { get; set; }
```

**Data flow to WorldGenerator:**

```csharp
// Existing WorldGenerator constructor
new WorldGenerator(
    llmCall: (prompt, ct) => LlmService.CallAsync(prompt, ct),
    onProgress: message => _progressQueue.Enqueue(message)
);
```

---

### Screen 5: Generation Complete → Transition to Game

No new screen. When generation finishes:

1. `WorldGenerationProgressScript` fires `OnGenerationComplete`
2. `GameBootstrapper` hides the progress screen
3. Calls `LoadAndWireScenario("generated")` — this loads from the newly created `world.db`
4. Reads `start_town` meta from `WorldMapStore` to determine spawn position
5. Teleports player to the selected town's `GamePosition`
6. Transitions to `GameState.Exploring`

---

## Data Flow

```
noordholland.worldtemplate
       │
       ▼
WorldTemplateBuilder.Deserialize()
       │
       ├──→ WorldTemplatePickerScript  (reads header: name, counts)
       │
       ├──→ TownPickerScript           (reads full: towns, positions, outlines)
       │
       └──→ WorldGenerator.GenerateAsync()
                   │
                   ├── Phase 1: TownCurator (LLM) → CuratedRegion
                   ├── Phase 2: ContinentGenerator → continent grid
                   ├── Phase 3: RegionGenerator + Roads + Rivers → features
                   └── Phase 4: ChunkGenerators → tile data
                           │
                           ▼
                     WorldMapStore (world.db)
                           │
                           ▼
                    ScenarioLoader.LoadGeneratedWorld()
                           │
                           ▼
                    Player spawns at selected town
```

---

## Modifications to Existing Code

### 1. `StartMenuScript.cs`

- Add `"New Game"` button and `OnNewGame` callback
- Rename `"New Scenario"` to `"Debug Scenarios"` (or make it a small text link at the bottom)

### 2. `GameBootstrapper.cs`

- Create and wire the three new UI entities (template picker, town picker, progress) in the `menuScene`
- Wire the callback chain:

```csharp
// New Game flow
startMenuScript.OnNewGame = () => {
    startMenuScript.Hide();
    templatePickerScript.Show();
};

templatePickerScript.OnTemplateSelected = (path) => {
    templatePickerScript.Hide();
    townPickerScript.Show(path);
};

templatePickerScript.OnBack = () => {
    templatePickerScript.Hide();
    startMenuScript.Show();
};

townPickerScript.OnTownSelected = (path, town) => {
    townPickerScript.Hide();
    progressScript.Show(path, town, seed: Random.Shared.Next());
};

townPickerScript.OnBack = () => {
    townPickerScript.Hide();
    templatePickerScript.Show();
};

progressScript.OnGenerationComplete = () => {
    progressScript.Hide();
    LoadAndWireScenario("generated");
    // Teleport player to selected town's GamePosition
    gameStateManager.TransitionTo(GameState.Exploring);
};

progressScript.OnError = () => {
    progressScript.Hide();
    startMenuScript.Show();
};
```

### 3. `WorldTemplateBuilder.cs`

- Add `DeserializeHeader(string filePath)` — reads only the file header (name, region count) without loading elevation grids. Returns `WorldTemplateInfo`.

### 4. `ScenarioLoader.cs` — `LoadGeneratedWorld`

- After loading chunks, read `start_town` meta from `WorldMapStore`
- Look up the town's game position from stored POI data
- Set player spawn position to the town center instead of origin

### 5. `WorldGenerator.cs`

- Add structured progress reporting: `OnPhaseStarted(int phase, string name)`, `OnPhaseProgress(int phase, float percent)`, `OnPhaseCompleted(int phase)` alongside the existing string `onProgress`
- Or: keep the string-based progress but adopt a naming convention the UI can parse: `"[Phase 1/4] Curating towns (3/85)…"`

---

## Seed & Replayability

- World seed is a random `int` generated at the moment the player clicks "Start" on the town picker
- Stored in `WorldMapStore` meta as `world_seed`
- Future: add an optional seed input field on the town picker for deterministic replays

---

## Save Integration

- When the world finishes generating, the game auto-saves immediately (a "fresh start" save)
- The save data records the scenario as `"generated"` and the `world.db` path
- "Continue" on the start menu restores from this save, loading from `world.db` + applying saved state
- `SaveData` needs a new field: `ScenarioId` (currently hardcoded to load `"town"`)

---

## Automation / UI Test Support

Each new screen exposes query endpoints for `OraveyAutomationHandler`:

| Query | Returns | Purpose |
|-------|---------|---------|
| `GetWorldTemplatePickerState` | visible, templates[], selectedIndex | Template picker test automation |
| `GetTownPickerState` | visible, towns[], selectedTown, filter, sort | Town picker test automation |
| `GetGenerationProgressState` | visible, phase, phaseProgress, log[] | Progress screen test automation |
| `SelectWorldTemplate` | success | Pick a template by index |
| `SelectTown` | success | Pick a town by name |
| `ConfirmSelection` | success | Press "Next"/"Start" on current screen |

---

## Implementation Order

| Step | Task | Depends On |
|------|------|------------|
| 1 | Add `DeserializeHeader()` to `WorldTemplateBuilder` | — |
| 2 | Create `WorldTemplatePickerScript` | Step 1 |
| 3 | Create `TownPickerScript` | — |
| 4 | Create `WorldGenerationProgressScript` | — |
| 5 | Modify `StartMenuScript` — add "New Game" button | — |
| 6 | Modify `GameBootstrapper` — wire callback chain | Steps 2-5 |
| 7 | Modify `ScenarioLoader.LoadGeneratedWorld` — town spawn position | — |
| 8 | Modify `SaveData` — add `ScenarioId` field | — |
| 9 | Add structured progress to `WorldGenerator` | — |
| 10 | Add automation query handlers | Steps 2-4 |
| 11 | Unit tests (template picker discovery, progress parsing) | Steps 1-4 |
| 12 | UI tests (menu navigation, generation flow) | Steps 6, 10 |

---

## Future Considerations

- **Multiple world templates**: Elden lands, custom regions — the template picker scales naturally
- **Difficulty selection**: Add a difficulty picker between town picker and generation (Easy/Normal/Survival)
- **World customization**: Seed input, enemy density slider, LLM creativity slider
- **Save slots**: Multiple saves per generated world
- **Continue into generated world**: Currently "Continue" always loads `"town"` — needs to respect `SaveData.ScenarioId`
