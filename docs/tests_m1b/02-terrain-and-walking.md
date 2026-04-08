# Test Guide: Terrain Rendering & Player Movement

Covers **Steps 3 (Heightmap Renderer), 4 (Linear Features), 5 (Hybrid Overlay), 6 (Liquids), 7 (Trees), 17 (Player Terrain Height)**.

---

## Quick Launch — Terrain Test Scene

The `terrain_test` scenario is a hand-crafted 48×48 tile map designed to showcase all terrain features in one scene:

```powershell
dotnet run --project src/Oravey2.Windows -- --scenario terrain_test
```

---

## Step 3 — Heightmap Terrain

### What to Look For

| # | Check | How to Verify |
|---|-------|---------------|
| 1 | Continuous mesh | Terrain is a smooth heightmap, not a grid of flat tiles |
| 2 | Hills and valleys | Walk toward the upper-left area — a hill rises from the base height |
| 3 | Surface blending | Grass → dirt → rock transitions are smooth, no hard tile edges |
| 4 | Multiple surface types | Grass (green), asphalt (dark grey), concrete (light grey), dirt (brown), sand (tan) visible |
| 5 | No gaps between chunks | Walk across chunk boundaries — terrain is seamless |

### Test Procedure

1. Launch with `terrain_test` scenario
2. Use **W/A/S/D** to walk around the map
3. Use **Q/E** to rotate the camera and inspect terrain from different angles
4. Use **PageUp/PageDown** to zoom in/out — terrain detail should be consistent at all zoom levels

---

## Step 4 — Linear Features (Roads, Rails, Rivers)

### What to Look For

| # | Check | How to Verify |
|---|-------|---------------|
| 1 | Road rendering | A horizontal asphalt strip runs through the middle of the terrain_test map |
| 2 | Road draping | Roads follow terrain height — they go up and down with the hills |
| 3 | Road width | Roads are wider than a single tile, with smooth edges |
| 4 | Rivers | In the generated world, rivers cut through terrain with visible water |

### Test Procedure

1. In `terrain_test`, walk to the middle of the map — find the east-west asphalt road
2. Follow the road from one end to the other — it should drape smoothly on the terrain
3. In a `generated` world, look for rivers connecting to lakes

---

## Step 5 — Hybrid Mode (Towns)

### What to Look For

| # | Check | How to Verify |
|---|-------|---------------|
| 1 | Town rendering | In `terrain_test`, walk to the upper-right area — a walled town appears |
| 2 | Discrete floor tiles | Inside the town, the ground is flat concrete tiles (not heightmap mesh) |
| 3 | Wall structures | Outer walls visible around the town perimeter |
| 4 | Door gaps | South wall has a gap (entrance) you can walk through |
| 5 | Transition | At the town boundary, terrain gradually blends between tile mode and heightmap mode |

### Test Procedure

1. In `terrain_test`, walk northeast to tile area (33-44, 2-13)
2. The town should have wall segments and an interior floor
3. Walk through the door gap in the south wall
4. Walk outside the town boundary — observe the smooth transition back to heightmap terrain

---

## Step 6 — Liquid Rendering

### What to Look For

| # | Check | Where to Find It |
|---|-------|-----------------|
| 1 | Water lake | Bottom-center of `terrain_test` — blue/transparent water surface with shore foam |
| 2 | Lava pool | Small 3×3 glowing orange area with dark crust texture, south of center |
| 3 | Toxic puddle | Small 2×2 pulsing green area near the lava pool |
| 4 | Waterfall | Upper-left area — water at height 12 drops to height 4 at a cliff edge |
| 5 | Water surface animation | Water should shimmer/ripple (requires shader compilation — Step 16) |

### Test Procedure

1. In `terrain_test`, walk south from center to find the water lake
2. Walk near the lake — observe water surface and shore foam
3. Look for the lava and toxic puddles slightly east of the lake
4. Walk northwest to the waterfall area — observe water cascading down the cliff

---

## Step 7 — Vegetation (Trees)

### What to Look For

| # | Check | How to Verify |
|---|-------|---------------|
| 1 | 3D tree meshes | Near the player, trees appear as 3D models with trunks and foliage |
| 2 | Billboard LOD | Zoom out or walk away — distant trees switch to flat billboards facing the camera |
| 3 | Dead trees | In apocalyptic themes, some trees should be bare trunks without leaves |
| 4 | Height matching | Trees sit on the terrain surface, not floating above or buried below |
| 5 | Density variation | Forested areas have more trees, open areas have fewer |

### Test Procedure

1. In `terrain_test` or `generated`, look for green/forested areas
2. Walk close to trees — observe 3D mesh detail
3. Walk away and watch the LOD transition to billboards
4. Rotate camera (**Q/E**) — billboards should always face the camera

---

## Step 17 — Player Terrain Height Snapping

### What to Look For

| # | Check | How to Verify |
|---|-------|---------------|
| 1 | Player follows terrain | Walk over hills — the player capsule smoothly rises and falls |
| 2 | Smooth transitions | No popping or teleporting — height changes are gradual |
| 3 | Cliff blocking | Walk toward a steep cliff — movement should be blocked |
| 4 | Liquid blocking | Walk toward deep water (lake) — player cannot enter if depth > 2 |
| 5 | Shallow wading | Walk into shallow water — player wades at water surface level |
| 6 | Camera follows | Camera smoothly tracks the player's Y position changes |

### Test Procedure — Terrain Test Scene

1. Launch `terrain_test`
2. Walk to the **hill area** (upper-left):
   - Walk up the hill — player Y increases smoothly
   - Walk down — Y decreases smoothly
   - No stuttering or jumping
3. Walk to the **waterfall cliff** (left side, tiles 4-7):
   - Approach the cliff edge from the high side (height 12)
   - Try to walk off the cliff — **movement should be blocked** (height delta = 8 > cliff threshold of 7)
4. Walk toward the **water lake** (bottom-center):
   - Approach from land — deep water should **block** entry
5. Walk across **flat terrain**:
   - Player Y should remain constant, camera steady

### Test Procedure — Generated World

1. Launch with `--scenario generated` or start a New Game
2. Walk around the spawned town area — terrain should be mostly flat (Noord-Holland is flat)
3. Walk toward polder areas or canal embankments — observe slight height variations
4. The player should smoothly follow all terrain height changes
5. Walk along roads — road surface should be at consistent height

---

## Available Test Scenarios

| Scenario | Command | Best For |
|----------|---------|----------|
| `terrain_test` | `--scenario terrain_test` | Steps 3, 4, 5, 6, 7, 17 — all features in one small map |
| `town` | `--scenario town` | Step 5 — town overlay and NPC interaction |
| `wasteland` | `--scenario wasteland` | Step 3, 17 — wasteland terrain with enemies |
| `generated` | `--scenario generated` | Steps 8, 9 — real-world generated world |
| `m0_combat` | `--scenario m0_combat` | Default combat test (flat map) |
