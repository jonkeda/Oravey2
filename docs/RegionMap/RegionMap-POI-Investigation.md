# Region Map – POI Investigation

## Symptom

The region map overlay shows **zone names** ("flooding zone", "toxic zone", "collapse zone") instead of the expected **town names** ("The Dam (Ruins)" / Amsterdam, "Purgatory" / Purmerend).

## Root Cause

Both towns **and** zones are stored in the `poi` table, but the zone POIs use **town-local** chunk coordinates while the overlay's `GridToMap()` treats all coordinates as **region-level** chunk indices. This causes two problems:

1. **Zone POIs dominate the display.** Each town has 3–4 hazard zones (e.g. "flooding zone", "toxic zone") inserted by `ImportTown()`, so they outnumber the town POIs.
2. **Town POIs from `ImportCuratedTowns()` use raw lat/lon as grid coordinates** (`(int)t.Longitude, (int)t.Latitude` → grid_x=4, grid_y=52 for Amsterdam) instead of proper chunk indices. These land far outside the visible map area.

### Data Flow

```
Content Pack
├─ data/curated-towns.json          13 CuratedTownDto entries (lat/lon + GameName + Size)
├─ overworld/world.json             Simplified version (gameX=0, gameY=0 for all towns)
└─ towns/
   ├─ The Dam (Ruins)/zones.json    4 zones (zone_main, flooding, collapse, toxic)
   ├─ Purgatory/zones.json          4 zones (zone_main, collapse, fire, flooding)
   └─ Island Haven/zones.json       ...
```

### Import Pipeline (`ContentPackImporter.Import()`)

| Step | Source | Type | grid_x / grid_y | Problem |
|------|--------|------|-----------------|---------|
| `ImportTown()` → zones.json | Zone entries | `"zone"` | Town-local chunk coords (0–24) | Coords are local to each town, not region-level |
| `ImportCuratedTowns()` | curated-towns.json | `"town"` | `(int)t.Longitude`, `(int)t.Latitude` | **BUG:** Raw lat/lon (4, 52) used as chunk indices |

### What the DB Contains (NH Region)

| name | type | grid_x | grid_y | origin |
|------|------|--------|--------|--------|
| The Dam (Ruins) | town | 4 | 52 | `ImportCuratedTowns` – raw lon/lat |
| Purgatory | town | 4 | 52 | `ImportCuratedTowns` – raw lon/lat |
| Haven-Guard | town | 4 | 52 | `ImportCuratedTowns` – raw lon/lat |
| … 10 more towns | town | 4–5 | 52 | `ImportCuratedTowns` – raw lon/lat |
| The Dam (Ruins) | zone | 0 | 0 | zone_main from zones.json |
| flooding zone | zone | 0 | 24 | zone_hazard_0 |
| collapse zone | zone | 0 | 0 | zone_hazard_1 |
| toxic zone | zone | 24 | 0 | zone_hazard_2 |
| Purgatory | zone | 0 | 0 | zone_main |
| collapse zone | zone | 0 | 24 | zone_hazard_0 |
| … | zone | … | … | … |

All town POIs cluster at grid (4, 52) – far outside the map. Zone POIs at (0–24, 0–24) fall within range and are what the user sees.

### Overlay Rendering

`GetMarkerStyle(poi.Type)` uses the `type` field for styling:
- `"town"` → 10px warm-yellow marker
- `"zone"` → 8px gray marker (falls into `_` default branch)

Neither "city" nor "metropolis" ever appear because `ImportCuratedTowns` hardcodes `"town"` as the type. The `Size` field ("City", "Village", etc.) is stored in the `icon` column but never read by the overlay.

## Bugs Found

### Bug 1: `ImportCuratedTowns` uses raw lat/lon as grid coordinates

**File:** `src/Oravey2.Core/Data/ContentPackImporter.cs` line 308–309

```csharp
_store.InsertPoi(regionId, t.GameName, "town",
    (int)t.Longitude, (int)t.Latitude,    // ← BUG
    description: t.RealName, icon: t.Size);
```

Should use `ComputeTownChunkOffsets()` (already computed earlier in the method) to convert lat/lon to region-level chunk coordinates:

```csharp
var offset = townOffsets.GetValueOrDefault(t.GameName, (0, 0));
_store.InsertPoi(regionId, t.GameName, "town",
    offset.Item1, offset.Item2,
    description: t.RealName, icon: t.Size);
```

### Bug 2: Zone grid coordinates are town-local, not region-level

**File:** `src/Oravey2.Core/Data/ContentPackImporter.cs` line 201

```csharp
_store.InsertPoi(regionId, z.Name, "zone", z.ChunkStartX, z.ChunkStartY, …);
```

These coordinates are relative to the town's local grid. They need the town's chunk offset added:

```csharp
_store.InsertPoi(regionId, z.Name, "zone",
    chunkOffsetX + z.ChunkStartX, chunkOffsetY + z.ChunkStartY, …);
```

### Bug 3: Town POI type is always "town" regardless of size

`ImportCuratedTowns` hardcodes `"town"` as the type. The `Size` field is stored in the `icon` column. Either:
- Use `t.Size` as the POI type so `GetMarkerStyle()` can differentiate, or
- Have `GetMarkerStyle()` check the `icon` field as a secondary source.

## Recommended Fixes

1. **Fix `ImportCuratedTowns`**: Use `townOffsets` for grid coordinates, use `t.Size` as the POI type.
2. **Fix zone coordinate offset**: Add `chunkOffsetX/Y` to zone POI grid coordinates.
3. **Filter zones from map display**: Either skip `"zone"` type POIs in `AddPoiMarkers()`, or render them differently (smaller, no label) to reduce clutter.
4. **Re-import the NH content pack** after fixing to regenerate the `world.db`.
