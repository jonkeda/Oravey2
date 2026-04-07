# Step 01 — Data Model & Cleanup

**Work streams:** WS1 (Data Model Foundation)
**Depends on:** Nothing — this is the starting point
**User-testable result:** Solution compiles. All unit tests pass. Old code removed.

---

## Goals

1. Expand `TileData` to the full planned layout (all fields from all design docs).
2. Add all new enums, records, and data types needed by later steps.
3. Remove old code that is replaced by the new architecture.
4. Prove everything works with unit tests.

---

## Tasks

### 1.1 — Expand TileFlags

- [x] Change `TileFlags` backing type from `byte` to `ushort`
- [x] Add flags: `Forested = 16`, `Interior = 32`, `FastTravel = 64`, `Searchable = 128`
- [x] Verify existing flags (`Walkable`, `Irradiated`, `Burnable`, `Destructible`) are unchanged

### 1.2 — New Enums

- [x] Create `LiquidType` enum: `None`, `Water`, `Toxic`, `Acid`, `Sewage`, `Lava`, `Oil`, `Frozen`, `Anomaly`
- [x] Create `CoverEdges` flags enum: `None`, `North = 1`, `East = 2`, `South = 4`, `West = 8`
- [x] Create `CoverLevel` enum: `None`, `Half`, `Full`
- [x] Create `ChunkMode` enum: `Heightmap`, `Hybrid`
- [x] Create `MapLayer` enum: `DeepUnderground`, `Underground`, `Surface`, `Elevated`
- [x] Create `LinearFeatureType` enum: `Path`, `DirtRoad`, `Road`, `Highway`, `Rail`, `River`, `Stream`, `Canal`, `Pipeline`

### 1.3 — New Records

- [x] Create `LinearFeatureNode` record: `Vector2 Position`, `float? OverrideHeight`
- [x] Create `LinearFeature` record: `LinearFeatureType Type`, `string Style`, `float Width`, `IReadOnlyList<LinearFeatureNode> Nodes`
- [x] Create `TerrainModifier` abstract record
- [x] Create `FlattenStrip` : `TerrainModifier` — `CentreLine`, `Width`, `TargetHeight`
- [x] Create `ChannelCut` : `TerrainModifier` — `CentreLine`, `Width`, `Depth`
- [x] Create `LevelRect` : `TerrainModifier` — `Min`, `Max`, `TargetHeight`
- [x] Create `Crater` : `TerrainModifier` — `Centre`, `Radius`, `Depth`

### 1.4 — Expand TileData

- [x] Add `LiquidType Liquid` field
- [x] Add `CoverEdges HalfCover` field
- [x] Add `CoverEdges FullCover` field
- [x] Update `TileData.Empty` default
- [x] Update `HasWater` logic: when `Liquid == LiquidType.None` and `WaterLevel > HeightLevel`, default to Water for backward compat during transition
- [x] Update `LegacyTileType` property if still referenced, or remove it

### 1.5 — Expand ChunkData

- [x] Add `ChunkMode Mode` property (default `Heightmap`)
- [x] Add `MapLayer Layer` property (default `Surface`)
- [x] Add `IReadOnlyList<TerrainModifier> TerrainModifiers` property
- [x] Add `IReadOnlyList<LinearFeature> LinearFeatures` reference (or region-level; stored for chunk access)

### 1.6 — Update TileDataFactory

- [x] Update factory methods to accept/default new fields
- [x] Ensure all callers pass valid values

### 1.7 — Fix All Compilation Errors

- [x] Update every `TileData` construction site in the codebase to supply new default values
- [x] This is mechanical — search for `new TileData(` and `TileData(` constructor calls

### 1.8 — Remove Old Code

- [x] Delete old `MapBlueprint` JSON serialisation code in `Blueprint/` folder
- [x] Delete `PromptBuilder.cs`, `BlueprintCollector.cs` from `Oravey2.MapGen`
- [x] Delete `ValidateBlueprintTool.cs`, `WriteBlueprintTool.cs`, `CheckOverlapTool.cs`, `CheckWalkabilityTool.cs` from `Oravey2.MapGen/Tools`
- [x] Delete Portland map data files from `Oravey2.Windows/Maps/portland/`
- [x] Delete any JSON map loader code
- [x] Remove `LegacyTileType` property from `TileData` if nothing references it after cleanup

### 1.9 — Unit Tests

File: `tests/Oravey2.Tests/World/TileDataTests.cs`

- [x] `TileData_DefaultEmpty_AllFieldsZero` — verify `TileData.Empty` has all fields at default
- [x] `TileData_Size_FitsExpectedByteCount` — verify `Unsafe.SizeOf<TileData>()` is within expected range (16–20 bytes)
- [x] `TileFlags_NewFlags_HaveCorrectValues` — verify `Forested`, `Interior`, `FastTravel`, `Searchable` bit values
- [x] `TileFlags_CombinedFlags_WorkWithBitwiseOr` — `Walkable | Forested | Searchable` round-trips
- [x] `CoverEdges_AllDirections_AreSingleBits` — verify `North | East | South | West` produces 0b1111
- [x] `LiquidType_AllValues_AreDistinct` — verify enum values are unique
- [x] `TileData_HasWater_WhenWaterLevelAboveHeight` — with and without `LiquidType` set
- [x] `LinearFeature_ConstructsWithNodes` — verify record creation, node list integrity
- [x] `TerrainModifier_Subtypes_AreRecords` — verify `FlattenStrip`, `ChannelCut`, `LevelRect`, `Crater` can be constructed

File: `tests/Oravey2.Tests/World/ChunkDataTests.cs`

- [x] `ChunkData_DefaultMode_IsHeightmap` — new chunks default to `ChunkMode.Heightmap`
- [x] `ChunkData_DefaultLayer_IsSurface` — new chunks default to `MapLayer.Surface`

---

## Verify

```bash
# Everything compiles
dotnet build src/Oravey2.Core
dotnet build src/Oravey2.Windows

# All unit tests pass
dotnet test tests/Oravey2.Tests
```

**User test:** Solution compiles with zero warnings related to new types. Running `dotnet test` shows all new tests green. The game launches (even if the map is empty — terrain rendering is Step 03).
