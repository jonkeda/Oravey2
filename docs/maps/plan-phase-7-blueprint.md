# Phase 7 — Blueprint Compiler

> **Status:** Not Started  
> **Depends on:** Phase 2 (JSON loading exists so compiler has output target)  
> **Optional deps:** Phases 3, 4, 6 (height/water/buildings — compiler can output these fields even if renderer doesn't use them yet)

---

## Goal

Build the deterministic compiler that transforms a `MapBlueprint.json` (LLM-generated) into runtime chunk files. This closes the loop: LLM → Blueprint → Compiler → JSON chunks → Engine.

---

## Steps

### Step 7.1 — Blueprint data model

**File:** `src/Oravey2.Core/World/Blueprint/MapBlueprint.cs` (new)

C# records matching the schema from doc 03 §5 (static-world-only version):

```csharp
public sealed record MapBlueprint(
    string Name,
    string Description,
    BlueprintSource Source,
    BlueprintDimensions Dimensions,
    TerrainBlueprint Terrain,
    WaterBlueprint? Water,
    RoadBlueprint[]? Roads,
    BuildingBlueprint[]? Buildings,
    PropBlueprint[]? Props,
    ZoneBlueprint[]? Zones
);

public sealed record BlueprintSource(string RealWorldLocation, string? Notes);
public sealed record BlueprintDimensions(int ChunksWide, int ChunksHigh);
public sealed record TerrainBlueprint(int BaseElevation, TerrainRegion[] Regions, SurfaceRule[] Surfaces);
public sealed record TerrainRegion(string Id, string Type, /* ... region-specific fields */);
// ... etc for water, roads, buildings, props, zones
```

**Tests:** `MapBlueprintTests.cs`
- All records instantiate with valid data
- Serialize/deserialize round-trip
- Minimal blueprint (just dimensions + base elevation) is valid

---

### Step 7.2 — Blueprint JSON loader

**File:** `src/Oravey2.Core/World/Blueprint/BlueprintLoader.cs` (new)

```
BlueprintLoader
├── Load(string path) → MapBlueprint
├── LoadFromString(string json) → MapBlueprint
├── Validate(MapBlueprint) → ValidationResult
```

**Tests:** `BlueprintLoaderTests.cs`
- Load valid blueprint JSON → correct dimensions, region count
- Load minimal blueprint → only required fields populated
- Invalid JSON → descriptive error
- Missing required field → validation error

---

### Step 7.3 — Blueprint validator

**File:** `src/Oravey2.Core/World/Blueprint/BlueprintValidator.cs` (new)

```
BlueprintValidator
├── Validate(MapBlueprint) → ValidationResult
│     checks:
│     - dimensions > 0
│     - all region coordinates within chunk bounds
│     - no overlapping building footprints
│     - all zone chunkRanges within dimensions
│     - road paths within bounds
│     - building mesh asset paths not empty
```

```csharp
public sealed record ValidationResult(bool IsValid, ValidationError[] Errors);
public sealed record ValidationError(string Code, string Message, string? Context);
```

**Tests:** `BlueprintValidatorTests.cs`
- Valid blueprint → IsValid true, no errors
- Zero dimensions → error "INVALID_DIMENSIONS"
- Region outside bounds → error "REGION_OUT_OF_BOUNDS"
- Overlapping buildings → error "BUILDING_OVERLAP"
- Zone outside dimensions → error "ZONE_OUT_OF_BOUNDS"
- Empty mesh asset path → error "MISSING_MESH_ASSET"
- Multiple errors in one blueprint → all reported

---

### Step 7.4 — Terrain compiler pass

**File:** `src/Oravey2.Core/World/Blueprint/TerrainCompiler.cs` (new)

Converts terrain regions to per-tile TileData.

```
TerrainCompiler
├── Compile(MapBlueprint) → TileData[chunksWide*16, chunksHigh*16]
│
│   Pipeline:
│   1. Initialize all tiles at baseElevation, default surface
│   2. Apply elevation regions (polygon fill + noise)
│   3. Apply surface rules per region
│   4. Set walkability flags (all terrain walkable unless cliff/water)
│   5. Set VariantSeed per tile (hash of position)
```

**Tests:** `TerrainCompilerTests.cs`
- Flat blueprint (no regions) → all tiles at baseElevation, default surface
- Single elevation region → tiles inside polygon have elevated height
- Surface rule "Asphalt 100%" in region → all tiles in region are Asphalt
- Mixed surface rule (60/30/10) → approximately correct distribution (seed-deterministic)
- Two non-overlapping elevation regions → each has correct height range
- VariantSeed differs per tile position (no repeating pattern in small area)

---

### Step 7.5 — Road compiler pass

**File:** `src/Oravey2.Core/World/Blueprint/RoadCompiler.cs` (new)

Carves roads into the terrain grid.

```
RoadCompiler
├── CompileRoads(TileData[,] grid, RoadBlueprint[]) → void (modifies grid in place)
│
│   Pipeline:
│   1. Interpolate road path points to tile coordinates
│   2. Expand path to road width (perpendicular offset)
│   3. Set road tiles: surface = road.SurfaceType, walkable
│   4. Smooth height along road path (roads don't climb cliffs)
│   5. Apply condition: low condition → some tiles become Rubble
```

**Tests:** `RoadCompilerTests.cs`
- Straight road 3 wide → 3 tiles wide at each point
- Road sets surface type correctly (Asphalt)
- Road tiles are walkable
- Road over height variation → height smoothed along path
- Road with condition 0.3 → some tiles are Rubble instead of Asphalt
- Road with condition 1.0 → all tiles are road surface

---

### Step 7.6 — Water compiler pass

**File:** `src/Oravey2.Core/World/Blueprint/WaterCompiler.cs` (new)

Applies rivers, lakes to the terrain grid.

```
WaterCompiler
├── CompileWater(TileData[,] grid, WaterBlueprint) → void
│
│   River: carve channel (lower HeightLevel), set WaterLevel
│   Lake: set uniform WaterLevel in circular region
│   Bridge: mark bridge tiles as walkable at deck height
```

**Tests:** `WaterCompilerTests.cs`
- River path → tiles along path have WaterLevel > HeightLevel
- River width → correct number of tiles wide
- Lake → circular region has water
- Lake center deeper than edge (terrain carved into bowl)
- Bridge tiles → walkable despite being over water
- No water outside defined regions

---

### Step 7.7 — Building & prop compiler pass

**File:** `src/Oravey2.Core/World/Blueprint/StructureCompiler.cs` (new)

Places buildings and props into the grid.

```
StructureCompiler
├── CompileStructures(TileData[,] grid, BuildingBlueprint[], PropBlueprint[]) → (BuildingDefinition[], PropDefinition[])
│
│   Buildings: compute footprint tiles, set StructureId, clear walkable
│   Props: compute tile, optionally clear walkable
```

**Tests:** `StructureCompilerTests.cs`
- Building at position → footprint tiles have StructureId
- Building footprint tiles are non-walkable
- Prop (non-blocking) → tile unchanged
- Prop (blocking) → tile non-walkable

---

### Step 7.8 — Zone compiler pass

**File:** `src/Oravey2.Core/World/Blueprint/ZoneCompiler.cs` (new)

Converts zone blueprints to ZoneDefinition records.

```
ZoneCompiler
├── CompileZones(ZoneBlueprint[]) → ZoneDefinition[]
```

Straightforward mapping — zone blueprint fields map 1:1 to ZoneDefinition.

**Tests:** `ZoneCompilerTests.cs`
- Zone blueprint → correct ZoneDefinition fields
- Multiple zones → all compiled
- Zone biome string maps to BiomeType enum

---

### Step 7.9 — Master compiler (orchestrator)

**File:** `src/Oravey2.Core/World/Blueprint/MapCompiler.cs` (new)

Runs all passes in order and outputs chunk files.

```
MapCompiler
├── Compile(MapBlueprint, string outputDirectory) → CompilationResult
│
│   1. Validate blueprint
│   2. TerrainCompiler → base grid
│   3. RoadCompiler → carve roads
│   4. WaterCompiler → add water
│   5. StructureCompiler → place buildings/props
│   6. ZoneCompiler → zones
│   7. Split grid into 16×16 chunks
│   8. Write world.json + chunks/*.json + zones.json + buildings.json + props.json
```

```csharp
public sealed record CompilationResult(
    bool Success,
    int ChunksGenerated,
    int BuildingsPlaced,
    int PropsPlaced,
    ValidationError[] Warnings
);
```

**Tests:** `MapCompilerTests.cs`
- Compile minimal blueprint → world.json + 1 chunk file exist
- Compile blueprint with 2×2 chunks → 4 chunk files
- Compile with buildings → buildings.json exists
- Compiled chunks loadable by Phase 2 MapLoader
- Full round-trip: Blueprint → Compile → Load → tiles match expected

---

### Step 7.10 — End-to-end test with sample blueprint

**File:** `tests/Oravey2.Tests/Fixtures/Blueprints/sample_portland.json` (new)

A small (2×2 chunk) blueprint with:
- 1 river
- 1 elevation region
- 2 roads
- 1 building
- 2 props
- 2 zones

**Tests:** `EndToEndBlueprintTests.cs`
- Load sample blueprint → validates successfully
- Compile → all chunk files generated
- Load compiled output → WorldMapData with correct dimensions
- River tiles have water
- Building footprint tiles are non-walkable
- Roads have correct surface type
- Zones loaded into ZoneRegistry

---

## Acceptance Criteria

| # | Criteria | How to Verify |
|---|---------|--------------|
| 1 | Blueprint JSON loads and validates | Unit tests |
| 2 | Validator catches all defined error cases | 10+ validation tests |
| 3 | Terrain compiler produces correct heights and surfaces from regions | Unit tests |
| 4 | Road compiler carves correct width and smooths height | Unit tests |
| 5 | Water compiler creates rivers and lakes with correct levels | Unit tests |
| 6 | Structure compiler marks footprints correctly | Unit tests |
| 7 | Master compiler produces files loadable by MapLoader | Round-trip test |
| 8 | End-to-end: sample blueprint → compiled → loaded → correct world | Integration test |
| 9 | All existing tests pass | `dotnet test` |
| 10 | 40+ new unit tests across all compiler components | Test count |

## Verification

```powershell
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Blueprint" --verbosity normal
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Compiler" --verbosity normal
dotnet test tests/Oravey2.Tests --verbosity quiet
```
