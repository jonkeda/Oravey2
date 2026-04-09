# Step 06 — LLM Town Selection (Two Modes)

## Goal

Implement the town selection step with both Mode A (LLM invents) and Mode B
(LLM picks from list). Output is `curated-towns.json` in the content pack.

## Deliverables

### 6.1 Mode A prompt — "Discover"

New method in `TownCurator`:

```csharp
public async Task<List<CuratedTown>> DiscoverAsync(
    string regionName,
    double southLat, double westLon,
    double northLat, double eastLon,
    int seed,
    CancellationToken ct = default);
```

Prompt asks the LLM to **invent** 8–15 locations using its own knowledge of
the region. Does not receive the parsed town list. Returns the same
`CuratedTown` records.

### 6.2 Mode B — "Select from list" (existing)

Existing `TownCurator.CurateAsync()` already does this. Minor refactor: extract
the town list from the `CuratedRegion` return type so both modes return
`List<CuratedTown>`.

### 6.3 `TownSelectionStepView` / `TownSelectionStepViewModel`

- **Mode toggle**: radio buttons for "Discover" (A) vs "Select" (B)
- **[Run LLM]** button → calls the appropriate method
- **Results list**: card per town with:
  - Checkbox (include/exclude)
  - Name, role, faction, threat level, description
  - **[Edit]** → inline editing of all fields
  - **[Re-roll]** → re-runs LLM for just this town (sends a targeted prompt
    asking for a replacement)
  - **[Remove]** → unchecks and greys out
- **[Add Town]** → manual entry form
- **[Re-roll All]** → re-runs the full LLM prompt
- **Validation panel**:
  - Town count in range (8–15)
  - Threat range coverage (low/mid/high)
  - Minimum spacing check (~15 km)
- **[Save & Next →]** → writes `data/curated-towns.json`

### 6.4 `curated-towns.json` format

```json
{
  "mode": "B",
  "seed": 42,
  "generatedAt": "2026-04-09T14:30:00Z",
  "towns": [
    {
      "gameName": "Havenburg",
      "realName": "Den Helder",
      "latitude": 52.9533,
      "longitude": 4.7600,
      "role": "military_outpost",
      "faction": "Noordfort",
      "threatLevel": 7,
      "description": "Former naval base, now a fortified citadel...",
      "estimatedPopulation": 56000
    }
  ]
}
```

Written to: `content/Oravey2.Apocalyptic.NL.NH/data/curated-towns.json`

### 6.5 Tests

- `TownCurator.DiscoverAsync` — mock LLM, verify prompt structure and parsing
- `TownCurator.CurateAsync` — existing tests still pass
- ViewModel: mode switching, validation logic, save output

## Dependencies

- Step 05 (parsed `RegionTemplate` for Mode B)
- Step 02 (pipeline state — content pack path)
- Existing: `TownCurator`, LLM call infrastructure

## Estimated scope

- Modified files: `TownCurator.cs` (add `DiscoverAsync`)
- New/modified: 1 view + 1 VM
- New: `curated-towns.json` schema
