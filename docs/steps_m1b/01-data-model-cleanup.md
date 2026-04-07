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

- [ ] Change `TileFlags` backing type from `byte` to `ushort`
- [ ] Add flags: `Forested = 16`, `Interior = 32`, `FastTravel = 64`, `Searchable = 128`
- [ ] Verify existing flags (`Walkable`, `Irradiated`, `Burnable`, `Destructible`) are unchanged

### 1.2 — New Enums

- [ ] Create `LiquidType` enum: `None`, `Water`, `Toxic`, `Acid`, `Sewage`, `Lava`, `Oil`, `Frozen`, `Anomaly`
- [ ] Create `CoverEdges` flags enum: `None`, `North = 1`, `East = 2`, `South = 4`, `West = 8`
- [ ] Create `CoverLevel` enum: `None`, `Half`, `Full`
- [ ] Create `ChunkMode` enum: `Heightmap`, `Hybrid`
- [ ] Create `MapLayer` enum: `DeepUnderground`, `Underground`, `Surface`, `Elevated`
- [ ] Create `LinearFeatureType` enum: `Path`, `DirtRoad`, `Road`, `Highway`, `Rail`, `River`, `Stream`, `Canal`, `Pipeline`

### 1.3 — New Records

- [ ] Create `LinearFeatureNode` record: `Vector2 Position`, `float? OverrideHeight`
- [ ] Create `LinearFeature` record: `LinearFeatureType Type`, `string Style`, `float Width`, `IReadOnlyList<LinearFeatureNode> Nodes`
- [ ] Create `TerrainModifier` abstract record
- [ ] Create `FlattenStrip` : `TerrainModifier` — `CentreLine`, `Width`, `TargetHeight`
- [ ] Create `ChannelCut` : `TerrainModifier` — `CentreLine`, `Width`, `Depth`
- [ ] Create `LevelRect` : `TerrainModifier` — `Min`, `Max`, `TargetHeight`
- [ ] Create `Crater` : `TerrainModifier` — `Centre`, `Radius`, `Depth`

### 1.4 — Expand TileData

- [ ] Add `LiquidType Liquid` field
- [ ] Add `CoverEdges HalfCover` field
- [ ] Add `CoverEdges FullCover` field
- [ ] Update `TileData.Empty` default
- [ ] Update `HasWater` logic: when `Liquid == LiquidType.None` and `WaterLevel > HeightLevel`, default to Water for backward compat during transition
- [ ] Update `LegacyTileType` property if still referenced, or remove it

### 1.5 — Expand ChunkData

- [ ] Add `ChunkMode Mode` property (default `Heightmap`)
- [ ] Add `MapLayer Layer` property (default `Surface`)
- [ ] Add `IReadOnlyList<TerrainModifier> TerrainModifiers` property
- [ ] Add `IReadOnlyList<LinearFeature> LinearFeatures` reference (or region-level; stored for chunk access)

### 1.6 — Update TileDataFactory

- [ ] Update factory methods to accept/default new fields
- [ ] Ensure all callers pass valid values

### 1.7 — Fix All Compilation Errors

- [ ] Update every `TileData` construction site in the codebase to supply new default values
- [ ] This is mechanical — search for `new TileData(` and `TileData(` constructor calls

### 1.8 — Remove Old Code

- [ ] Delete old `MapBlueprint` JSON serialisation code in `Blueprint/` folder
- [ ] Delete `PromptBuilder.cs`, `BlueprintCollector.cs` from `Oravey2.MapGen`
- [ ] Delete `ValidateBlueprintTool.cs`, `WriteBlueprintTool.cs`, `CheckOverlapTool.cs`, `CheckWalkabilityTool.cs` from `Oravey2.MapGen/Tools`
- [ ] Delete Portland map data files from `Oravey2.Windows/Maps/portland/`
- [ ] Delete any JSON map loader code
- [ ] Remove `LegacyTileType` property from `TileData` if nothing references it after cleanup

### 1.9 — Unit Tests

File: `tests/Oravey2.Tests/World/TileDataTests.cs`

- [ ] `TileData_DefaultEmpty_AllFieldsZero` — verify `TileData.Empty` has all fields at default
- [ ] `TileData_Size_FitsExpectedByteCount` — verify `Unsafe.SizeOf<TileData>()` is within expected range (16–20 bytes)
- [ ] `TileFlags_NewFlags_HaveCorrectValues` — verify `Forested`, `Interior`, `FastTravel`, `Searchable` bit values
- [ ] `TileFlags_CombinedFlags_WorkWithBitwiseOr` — `Walkable | Forested | Searchable` round-trips
- [ ] `CoverEdges_AllDirections_AreSingleBits` — verify `North | East | South | West` produces 0b1111
- [ ] `LiquidType_AllValues_AreDistinct` — verify enum values are unique
- [ ] `TileData_HasWater_WhenWaterLevelAboveHeight` — with and without `LiquidType` set
- [ ] `LinearFeature_ConstructsWithNodes` — verify record creation, node list integrity
- [ ] `TerrainModifier_Subtypes_AreRecords` — verify `FlattenStrip`, `ChannelCut`, `LevelRect`, `Crater` can be constructed

File: `tests/Oravey2.Tests/World/ChunkDataTests.cs`

- [ ] `ChunkData_DefaultMode_IsHeightmap` — new chunks default to `ChunkMode.Heightmap`
- [ ] `ChunkData_DefaultLayer_IsSurface` — new chunks default to `MapLayer.Surface`

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
