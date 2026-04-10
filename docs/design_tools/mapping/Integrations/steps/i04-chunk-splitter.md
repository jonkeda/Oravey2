# Step i04 — ChunkSplitter Utility

**Design doc:** 01 phase 4, 02
**Depends on:** None (uses existing `TileMapData`, `TileData`,
`TileDataSerializer`)
**Deliverable:** `ChunkSplitter` in `Oravey2.Core.Data` that splits
any-size `TileMapData` into 16×16 chunk blobs.

---

## Goal

Create a shared utility that converts a `TileMapData` of arbitrary
width×height into a list of 16×16 tile grids ready for
`WorldMapStore.InsertChunk()`. Used by both the `WorldDbSeeder`
(step i06) and the `ContentPackImporter` (step i09).

---

## Tasks

### i04.1 — Create `ChunkSplitter`

File: `src/Oravey2.Core/Data/ChunkSplitter.cs`

- [ ] Static class, single method:
  ```csharp
  public static class ChunkSplitter
  {
      public static List<(int ChunkX, int ChunkY, TileData[,] Tiles)>
          Split(TileMapData mapData)
      {
          var result = new List<(int, int, TileData[,])>();
          int chunksW = (mapData.Width + 15) / 16;
          int chunksH = (mapData.Height + 15) / 16;
          for (int cy = 0; cy < chunksH; cy++)
              for (int cx = 0; cx < chunksW; cx++)
              {
                  var tiles = new TileData[16, 16];
                  for (int ly = 0; ly < 16; ly++)
                      for (int lx = 0; lx < 16; lx++)
                      {
                          int wx = cx * 16 + lx;
                          int wy = cy * 16 + ly;
                          tiles[ly, lx] =
                              (wx < mapData.Width && wy < mapData.Height)
                                  ? mapData.GetTileData(wx, wy)
                                  : default;
                      }
                  result.Add((cx, cy, tiles));
              }
          return result;
      }
  }
  ```
- [ ] Edge chunks padded with `default(TileData)` for tiles outside
  the map boundary

### i04.2 — Overload: split and serialize to byte[]

- [ ] Add convenience overload that returns serialized blobs:
  ```csharp
  public static List<(int ChunkX, int ChunkY, byte[] TileBlob)>
      SplitAndSerialize(TileMapData mapData)
  {
      return Split(mapData)
          .Select(c => (c.ChunkX, c.ChunkY,
              TileDataSerializer.SerializeTileGrid(c.Tiles)))
          .ToList();
  }
  ```

### i04.3 — Tests

File: `tests/Oravey2.Tests/Data/ChunkSplitterTests.cs`

- [ ] `Split_16x16_ReturnsSingleChunk` — 16×16 map → 1 chunk at (0,0)
- [ ] `Split_32x32_ReturnsFourChunks` — 32×32 → 4 chunks at (0,0), (1,0), (0,1), (1,1)
- [ ] `Split_Rectangular_CorrectChunkCount` — 48×32 → 3×2 = 6 chunks
- [ ] `Split_NonAligned_PadsEdge` — 20×20 → 2×2 = 4 chunks, edge
  tiles are `default(TileData)`
- [ ] `Split_Empty_ReturnsEmpty` — 0×0 → empty list
- [ ] `SplitAndSerialize_RoundTrips` — split → serialize → deserialize
  → tiles match original
- [ ] Build + all tests pass

---

## Files changed

| File | Action |
|------|--------|
| `ChunkSplitter.cs` | **New** in `Oravey2.Core/Data/` |
| `ChunkSplitterTests.cs` | **New** in `tests/Oravey2.Tests/Data/` |
