# Test Guide: Navigation, Zoom & Minimap

Covers **Steps 10 (Chunk Streaming), 11 (Zoom Levels), 12 (Globe View), 14 (Minimap)**.

---

## Step 10 — Chunk Streaming

Chunk streaming loads terrain dynamically as the player moves, so the world can be infinite without loading screens.

### Test Procedure

1. Launch with `--scenario generated` (requires a generated `world.db`)
2. Walk in one direction (hold **W**) for 30+ seconds
3. Observe new terrain appearing ahead of you seamlessly

### What to Verify

| # | Check | Expected |
|---|-------|----------|
| 1 | No loading screens | Terrain streams in as you walk — no pauses or freezes |
| 2 | Seamless boundaries | No visible seams or gaps between chunks |
| 3 | Backtrack loads from cache | Turn around and walk back — previously seen terrain loads instantly |
| 4 | Continuous features | Roads and rivers continue across chunk boundaries without breaks |
| 5 | No memory explosion | Walking for several minutes doesn't cause the game to run out of memory (LRU cache evicts old chunks) |

---

## Step 11 — Multi-Scale Zoom (L1–L3)

The game supports three zoom levels that transition smoothly as you zoom out.

### Test Procedure

1. Launch with `--scenario generated` or `terrain_test`
2. Start at default zoom (L1 — local terrain view)
3. Use **PageDown** or **mouse scroll down** to zoom out progressively

### Zoom Levels

| Level | View | Time Scale | Visual |
|-------|------|------------|--------|
| L1 — Local | Detailed terrain, player visible | 1× (real-time) | Heightmap mesh, trees, buildings |
| L2 — Regional | Biome-colored overview, town labels | 60× | Simplified terrain, town names visible |
| L3 — Continental | Strategic map with city dots | 1440× (1 day/min) | Flat map, faction territories, major cities |

### What to Verify

| # | Check | Expected |
|---|-------|----------|
| 1 | Smooth zoom transition | No popping or sudden changes between zoom levels |
| 2 | L1 detail | At closest zoom, full terrain detail with trees and structures |
| 3 | L2 labels | At mid zoom, town names appear as floating labels |
| 4 | L3 strategic | At furthest zoom, map simplifies to colored regions with city dots |
| 5 | Time scaling | Watch the clock in the HUD — time speeds up as you zoom out |
| 6 | Zoom back in | Zoom in from L3 → L2 → L1 — terrain detail returns smoothly |

---

## Step 12 — Globe View (L4)

Zooming past L3 enters an orbital globe view showing the full planet.

### Test Procedure

1. From L3, continue zooming out with **PageDown** or **mouse scroll**
2. The view should transition to a 3D globe

### What to Verify

| # | Check | Expected |
|---|-------|----------|
| 1 | Globe appears | A spherical planet with biome-colored continents |
| 2 | Rotation | Click and drag to rotate the globe |
| 3 | Region selection | Click on a region to see a travel dialog with distance and estimated travel time |
| 4 | Zoom back | Zoom in to return to L3 → L2 → L1 at the selected location |

---

## Step 14 — Minimap & HUD

The minimap provides a top-down overview in the corner of the screen.

### Test Procedure

1. Launch any scenario
2. Look at the top-right corner of the screen for the minimap
3. Press **M** to toggle minimap size (small ↔ large)

### What to Verify

| # | Check | Expected |
|---|-------|----------|
| 1 | Minimap visible | Small minimap appears in the corner showing nearby terrain |
| 2 | Player arrow | A directional arrow shows the player's position and facing |
| 3 | POI icons | Points of interest (towns, buildings) shown as icons on minimap |
| 4 | Fog of war | Unexplored areas are darkened/hidden on the minimap |
| 5 | Click to jump | Click a spot on the minimap — camera jumps to that location |
| 6 | Toggle size | Press **M** — minimap expands to a larger view, press again to shrink |
| 7 | Compass strip | Top of screen shows N/E/S/W compass heading |
| 8 | HUD info | Location name, game time, and weather shown in HUD |

---

## Combined Test: Walk Across the World

This exercise tests Steps 10, 11, 14, and 17 together:

1. Start a New Game or load `--scenario generated`
2. Note your starting location on the minimap
3. Pick a direction and walk (hold **W**) for 2+ minutes
4. **While walking, verify:**
   - Terrain streams in ahead (Step 10)
   - Player follows terrain height smoothly (Step 17)
   - Minimap updates with explored terrain (Step 14)
   - No loading screens or freezes
5. Zoom out to L2 (**PageDown**) — you should see how far you've walked
6. Zoom to L3 — see the broader continent
7. Zoom back to L1 and continue walking
