# Step 15 — Location Descriptions

**Work streams:** WS14 (Location Descriptions)
**Depends on:** Step 02 (SQLite storage), Step 09 (procedural generation)
**User-testable result:** Click a POI on the map → info panel slides in with a tiered description: summary first, expanding to full flavour text. LLM-generated descriptions feel contextual and atmospheric.

---

## Goals

1. `DescriptionService` with template + LLM pipeline.
2. Three-tier descriptions: summary (1 line), medium (paragraph), full (multi-paragraph).
3. Info panel UI with smooth slide-in/out animation.
4. Caching: generated descriptions stored in SQLite, only generated once per POI.

---

## Tasks

### 15.1 — Description Data Model

- [ ] Create `Descriptions/LocationDescription.cs`
- [ ] Fields: `LocationId` (int), `Summary` (string, ≤80 chars), `Medium` (string, ≤300 chars), `Full` (string, ≤1500 chars), `GeneratedAt` (DateTime), `TemplateId` (string)
- [ ] SQLite table: `location_descriptions` with matching columns
- [ ] Add `IDescriptionStore` interface to `MapDataProvider` for CRUD

### 15.2 — Template System

- [ ] Create `Descriptions/DescriptionTemplate.cs`
- [ ] Templates keyed by `POIType` × `BiomeType` combination
- [ ] Each template: opening phrase patterns, atmosphere keywords, hazard descriptors
- [ ] Example: (AbandonedTown, Wasteland) → "The ruins of {name} bake under a relentless sun…"
- [ ] Fallback template for uncovered combinations
- [ ] Templates stored as embedded resources (JSON or TOML files)

### 15.3 — LLM Integration

- [ ] Create `Descriptions/DescriptionGenerator.cs`
- [ ] Builds a prompt from template + world context (weather, faction, nearby features, season)
- [ ] Calls LLM service (existing `ILlmService` interface)
- [ ] Parses response into summary / medium / full tiers
- [ ] Retry logic: up to 2 retries if response doesn't parse
- [ ] Fallback: if LLM unavailable, use template-only generation (no LLM)

### 15.4 — Description Caching

- [ ] On first request: check `location_descriptions` table
- [ ] Cache hit: return stored description immediately
- [ ] Cache miss: generate via LLM, store result, return
- [ ] Invalidation: re-generation triggered by major world events (town destroyed, faction change)
- [ ] Bulk pre-generation: `WorldGenerator` pre-generates descriptions for starting region POIs

### 15.5 — Info Panel UI

- [ ] Create `UI/LocationInfoPanel.cs`
- [ ] Panel slides in from right edge of screen
- [ ] Header: location name + POI type icon
- [ ] Body: summary tier shown by default
- [ ] "Read more" button expands to medium tier
- [ ] "Full description" expands to full tier with scroll
- [ ] Close button or click-outside-to-dismiss
- [ ] Loading spinner while LLM generates

### 15.6 — POI Click Integration

- [ ] Clicking a POI marker on the map (L1 or L2) opens the info panel
- [ ] Clicking a town name label opens the info panel
- [ ] Right-click POI: context menu with "Description", "Set waypoint", "Mark on map"
- [ ] Keyboard shortcut: I key opens info for nearest POI

### 15.7 — Description Quality

- [ ] Summary: atmospheric single sentence, names the location and type
- [ ] Medium: adds historical context, current state, notable features
- [ ] Full: adds sensory details (sounds, smells), rumours, possible loot hints, faction presence
- [ ] Post-apocalyptic tone: decay, danger, survival, hope
- [ ] No fourth-wall-breaking: descriptions stay in-world

### 15.8 — Journal Integration

- [ ] Discovered locations recorded in player journal
- [ ] Journal → Locations tab lists all discovered POIs with summaries
- [ ] Click journal entry → opens full info panel
- [ ] Sort by: discovery date, distance, type, faction

### 15.9 — Unit Tests

File: `tests/Oravey2.Tests/Descriptions/DescriptionTemplateTests.cs`

- [ ] `GetTemplate_AbandonedTown_Wasteland_ReturnsTemplate` — known combination returns template
- [ ] `GetTemplate_UnknownCombo_ReturnsFallback` — unrecognised combo returns fallback
- [ ] `AllPOITypes_HaveAtLeastFallback` — no POI type causes a null template

File: `tests/Oravey2.Tests/Descriptions/DescriptionGeneratorTests.cs`

- [ ] `Generate_WithLlm_ReturnsSummaryMediumFull` — mock LLM returns parseable response → 3 tiers populated
- [ ] `Generate_LlmUnavailable_FallsBackToTemplate` — LLM throws → template-only description returned
- [ ] `Generate_LlmBadResponse_RetriesAndSucceeds` — first call returns garbage, second returns valid
- [ ] `SummaryLength_Under80Chars` — generated summary truncated/validated to ≤80

File: `tests/Oravey2.Tests/Descriptions/DescriptionStoreTests.cs`

- [ ] `Save_ThenLoad_RoundTrips` — store description, retrieve by LocationId, matches
- [ ] `CacheHit_DoesNotCallLlm` — second request for same location doesn't invoke generator
- [ ] `Invalidate_RegeneratesOnNextRequest` — after invalidation, next request calls generator

### 15.10 — UI Tests

File: `tests/Oravey2.UITests/Descriptions/InfoPanelTests.cs`

- [ ] `ClickPOI_InfoPanelAppears` — click a town POI, info panel slides in with location name and summary text
- [ ] `ExpandDescription_ShowsMoreText` — click "Read more", panel content grows to show medium description
- [ ] `ClosePanel_Dismisses` — click close button, panel slides out

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Descriptions."
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~InfoPanel"
```

**User test:** Navigate to a town on the map. Click the town marker. An info panel slides in from the right showing the town name and a one-line atmospheric summary. Click "Read more" — a richer paragraph appears with historical context and current state. Click "Full description" — detailed sensory text fills the panel. Close the panel, then open it again — the description loads instantly from cache.
