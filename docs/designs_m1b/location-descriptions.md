# Location Descriptions & Info Panel

**Status:** Draft  
**Milestone:** M1b  
**Depends on:** Real-World Generation (real-world-generation.md), Zoom Levels (multi-scale-zoom-levels.md)

---

## Summary

Every POI, town, area, and discoverable location has a **three-tier text description** displayed in a side panel when the player requests it. Descriptions are generated at different times to balance quality with responsiveness: short descriptions upfront, longer ones on demand.

| Tier | Name | Length | When Generated | Source |
|------|------|--------|----------------|--------|
| **Low** | Tagline | 1–2 sentences | At curation / world gen | Template (generic POIs) or LLM (towns) |
| **Medium** | Summary | 1–2 paragraphs | On first discovery or panel open | LLM for towns/quests; expanded template for generic |
| **High** | Full dossier | 3–5 paragraphs + data table | On demand when player requests "Read more" | Always LLM |

---

## Info Panel UI

The info panel slides in from the right side of the screen when the player selects a location (click at Level 2/3, or interact at Level 1).

```
┌─────────────────────────────────────────────────┬──────────────────────┐
│                                                  │ ╔══════════════════╗ │
│                                                  │ ║  PURMEREND       ║ │
│                                                  │ ║  Safe Haven      ║ │
│              Game Viewport                       │ ║  ────────────    ║ │
│                                                  │ ║  Threat: ██░░░   ║ │
│                                                  │ ║  Faction: SC     ║ │
│                                                  │ ║                  ║ │
│                                                  │ ║  Walled farming  ║ │
│                                                  │ ║  community on    ║ │
│                                                  │ ║  reclaimed polder║ │
│                                                  │ ║  land...         ║ │
│                                                  │ ║                  ║ │
│                                                  │ ║  [Read more]     ║ │
│                                                  │ ╚══════════════════╝ │
└─────────────────────────────────────────────────┴──────────────────────┘
```

### Panel States

| State | Content Shown | Trigger |
|-------|--------------|---------|
| **Closed** | Nothing | Default |
| **Tagline** | Icon + name + role + 1–2 sentence description | Hover/select POI at Level 2/3 |
| **Summary** | Above + 1–2 paragraph description + stats table | Click POI or press info key |
| **Full dossier** | Above + full multi-paragraph lore + history + tips | Click "Read more" in panel |

---

## Description Tiers

### Tier 1: Tagline (Low)

A quick identifier. Always available the moment a location is discovered or generated.

**For curated towns (LLM-generated during curation):**
> *Walled farming community on reclaimed polder land. Starting area.*

This already exists in the `CuratedTown.Description` field from [real-world-generation.md](real-world-generation.md).

**For generic POIs (template-generated):**

```csharp
public static class DescriptionTemplates
{
    public static string GenerateTagline(PointOfInterest poi) => poi.Type switch
    {
        PoiType.GasStation     => $"Abandoned fuel stop along the {poi.NearestRoad}.",
        PoiType.Checkpoint     => $"Military checkpoint. {ThreatWord(poi.ThreatLevel)} presence.",
        PoiType.RadioTower     => $"Rusted communications tower. May still have power.",
        PoiType.Ruin           => $"Collapsed structures. Scavengers beware.",
        PoiType.Settlement     => $"Small {poi.Faction?.Name ?? "independent"} outpost.",
        PoiType.AnomalyZone    => $"Unstable zone. Strange readings on all frequencies.",
        PoiType.Shipwreck      => $"Rusting hull offshore. Cargo unknown.",
        PoiType.OilPlatform    => $"Derelict offshore platform. Structural integrity questionable.",
        PoiType.Camp           => $"{poi.Faction?.Name ?? "Unknown"} camp. {ThreatWord(poi.ThreatLevel)}.",
        PoiType.Dungeon        => $"Dark entrance leading underground. {ThreatWord(poi.ThreatLevel)} threat.",
        PoiType.Landmark       => $"Notable landmark visible from distance.",
        _ => $"Point of interest."
    };

    private static string ThreatWord(int level) => level switch
    {
        1 => "Minimal",
        2 => "Light",
        3 => "Moderate",
        4 => "Heavy",
        5 => "Extreme",
        _ => "Unknown"
    };
}
```

### Tier 2: Summary (Medium)

Generated on first discovery or when the player opens the info panel. Provides enough context to decide whether to explore.

**For curated towns — LLM prompt:**

```
System: You are writing a field guide for a post-apocalyptic world. 
Write a 2-paragraph summary for a location. Include:
- What the place looks like now
- Who controls it and their attitude toward strangers
- What a traveller might find useful (trade, shelter, supplies)
- Any warnings

Keep the tone matter-of-fact, like a scout's report.

Location: Purmerend
Role: Safe haven
Faction: Survivors' Coalition
Threat level: 2/5
Category: Town (pop ~81,000 pre-war)
Geography: Flat polder land, canals, north of Amsterdam
```

**Example output:**
> Purmerend sits on reclaimed polder land surrounded by drainage canals that now serve as a natural moat. The Survivors' Coalition has reinforced the town's perimeter with earthworks and repurposed shipping containers, creating a walled enclave where farming and trade continue in relative safety.
>
> Strangers are admitted through the south gate after a weapons check. The central market near the old cheese museum operates a barter economy — food is plentiful but ammunition is scarce. Avoid the northern canal district after dark; the Coalition's authority thins out near the water.

**For generic POIs — expanded template:**

```csharp
public static string GenerateSummary(PointOfInterest poi, RegionContext region)
{
    var sb = new StringBuilder();

    sb.AppendLine(GenerateTagline(poi));
    sb.AppendLine();

    // Contextual sentence based on surroundings
    if (region.NearestTown is { } town)
        sb.AppendLine($"Located {region.DistanceToTown:F0} km " +
            $"{region.DirectionToTown} of {town.GameName}.");

    // Type-specific detail
    sb.AppendLine(poi.Type switch
    {
        PoiType.GasStation => "Fuel tanks may still hold residue. " +
            "Check the back office for supplies. Watch for traps.",
        PoiType.Checkpoint => "Concrete barriers and a guard booth. " +
            $"Last controlled by {poi.Faction?.Name ?? "unknown forces"}. " +
            "May still be manned.",
        PoiType.RadioTower => "Climbing the tower provides a vantage point. " +
            "The equipment shed at the base could hold useful electronics.",
        // ... other types
        _ => ""
    });

    return sb.ToString();
}
```

### Tier 3: Full Dossier (High)

Only generated when the player explicitly taps "Read more". Always LLM-generated, even for generic POIs. This is the lore-rich, immersive version.

**LLM prompt:**

```
System: You are writing a detailed field dossier for a post-apocalyptic 
intelligence report. Write 3–5 paragraphs covering:

- History: What this place was before the apocalypse
- Current state: Physical description, condition, who's here now
- Resources: What can be found, traded, or scavenged
- Dangers: Threats, environmental hazards, hostile factions
- Tactical notes: Best approach, time of day, recommended gear

Write in a clipped military/scout report style. No flowery prose.

Location: {name}
Type: {type}
Role: {role}
Faction: {faction}
Threat level: {threat}/5
Nearby: {nearby_pois}
Geography: {biome}, {elevation}m, {nearby_features}
```

**Example output for Purmerend:**
> **Pre-War:** Purmerend was a commuter town of 81,000 in Noord-Holland, known for its cheese market and proximity to Amsterdam. The town was built on reclaimed polder land, criss-crossed by drainage canals connected to the Beemster network.
>
> **Current State:** The Survivors' Coalition established control within the first year after the collapse. They leveraged the town's natural moat — the encircling canals — and built earthwork walls along the perimeter. The old town centre is intact and inhabited. The industrial zone to the east has been converted to workshops and armouries. The northern residential blocks are partially flooded and mostly abandoned.
>
> **Resources:** Purmerend is the primary agricultural hub for the region. Greenhouse farms inside the walls produce vegetables year-round. The market operates daily with barter rates posted on the town board. Medical supplies are available at the converted hospital but rationed. Ammunition is the scarcest commodity — the Coalition restricts sales to proven allies.
>
> **Dangers:** Threat level is low inside walls (2/5). The northern canal district sees occasional mutant incursions from the flooded Beemster polder. Raider probing from Alkmaar occurs monthly along the N244 highway. The Coalition has standing patrols but response time to the outer districts is 10+ minutes.
>
> **Tactical Notes:** Approach from the south via the A7 motorway. The south gate has the fastest processing. Weapons must be peace-bonded inside walls. Best trading hours: 0800–1400. Coalition leadership meets at the old town hall — bring something worth their time.

---

## Data Model

### Description Storage

Descriptions are stored in `world.db` alongside the POI / town data. Tier 1 is always populated. Tiers 2 and 3 are nullable — generated and persisted when first requested.

```sql
CREATE TABLE location_description (
    location_id    INTEGER PRIMARY KEY,  -- poi_id or curated_town_id
    location_type  TEXT NOT NULL,        -- 'poi', 'town', 'region', 'area'
    tagline        TEXT NOT NULL,        -- Tier 1: always present
    summary        TEXT,                 -- Tier 2: null until generated
    dossier        TEXT,                 -- Tier 3: null until generated
    summary_utc    TEXT,                 -- When summary was generated
    dossier_utc    TEXT,                 -- When dossier was generated
    llm_model      TEXT                  -- Which model wrote the higher tiers
);
```

### Code Model

```csharp
public record LocationDescription(
    int LocationId,
    LocationType Type,
    string Tagline,
    string? Summary,
    string? Dossier);

public enum LocationType : byte
{
    Poi,
    Town,
    Region,
    Area,
    Building,
    Dungeon
}
```

---

## Description Generation Service

```csharp
public class DescriptionService
{
    private readonly WorldMapStore _world;
    private readonly ILlmClient _llm;

    /// Returns the tagline (always available, no async needed)
    public string GetTagline(int locationId)
    {
        return _world.GetDescription(locationId).Tagline;
    }

    /// Returns the summary, generating it if not yet available
    public async Task<string> GetOrGenerateSummaryAsync(int locationId)
    {
        var desc = _world.GetDescription(locationId);
        if (desc.Summary is not null)
            return desc.Summary;

        var context = _world.GetLocationContext(locationId);
        string summary;

        if (context.IsGenericPoi)
        {
            // Template-based for generic POIs
            summary = DescriptionTemplates.GenerateSummary(context.Poi, context.Region);
        }
        else
        {
            // LLM-generated for towns, dungeons, special locations
            var prompt = DescriptionPrompts.BuildSummaryPrompt(context);
            summary = await _llm.GenerateTextAsync(prompt);
        }

        _world.UpdateDescription(locationId, summary: summary);
        return summary;
    }

    /// Returns the full dossier, always LLM-generated
    public async Task<string> GetOrGenerateDossierAsync(int locationId)
    {
        var desc = _world.GetDescription(locationId);
        if (desc.Dossier is not null)
            return desc.Dossier;

        var context = _world.GetLocationContext(locationId);
        var prompt = DescriptionPrompts.BuildDossierPrompt(context);
        var dossier = await _llm.GenerateTextAsync(prompt);

        _world.UpdateDescription(locationId, dossier: dossier);
        return dossier;
    }
}
```

---

## Generation Timing

```
World creation
  └── LLM curation pass
        └── Tier 1 taglines for all curated towns  ← stored immediately
        └── Tier 1 taglines for generic POIs        ← template-generated

Player discovers location (fog of war clears)
  └── Tier 1 shown in minimap / POI marker tooltip

Player opens info panel (click / info key)
  └── Tier 2 requested
        ├── Generic POI? → template expansion (instant)
        └── Town/dungeon? → LLM call (async, panel shows tagline + spinner)
              └── Result cached in world.db

Player clicks "Read more"
  └── Tier 3 requested
        └── LLM call (async, panel shows summary + spinner)
              └── Result cached in world.db
```

### Loading UX

When an LLM call is in progress, the panel shows the already-available tier with a subtle loading indicator below:

```
╔══════════════════╗
║  PURMEREND       ║
║  Safe Haven      ║
║  ────────────    ║
║  Walled farming  ║
║  community on    ║
║  reclaimed polder║
║  land.           ║
║                  ║
║  ┄┄┄ Loading ┄┄┄ ║
║                  ║
╚══════════════════╝
```

Once the LLM responds, the text smoothly expands. If the LLM fails (offline, timeout), show the current tier with a "Description unavailable — try again later" note.

---

## Offline Fallback

When offline (no LLM available):
- **Tier 1:** Always available (pre-generated).
- **Tier 2:** Template-based expansion for all location types (less flavourful but functional).
- **Tier 3:** Not available offline. Show: *"Detailed report requires communications link."* (in-universe flavour for "no internet").

The game is fully playable with Tier 1 + template Tier 2 only.

---

## Description Scope by Location Type

| Location Type | Tier 1 Source | Tier 2 Source | Tier 3 Source | Typical Info |
|--------------|---------------|---------------|---------------|-------------|
| **Curated town** | LLM (curation) | LLM | LLM | History, faction, trade, danger, approach |
| **Generic POI** (gas station, checkpoint) | Template | Template expansion | LLM | What's here, condition, loot hints, danger |
| **Dungeon** | LLM (curation) | LLM | LLM | Backstory, layout hints, enemy types, loot quality |
| **Region** (Level 2 area) | Template from biome + stats | LLM | LLM | Geography, climate, faction control, travel warnings |
| **Continent** (Level 3) | Template | LLM | LLM | Overview, major factions, climate zones, travel routes |
| **Building** (Level 1 interior) | Template | Template / LLM | LLM | What it was, what's inside, danger, loot |
| **Area** (wilderness zone) | Template from biome | Template | LLM | Terrain, wildlife, hazards, scavenge potential |

---

## Panel Data Layout

The info panel has a consistent structure across all tiers:

### Header (Always Visible)

```
[Icon]  LOCATION NAME
        Role / Type label
        ─────────────────
        Threat: ██░░░  (2/5)
        Faction: Survivors' Coalition
        Distance: 14 km NW
```

### Body (Tier-Dependent)

| Section | Tier 1 | Tier 2 | Tier 3 |
|---------|--------|--------|--------|
| Description text | 1–2 sentences | 1–2 paragraphs | 3–5 paragraphs |
| Stats table | — | Basic (population, trade goods) | Full (defences, resources, schedules) |
| Warnings | — | Hazard icons | Detailed tactical notes |
| History | — | — | Pre-war and post-war history |
| Nearby | — | List of adjacent POIs | Distances + descriptions of neighbours |
| Actions | — | — | "Set waypoint", "Plan route" buttons |

### Stats Table (Tier 2+)

```
╔════════════════════════════════╗
║  Population:    ~2,400         ║
║  Trade goods:   Food, tools    ║
║  Scarce:        Ammo, medicine ║
║  Fuel:          Available      ║
║  Fast travel:   ✓ (south gate) ║
║  Medical:       Rationed       ║
╚════════════════════════════════╝
```

---

## LLM Token Budget

To keep generation fast and costs predictable:

| Tier | Max Tokens (Output) | Approx. Words | Target Latency |
|------|--------------------:|-------------:|----------------|
| 1 (Tagline) | 50 | ~30 | Pre-generated |
| 2 (Summary) | 200 | ~150 | < 3 s |
| 3 (Dossier) | 600 | ~450 | < 8 s |

Use a smaller/faster model for Tier 2 (e.g., GPT-4o-mini equivalent) and a larger model for Tier 3 if available.

---

## Files to Create / Modify

| Action | File | Notes |
|--------|------|-------|
| Create | `Services/DescriptionService.cs` | Tier routing, LLM calls, caching |
| Create | `Services/DescriptionPrompts.cs` | Prompt builders for Tier 2 and 3 |
| Create | `Services/DescriptionTemplates.cs` | Template-based taglines and summaries |
| Create | `Models/LocationDescription.cs` | Description record + LocationType enum |
| Create | `UI/InfoPanel/InfoPanelController.cs` | Panel show/hide, tier progression |
| Create | `UI/InfoPanel/InfoPanelView.cs` | Layout, text display, loading state |
| Create | `Data/LocationDescriptionSchema.sql` | `location_description` table DDL |
| Modify | `Data/WorldMapStore.cs` | Add description read/write methods |
| Modify | `Data/WorldDbSchema.sql` | Add `location_description` table |
| Modify | `MapGen/Curation/TownCurator.cs` | Generate Tier 1 taglines during curation |

---

## Acceptance Criteria

1. Every discovered POI shows a Tier 1 tagline in its tooltip / marker.
2. Opening the info panel for a curated town triggers LLM Tier 2 generation on first open; subsequent opens load from cache.
3. Opening the info panel for a generic POI shows template-expanded Tier 2 instantly.
4. Clicking "Read more" triggers LLM Tier 3 generation with a loading indicator.
5. Generated descriptions persist in `world.db` — never regenerated once cached.
6. Offline play shows Tier 1 + template Tier 2 for all locations; Tier 3 is gracefully unavailable.
7. Panel displays header (icon, name, threat, faction, distance) consistently across all tiers.
8. LLM Tier 2 returns within 3 seconds; Tier 3 within 8 seconds on a standard connection.
9. Descriptions for regions and continents work at Level 2/3 zoom with appropriate content.
10. Stats table appears at Tier 2+ with location-relevant data (population, trade, resources).
