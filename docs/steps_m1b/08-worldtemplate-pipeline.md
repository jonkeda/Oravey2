# Step 08 — Real-World Data Pipeline (WorldTemplate)

**Work streams:** WS8 (Real-World Data Pipeline)
**Depends on:** Step 02 (SQLite storage)
**User-testable result:** Run the build tool → produces a WorldTemplate binary file from OSM + SRTM data for Noord-Holland. Unit tests validate coordinate conversion and data extraction.

---

## Goals

1. Build a compile-time tool that processes real-world data into a game-usable format.
2. Parse SRTM elevation data → compact height grid.
3. Parse OSM PBF data → towns, roads, water, land use.
4. Coordinate conversion (lat/lon ↔ game XZ).
5. Package the starter region (Noord-Holland).

---

## Tasks

### 8.1 — WorldTemplate Data Model

- [ ] Create `MapGen/WorldTemplate/WorldTemplate.cs` — top-level container with continent outlines, global elevation
- [ ] Create `MapGen/WorldTemplate/RegionTemplate.cs` — per-region: elevation grid, road segments, town catalog, land use zones, railways, water bodies
- [ ] Create `MapGen/WorldTemplate/TownEntry.cs` — name, lat/lon, population, boundary polygon, category
- [ ] Create `MapGen/WorldTemplate/TownCategory.cs` enum — `Hamlet`, `Village`, `Town`, `City`, `Metropolis`
- [ ] Create `MapGen/WorldTemplate/RoadSegment.cs` — road class, polyline nodes
- [ ] Create `MapGen/WorldTemplate/LandUseZone.cs` — polygon + type (forest, farmland, residential, industrial, etc.)

### 8.2 — GeoMapper

- [ ] Create `MapGen/WorldTemplate/GeoMapper.cs`
- [ ] Constructor takes origin lat/lon (default: Purmerend 52.50°N, 4.95°E)
- [ ] `LatLonToGameXZ(lat, lon)` → `Vector2` game coordinates (metres from origin)
- [ ] `GameXZToLatLon(x, z)` → `(lat, lon)` — inverse
- [ ] Uses equirectangular approximation with `cos(originLat)` correction for longitude
- [ ] Accurate to <1 m within 200 km of origin

### 8.3 — SRTM Elevation Parser

- [ ] Create `MapGen/WorldTemplate/SrtmParser.cs`
- [ ] Read SRTM HGT files (binary, 1-arcsecond ~30 m resolution)
- [ ] Convert to compact `float[,]` grid aligned to game coordinates via `GeoMapper`
- [ ] Handle SRTM void values (fill with neighbour average)
- [ ] Output: region-sized elevation grid

### 8.4 — OSM PBF Parser

- [ ] Create `MapGen/WorldTemplate/OsmParser.cs`
- [ ] Use `OsmSharp` NuGet package to read PBF files
- [ ] Extract coastlines (`natural=coastline`)
- [ ] Extract admin boundaries (`admin_level=2,4`) for region alignment
- [ ] Extract town/city entries (`place=city/town/village/hamlet` + population)
- [ ] Extract major roads (`highway=motorway/trunk/primary/secondary`)
- [ ] Extract water bodies (`natural=water`, `waterway=river`)
- [ ] Extract railways (`railway=rail`)
- [ ] Extract land use (`landuse=forest/farmland/industrial/residential`)
- [ ] All geometries converted to game coordinates via `GeoMapper`

### 8.5 — WorldTemplate Builder

- [ ] Create `MapGen/WorldTemplate/WorldTemplateBuilder.cs`
- [ ] Combines elevation data + OSM data for a region
- [ ] Groups data into `RegionTemplate` instances
- [ ] Serialises to a binary file format (BinaryWriter with version header)

### 8.6 — Build Tool

- [ ] Create a console application or MSBuild task: `Oravey2.WorldTemplateTool`
- [ ] Input: SRTM HGT directory + OSM PBF file + region bounds
- [ ] Output: `noordholland.worldtemplate` binary file
- [ ] Log: count of towns, roads, water features extracted

### 8.7 — Starter Region Package

- [ ] Download SRTM data for 52°N–53°N, 4°E–5°E (covers Noord-Holland)
- [ ] Download OSM extract for Noord-Holland (from Geofabrik)
- [ ] Run the build tool → produce the starter region template
- [ ] Include the output file in the game project as a content asset

### 8.8 — Unit Tests

File: `tests/Oravey2.Tests/WorldTemplate/GeoMapperTests.cs`

- [ ] `LatLonToXZ_Origin_ReturnsZero` — origin point maps to (0, 0)
- [ ] `LatLonToXZ_KnownDistance_IsAccurate` — Purmerend to Amsterdam (~15 km) maps correctly (error < 50 m)
- [ ] `RoundTrip_LatLonToXZToLatLon_MatchesOriginal` — error < 1 m at regional scale
- [ ] `LatLonToXZ_FarFromOrigin_StillReasonable` — 200 km offset, error < 500 m

File: `tests/Oravey2.Tests/WorldTemplate/SrtmParserTests.cs`

- [ ] `ParseHgtFile_ProducesGrid` — small test HGT file → non-empty grid
- [ ] `VoidValues_AreFilled` — void cells replaced with neighbour average

File: `tests/Oravey2.Tests/WorldTemplate/OsmParserTests.cs`

- [ ] `ParsePbf_ExtractsTowns` — test PBF has known towns → count matches
- [ ] `ParsePbf_ExtractsRoads` — test PBF has roads → at least 1 extracted
- [ ] `TownPositions_ConvertedToGameCoords` — town XZ values are non-zero, in expected range

---

## Verify

```bash
dotnet build src/Oravey2.MapGen
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~WorldTemplate."

# Run the build tool (manual)
dotnet run --project tools/Oravey2.WorldTemplateTool -- --srtm data/srtm --osm data/noordholland.osm.pbf --output content/noordholland.worldtemplate
```

**User test:** The build tool runs and reports: "Extracted 85 towns, 1,247 road segments, 312 water features. Output: noordholland.worldtemplate (8.3 MB)." Unit tests all green.
