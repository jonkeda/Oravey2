# Phase 5 — Sub-Tile Rendering

> **Status:** Not Started  
> **Depends on:** Phase 1, Phase 3 (height meshes), Phase 4 (water/shore data)  
> **Result:** Tiles look organic, grid pattern eliminated

---

## Goal

Implement the 8-4-4 sub-tile assembly, triplanar texturing, edge jitter, and quality presets. This is the visual breakthrough phase.

---

## Steps

### Step 5.1 — Neighbor analysis engine

**File:** `src/Oravey2.Core/World/Rendering/NeighborAnalyzer.cs` (new)

Pure logic — no Stride dependency. Given a tile grid and a position, determine the 8 neighbors' surface types.

```
NeighborAnalyzer (static)
├── GetNeighbors(TileMapData, x, y) → NeighborInfo
├── GetQuadrantShape(NeighborInfo, Quadrant) → SubTileShape
```

```csharp
public readonly record struct NeighborInfo(
    SurfaceType Center,
    SurfaceType N, SurfaceType NE, SurfaceType E, SurfaceType SE,
    SurfaceType S, SurfaceType SW, SurfaceType W, SurfaceType NW);

public enum Quadrant { NE, SE, SW, NW }

public enum SubTileShape { Fill, Edge, OuterCorner, InnerCorner }
```

Decision logic per quadrant (e.g., NE quadrant checks N, NE, E neighbors):
- Both cardinals different → OuterCorner
- One cardinal different → Edge (rotated to face different neighbor)
- Both cardinals same, diagonal different → InnerCorner
- All same → Fill

**Tests:** `NeighborAnalyzerTests.cs` (critical — 20+ tests)
- Surrounded by same type → all 4 quadrants are Fill
- North neighbor different → NE and NW quadrants are Edge
- Corner exposed (N and E different) → NE is OuterCorner
- Only diagonal different (NE different, N and E same) → NE is InnerCorner
- Edge tile (map boundary) treats out-of-bounds as different type
- Mixed: one side different, diagonal different on other side → correct per-quadrant
- All 4 quadrants computed independently
- Multiple surface type transitions at same tile

---

### Step 5.2 — Sub-tile mesh selection

**File:** `src/Oravey2.Core/World/Rendering/SubTileSelector.cs` (new)

Given the 4 quadrant shapes, compute the mesh ID and rotation for each sub-tile:

```
SubTileSelector (static)
├── GetSubTileConfig(SubTileShape, Quadrant) → SubTileConfig
```

```csharp
public readonly record struct SubTileConfig(
    SubTileShape Shape,
    int RotationDegrees,   // 0, 90, 180, 270
    SurfaceType Surface);
```

Rotation mapping:
| Quadrant | Base rotation |
|----------|--------------|
| NE | 0° |
| SE | 90° |
| SW | 180° |
| NW | 270° |

Edge sub-tiles get additional rotation to face the correct direction.

**Tests:** `SubTileSelectorTests.cs`
- NE quadrant, Fill → rotation 0°
- SE quadrant, Fill → rotation 90°
- NE quadrant, Edge facing North → correct rotation
- SW quadrant, OuterCorner → rotation 180°

---

### Step 5.3 — Quality settings system

**File:** `src/Oravey2.Core/World/Rendering/QualitySettings.cs` (new)

```csharp
public sealed class QualitySettings
{
    public QualityPreset Preset { get; set; } = QualityPreset.Medium;
    public bool SubTileAssembly { get; set; } = true;
    public bool EdgeJitter { get; set; } = true;
    public float DetailDensity { get; set; } = 0.5f;
    public float DetailRange { get; set; } = 10f;
    public int LodRings { get; set; } = 2;

    public static QualitySettings FromPreset(QualityPreset preset) => preset switch
    {
        QualityPreset.Low => new() { SubTileAssembly = false, EdgeJitter = false, DetailDensity = 0, DetailRange = 0, LodRings = 1 },
        QualityPreset.Medium => new() { SubTileAssembly = true, EdgeJitter = true, DetailDensity = 0.5f, DetailRange = 10, LodRings = 2 },
        QualityPreset.High => new() { SubTileAssembly = true, EdgeJitter = true, DetailDensity = 1.0f, DetailRange = 20, LodRings = 3 },
        _ => new()
    };
}

public enum QualityPreset { Low, Medium, High }
```

**Tests:** `QualitySettingsTests.cs`
- Low preset: SubTileAssembly off, DetailDensity 0
- Medium preset: SubTileAssembly on, DetailDensity 0.5
- High preset: everything on, DetailDensity 1.0

---

### Step 5.4 — Edge jitter computation

**File:** `src/Oravey2.Core/World/Rendering/EdgeJitter.cs` (new)

Pure math — compute vertex displacement for tile border vertices.

```
EdgeJitter (static)
├── GetDisplacement(float worldX, float worldZ, byte variantSeed) → Vector2
│     uses simple hash-based noise, amplitude 0.05–0.1
├── IsBorderVertex(TileMapData, x, y, vertexLocalX, vertexLocalZ) → bool
│     true if vertex is on an edge between two different surface types
```

Only border vertices get displaced. Interior and corner vertices stay fixed.

**Tests:** `EdgeJitterTests.cs`
- Same position + same seed → same displacement (deterministic)
- Different seeds → different displacement
- Displacement magnitude within 0.0–0.1 range
- Interior vertex → not displaced
- Border vertex between different types → displaced
- Border vertex between same types → not displaced

---

### Step 5.5 — Chunk mesh batcher (Low quality fast path)

**File:** `src/Oravey2.Core/World/Rendering/ChunkMeshBatcher.cs` (new)

For Low quality preset: merge all tiles of the same surface type into a single mesh per chunk.

```
ChunkMeshBatcher
├── BatchChunk(ChunkData, QualitySettings) → BatchedChunkMeshes
```

```csharp
public sealed class BatchedChunkMeshes
{
    // Key: SurfaceType, Value: list of tile positions in that batch
    public Dictionary<SurfaceType, List<(int X, int Y, TileData Data)>> Batches { get; }
    public int TotalDrawCalls => Batches.Count;  // 1 per surface type
}
```

**Tests:** `ChunkMeshBatcherTests.cs`
- Chunk with all Ground → 1 batch, 256 tiles
- Chunk with Ground + Road + Water → 3 batches
- Empty tiles not included in any batch
- Draw call count matches unique surface type count
- Low preset uses batching, Medium/High produce sub-tile data instead

---

### Step 5.6 — Update TileMapRendererScript with sub-tile path

**File:** `src/Oravey2.Core/World/TileMapRendererScript.cs` (modify)

Add a rendering path controlled by `QualitySettings`:

```
BuildMap():
  if (quality.SubTileAssembly):
    for each tile:
      analyze neighbors → 4 SubTileConfigs
      create 4 child entities with correct mesh + rotation
  else:
    for each batch in ChunkMeshBatcher.BatchChunk():
      create 1 merged mesh entity per surface type
```

The existing colored-cube path becomes the Low quality fallback (batched).

**Tests:** UI tests — **no changes**. Visual output tested manually. Logic tested via NeighborAnalyzer and SubTileSelector unit tests.

---

## Acceptance Criteria

| # | Criteria | How to Verify |
|---|---------|--------------|
| 1 | NeighborAnalyzer correctly identifies all 4 quadrant shapes for any tile configuration | 20+ unit tests |
| 2 | SubTileSelector produces correct rotation per quadrant | Unit tests |
| 3 | EdgeJitter is deterministic and bounded | Unit tests |
| 4 | ChunkMeshBatcher reduces draw calls to ~4 per chunk on Low quality | Unit tests |
| 5 | QualitySettings presets configure all rendering options | Unit tests |
| 6 | Renderer uses sub-tiles on Medium/High, batching on Low | Code path test |
| 7 | All existing tests pass | `dotnet test` |
| 8 | 30+ new unit tests | Test count |

## Verification

```powershell
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Rendering" --verbosity normal
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Neighbor" --verbosity normal
dotnet test tests/Oravey2.Tests --verbosity quiet
```
