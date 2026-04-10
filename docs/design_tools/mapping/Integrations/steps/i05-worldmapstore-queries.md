# Step i05 — WorldMapStore Query Extensions

**Design doc:** 03
**Depends on:** None (extends existing `WorldMapStore`)
**Deliverable:** Three new query methods on `WorldMapStore` needed by
`RegionLoader` and `RegionSelectorScript`.

---

## Goal

The existing `WorldMapStore` can insert data and read chunks by
coordinate. The unified loader needs three more queries: look up a
region by name, get all entity spawns for a region, and list all
regions.

---

## Tasks

### i05.1 — `GetRegionByName`

File: `src/Oravey2.Core/Data/WorldMapStore.cs`

- [ ] Add method:
  ```csharp
  public RegionInfo? GetRegionByName(string name)
  ```
- [ ] SQL: `SELECT * FROM region WHERE name = $name LIMIT 1`
- [ ] Returns `null` if not found

### i05.2 — `GetAllRegions`

- [ ] Add method:
  ```csharp
  public IReadOnlyList<RegionInfo> GetAllRegions()
  ```
- [ ] SQL: `SELECT * FROM region ORDER BY name`
- [ ] Returns empty list if no regions exist

### i05.3 — `GetEntitySpawnsForRegion`

- [ ] Add method:
  ```csharp
  public IReadOnlyList<(long ChunkId, int ChunkX, int ChunkY, EntitySpawnInfo Spawn)>
      GetEntitySpawnsForRegion(long regionId)
  ```
- [ ] SQL: join `chunk` and `entity_spawn` on `chunk_id` where
  `chunk.region_id = $regionId`
- [ ] Returns chunk coordinates alongside each spawn so the loader
  can compute world positions

### i05.4 — `RegionInfo` record

File: `src/Oravey2.Core/Data/WorldMapStore.cs` (nested) or new file

- [ ] Define if it doesn't already exist:
  ```csharp
  public record RegionInfo(
      long Id, long ContinentId, string Name,
      int GridX, int GridY,
      string? Biome, int BaseHeight,
      string? Description);
  ```
- [ ] Match the `region` table columns

### i05.5 — Tests

File: `tests/Oravey2.Tests/Data/WorldMapStoreQueryTests.cs`

- [ ] `GetRegionByName_Exists_ReturnsRegion` — insert region → query
  by name → fields match
- [ ] `GetRegionByName_NotFound_ReturnsNull`
- [ ] `GetAllRegions_MultipleRegions_ReturnsAll` — insert 3 → returns 3
  ordered by name
- [ ] `GetAllRegions_Empty_ReturnsEmptyList`
- [ ] `GetEntitySpawnsForRegion_ReturnsSpawnsWithChunkCoords` —
  insert region + chunk + 2 spawns → returns both with correct
  chunk X/Y
- [ ] `GetEntitySpawnsForRegion_NoSpawns_ReturnsEmpty`
- [ ] Build + all tests pass

---

## Files changed

| File | Action |
|------|--------|
| `WorldMapStore.cs` | **Modify** — add 3 methods + `RegionInfo` |
| `WorldMapStoreQueryTests.cs` | **New** |
