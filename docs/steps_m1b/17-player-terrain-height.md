# Step 17 — Player Terrain Height Snapping

**Work streams:** WS-Player (Player Movement)
**Depends on:** Step 03 (heightmap renderer), Step 05 (GetSurfaceHeight)
**User-testable result:** Launch the game → the player capsule rises and falls with the terrain as it walks over hills, down into valleys, and across elevation changes.

---

## Goals

1. Snap the player entity Y position to the heightmap surface every frame.
2. Smooth vertical transitions so the player doesn't teleport between heights.
3. Keep the camera following correctly as the player moves up and down.
4. Prevent the player from walking into deep liquid (optional wading/blocking).

---

## Problem

`PlayerMovementScript.Update()` moves the player in XZ only:

```csharp
var worldDir = new Vector3(worldX, 0f, worldZ);  // ← Y always 0
```

The player entity is created at `Y = 0.5` and never changes height. On the heightmap terrain (which has hills from height 4–12), the player floats above flat areas and clips through elevated terrain.

---

## Tasks

### 17.1 — Add Height Query to PlayerMovementScript

- [ ] Add a reference to the `WorldMapData` (or height query interface) on `PlayerMovementScript`
- [ ] After computing `newPos.X` and `newPos.Z`, query terrain height:
  ```csharp
  float terrainY = GetTerrainHeightAtWorldPos(newPos.X, newPos.Z);
  newPos.Y = terrainY + PlayerHeightOffset;
  ```
- [ ] `PlayerHeightOffset` = half the capsule height (0.5f) so the base sits on the ground

### 17.2 — World-to-Chunk Height Query

- [ ] Create `World/Terrain/TerrainHeightQuery.cs`
- [ ] Convert world-space XZ → chunk index + chunk-local XZ
- [ ] Account for the world-origin centring offset used in `HeightmapTerrainScript`:
  ```
  worldX = chunkLocalX + chunkX * chunkWorldSize - halfWorldX
  ```
- [ ] Build and cache the height grids per chunk (reuse from `ChunkTerrainBuilder` or sample on demand)
- [ ] Expose: `float GetHeight(float worldX, float worldZ)`
- [ ] Delegates to `HeightmapMeshGenerator.GetSurfaceHeight()` with the correct chunk's height grid

### 17.3 — Smooth Vertical Movement

- [ ] Don't snap Y instantly — lerp toward the target height:
  ```csharp
  float targetY = terrainY + PlayerHeightOffset;
  float currentY = Entity.Transform.Position.Y;
  newPos.Y = MathHelper.Lerp(currentY, targetY, 1f - MathF.Exp(-HeightSmoothing * dt));
  ```
- [ ] `HeightSmoothing` = 15f (fast enough to feel grounded, slow enough to avoid jitter)
- [ ] For large height changes (teleport, spawn), snap immediately

### 17.4 — Cliff / Steep Slope Blocking

- [ ] Before accepting vertical movement, check if the height delta exceeds a threshold
- [ ] Reuse `HeightHelper.GetSlopeType()` — block movement if slope is `SlopeType.Cliff`
- [ ] This prevents the player from walking straight up a cliff face
- [ ] The existing tile-based collision (`IsWalkableBBox`) handles horizontal blocking; this adds vertical blocking

### 17.5 — Liquid Depth Blocking

- [ ] Query `TileData.HasWater` and `TileData.WaterDepth` at the player's tile
- [ ] If `WaterDepth > 2` (deep liquid), block movement into that tile
- [ ] If `WaterDepth` is 1–2 (shallow), allow movement but keep player Y at liquid surface
- [ ] Optional: slow movement speed in shallow liquid (0.5× multiplier)

### 17.6 — Camera Height Follow

- [ ] `TacticalCameraScript` already follows `Target.Transform.Position` including Y
- [ ] Verify that the spherical offset calculation (`followTarget + offset`) works with varying Y
- [ ] The camera `followTarget` lerp will naturally smooth the camera Y movement
- [ ] No code changes expected — just verify visually

### 17.7 — Initial Spawn Height

- [ ] In `ScenarioLoader.CreatePlayer()`, query terrain height at spawn position:
  ```csharp
  playerEntity.Transform.Position = new Vector3(spawnX, terrainY + 0.5f, spawnZ);
  ```
- [ ] For `terrain_test` scenario, spawn at the map centre where height is known (height 4 = Y 1.0)
- [ ] Avoid spawning inside terrain on first frame

### 17.8 — Unit Tests

File: `tests/Oravey2.Tests/Terrain/TerrainHeightQueryTests.cs`

- [ ] `FlatTerrain_ReturnsConstantHeight` — query across flat map returns same Y everywhere
- [ ] `HillyTerrain_ReturnsHigherOnHill` — query at hill centre returns higher Y than edge
- [ ] `ChunkBoundary_ContinuousHeight` — query at chunk seam returns consistent value
- [ ] `OutOfBounds_ClampsToEdge` — query outside map returns nearest edge height

File: `tests/Oravey2.Tests/Player/PlayerHeightSnappingTests.cs`

- [ ] `PlayerMovement_FlatTerrain_MaintainsHeight` — player Y stays at terrainY + offset on flat ground
- [ ] `PlayerMovement_UpHill_YIncreases` — moving toward higher terrain increases player Y
- [ ] `PlayerMovement_CliffEdge_BlocksMovement` — movement into cliff returns to old position

### 17.9 — UI Tests

File: `tests/Oravey2.UITests/Player/TerrainWalkingTests.cs`

- [ ] `Player_WalksOnTerrain_NotFloating` — screenshot shows player capsule touching terrain surface
- [ ] `Player_WalksOverHill_YChanges` — player screen position shifts vertically when walking over hill area

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~TerrainHeightQuery|PlayerHeight"
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~TerrainWalking"
```

**User test:** Launch `terrain_test` scenario. Walk the player (WASD) toward the hills in the top-left area. The capsule visibly rises as it climbs the hill and descends when walking back down. The camera follows smoothly. The player cannot walk up the steep cliff at the waterfall edge.
