# Step 07 — LLM Town Design (Per Town)

## Goal

Implement per-town LLM feature design. Each town gets a `design.json` with
landmark, key locations, layout style, and hazards.

## Deliverables

### 7.1 `TownDesigner` service (new, in `Oravey2.MapGen`)

```csharp
public sealed class TownDesigner
{
    public async Task<TownDesign> DesignAsync(
        CuratedTown town,
        string regionContext,      // brief region description for LLM context
        int seed,
        CancellationToken ct = default);
}
```

Prompt gives the LLM the town's name, role, faction, threat level, and asks
it to design:
- One landmark building (name, visual description for Meshy, size)
- 3–8 key locations (name, purpose, visual description, size)
- Layout style (grid / radial / organic / linear / clustered / compound)
- 0–3 environmental hazards (type, description, location hint)

Returns `TownDesign` record (defined in `generation-pipeline-v3.md`).

### 7.2 Data model records

```csharp
public sealed record TownDesign(
    string TownName,
    LandmarkBuilding Landmark,
    List<KeyLocation> KeyLocations,
    string LayoutStyle,
    List<EnvironmentalHazard> Hazards);

public sealed record LandmarkBuilding(
    string Name, string VisualDescription, string SizeCategory);

public sealed record KeyLocation(
    string Name, string Purpose, string VisualDescription, string SizeCategory);

public sealed record EnvironmentalHazard(
    string Type, string Description, string LocationHint);
```

### 7.3 `TownDesignStepView` / `TownDesignStepViewModel`

- **Left panel — town list**: all curated towns, each showing status
  (`Not designed` / `Designed ✅`)
- **Right panel — design detail** for selected town:
  - Town summary (name, role, threat)
  - **[Design Town]** button → calls `TownDesigner.DesignAsync()`
  - Result cards: landmark, key locations, layout style badge, hazards
  - **[Accept]** → saves `design.json`
  - **[Re-generate]** → re-runs with new seed
  - **[Edit JSON]** → raw JSON editor for manual tweaks
- **Batch bar**:
  - **[Design All Remaining]** → sequential processing with progress bar,
    auto-accept each result

### 7.4 File output

Per town: `content/Oravey2.Apocalyptic.NL.NH/towns/{gameName}/design.json`

```json
{
  "townName": "Havenburg",
  "landmark": {
    "name": "Fort Kijkduin",
    "visualDescription": "A massive coastal fortress with crumbling stone walls, rusted artillery platforms, and a collapsed watchtower overgrown with vines",
    "sizeCategory": "large"
  },
  "keyLocations": [
    {
      "name": "The Drydock Market",
      "purpose": "shop",
      "visualDescription": "An old naval drydock converted into a covered marketplace with canvas tarps and salvaged ship parts as stalls",
      "sizeCategory": "medium"
    }
  ],
  "layoutStyle": "compound",
  "hazards": [
    {
      "type": "flooding",
      "description": "The harbour district floods at high tide, making lower streets impassable",
      "locationHint": "south-west waterfront"
    }
  ]
}
```

### 7.5 Tests

- `TownDesigner.DesignAsync` — mock LLM, verify prompt includes town metadata,
  verify JSON parsing
- ViewModel: design triggers save, batch processes all remaining, status updates

## Dependencies

- Step 06 (curated towns list)
- Step 01 (content pack folder exists)

## Estimated scope

- New files: `TownDesigner.cs`, `TownDesign.cs` (+ sub-records), view + VM
- ~4–6 new files
