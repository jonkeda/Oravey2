# RCA: Enemies Outside Map & Player Can Walk Off Edge

## Symptoms

1. The three red capsule enemies are not visible on the tile map — they are placed outside the rendered area
2. The play area feels too small for the fight
3. The player can walk freely off the edge of the tile map with no boundary enforcement

## Investigation

### Tile map coordinate system

`TileMapRendererScript` centres the map around world origin using:

```csharp
var centerX = (x - MapData.Width / 2f + 0.5f) * TileSize;
var centerZ = (y - MapData.Height / 2f + 0.5f) * TileSize;
```

For a 16×16 map with `TileSize = 1.0`:

| Tile index | World X/Z |
|-----------|-----------|
| (0, 0) | (-7.5, -7.5) |
| (1, 1) | (-6.5, -6.5) — first walkable tile (inside border wall) |
| (8, 8) | (0.5, 0.5) — near centre |
| (14, 14) | (6.5, 6.5) — last walkable tile |
| (15, 15) | (7.5, 7.5) — border wall |

**Walkable world bounds: approximately (-6.5, -6.5) to (6.5, 6.5).**

### Root Cause 1: Enemy positions use tile indices as world coordinates

Phase_A design placed enemies at tile indices `(10,10)`, `(5,12)`, `(12,5)` but wrote them directly as world positions:

```csharp
("enemy_1", new Vector3(10, 0.5f, 10)),   // world (10, 0.5, 10) — 2.5 units outside map edge
("enemy_2", new Vector3(5, 0.5f, 12)),    // world (5, 0.5, 12) — 4.5 units outside map edge
("enemy_3", new Vector3(12, 0.5f, 5)),    // world (12, 0.5, 5) — 4.5 units outside map edge
```

The correct world positions for those tile indices would be:
- Tile (10, 10) → world (2.5, 0.5, 2.5)
- Tile (5, 12) → world (-2.5, 0.5, 4.5)
- Tile (12, 5) → world (4.5, 0.5, -2.5)

### Root Cause 2: No player movement boundary

`PlayerMovementScript.Update()` applies movement without any bounds check:

```csharp
Entity.Transform.Position += worldDir * MoveSpeed * dt;
```

There is no clamping, tile-occupancy check, or collision detection. The player can walk infinitely in any direction.

### Root Cause 3: Map too small for combat gameplay

A 16×16 map produces only a 14×14 walkable interior (world units). Combined with a trigger radius of 8, combat starts almost immediately. Even after correcting enemy positions there's little room to manoeuvre. The map should be enlarged to 32×32 (30×30 walkable interior) to give a meaningful exploration area before and during combat.

## Fix Plan

### Fix 1: Increase map size to 32×32 (Program.cs)

```csharp
var mapData = TileMapData.CreateDefault(32, 32);
```

For a 32×32 map, walkable bounds become (-14.5, -14.5) to (14.5, 14.5).

### Fix 2: Correct enemy world positions (Program.cs)

Place enemies inside the larger map with good spread, using proper world coordinates:

```csharp
("enemy_1", new Vector3(8f, 0.5f, 8f)),
("enemy_2", new Vector3(-6f, 0.5f, 10f)),
("enemy_3", new Vector3(10f, 0.5f, -6f)),
```

### Fix 3: Reduce trigger radius (Program.cs)

Reduce `EncounterTriggerScript.TriggerRadius` from 8 to 5 so the player has room to explore.

### Fix 4: Clamp player position to walkable bounds (PlayerMovementScript)

After applying movement, clamp the player's position to within the map's walkable area. For M0, a simple world-space AABB clamp is sufficient (Phase B will add per-tile wall collision):

```csharp
// Clamp to walkable map bounds (inside border walls, 32×32 map)
var pos = Entity.Transform.Position;
pos.X = Math.Clamp(pos.X, -14.5f, 14.5f);
pos.Z = Math.Clamp(pos.Z, -14.5f, 14.5f);
Entity.Transform.Position = pos;
```

## Affected Files

| File | Change |
|------|--------|
| `src/Oravey2.Windows/Program.cs` | Increase map to 32×32, fix enemy positions, reduce trigger radius |
| `src/Oravey2.Core/Player/PlayerMovementScript.cs` | Add boundary clamping |
