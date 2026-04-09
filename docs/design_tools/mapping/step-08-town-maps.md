# Step 08 — Town Map Condensation

## Goal

Generate compact game-scale tile maps for each designed town. Real-world OSM
geometry is condensed into a playable grid with placed buildings and props.

## Deliverables

### 8.1 `TownMapCondenser` service (new, in `Oravey2.MapGen`)

```csharp
public sealed class TownMapCondenser
{
    public TownMapResult Condense(
        CuratedTown town,
        TownDesign design,
        RegionTemplate region,     // for OSM road/building geometry near this town
        int seed);
}
```

Algorithm:
1. Extract OSM features within town boundary (roads, building footprints)
2. Scale real-world distances to game tile grid (configurable scale factor)
3. Snap roads to 1-tile-wide paths on grid
4. Place landmark building at town centre
5. Place key locations along main roads
6. Fill remaining building footprints with generic buildings from catalog
7. Place props (vehicles, barrels, etc.) with randomized scatter
8. Define zones (biome, threat, fast-travel) based on design hazards

### 8.2 Output records

```csharp
public sealed record TownMapResult(
    TownLayout Layout,
    List<PlacedBuilding> Buildings,
    List<PlacedProp> Props,
    List<TownZone> Zones);

public sealed record TownLayout(
    int Width,
    int Height,
    int[][] Surface);             // surface type IDs per tile

public sealed record PlacedBuilding(
    string Id,
    string Name,
    string MeshAsset,             // "meshes/{assetId}.glb" — may be placeholder until step 09
    string SizeCategory,
    int[][] Footprint,
    int Floors,
    float Condition,
    TilePlacement Placement);

public sealed record PlacedProp(
    string Id,
    string MeshAsset,
    TilePlacement Placement,
    float Rotation,
    float Scale,
    bool BlocksWalkability);

public sealed record TownZone(
    string Id,
    string Name,
    int Biome,
    float RadiationLevel,
    int EnemyDifficultyTier,
    bool IsFastTravelTarget,
    int ChunkStartX, int ChunkStartY,
    int ChunkEndX, int ChunkEndY);

public sealed record TilePlacement(
    int ChunkX, int ChunkY,
    int LocalTileX, int LocalTileY);
```

### 8.3 `TownMapsStepView` / `TownMapsStepViewModel`

- **Left panel — town list**: towns with designs, showing map status
- **Right panel** for selected town:
  - **[Generate Map]** → runs `TownMapCondenser.Condense()`
  - Stats line: `24×18 tiles · 6 buildings · 14 props · 2 zones`
  - **[Show Preview]** → on-demand tile map rendering (lazy canvas)
  - **[Accept]** → saves files
  - **[Re-generate]** → new seed
  - **[Edit JSON]** → raw editor
- **Batch bar**: **[Generate All Maps]**

### 8.4 File output per town

```
content/Oravey2.Apocalyptic.NL.NH/towns/{gameName}/layout.json
content/Oravey2.Apocalyptic.NL.NH/towns/{gameName}/buildings.json
content/Oravey2.Apocalyptic.NL.NH/towns/{gameName}/props.json
content/Oravey2.Apocalyptic.NL.NH/towns/{gameName}/zones.json
```

JSON format matches existing portland map format (see
`content/Oravey2.Apocalyptic/maps/portland/`).

### 8.5 Overworld files

After all town maps are generated, also produce:

```
content/Oravey2.Apocalyptic.NL.NH/overworld/world.json       — region dimensions, player start
content/Oravey2.Apocalyptic.NL.NH/overworld/roads.json        — inter-town road network
content/Oravey2.Apocalyptic.NL.NH/overworld/water.json        — rivers, lakes, sea polygons
```

These are derived from the `RegionTemplate` road/water data, filtered to
only include features connecting the curated towns.

### 8.6 Tests

- `TownMapCondenser.Condense` — small synthetic input, verify grid dimensions,
  building placement within bounds, all key locations placed
- ViewModel: generate/accept/batch flow

## Dependencies

- Step 07 (town designs)
- Step 05 (parsed `RegionTemplate` for OSM geometry)
- Existing: `BuildingFootprint`, `SpatialUtils`

## Estimated scope

- New files: `TownMapCondenser.cs`, output records, view + VM
- This is the most algorithmically complex step
