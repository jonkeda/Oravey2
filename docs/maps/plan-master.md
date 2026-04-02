# Map Implementation — Master Plan

> **Date:** 2026-04-02  
> **Input:** docs/maps/01–05, current codebase analysis  
> **Approach:** 7 phases, each independently testable and shippable

---

## Current State

| Component | Code | Tests |
|-----------|------|-------|
| `TileType` enum (6 values) | `TileType.cs` | 7 unit test files (~50 methods) |
| `TileMapData` (2D `TileType[,]` grid) | `TileMapData.cs` | `TileMapDataTests.cs` |
| `ChunkData` (16×16 tiles + entities) | `ChunkData.cs` | `ChunkDataTests.cs` |
| `WorldMapData` (chunk grid) | `WorldMapData.cs` | `WorldMapDataTests.cs` |
| `TileMapRendererScript` (colored cubes) | `TileMapRendererScript.cs` | UI tests (5 classes, ~10 methods) |
| `TownMapBuilder` / `WastelandMapBuilder` | hardcoded C# layouts | 2 builder test files |
| Pathfinder | `TileGridPathfinderTests.cs` | uses `TileMapData` + `IsWalkable` |

**Constraint:** All existing unit tests (~50) and UI tests (~10) must keep passing after every phase.

---

## Phase Overview

```
Phase 1: TileData Model          ← new data types, backward compat shim
Phase 2: JSON Loading             ← load chunks from JSON files
Phase 3: Height System            ← multi-level terrain with slopes/cliffs
Phase 4: Water System             ← water levels, visual water planes
Phase 5: Sub-Tile Rendering       ← 8-4-4 assembly, triplanar, edge jitter
Phase 6: Buildings & Props        ← meshy.ai models on the map
Phase 7: Blueprint Compiler       ← LLM-generated maps → runtime chunks
```

Each phase has its own plan file (`plan-phase-N-*.md`) with:
- Exact files to create/modify
- Test cases to write first (TDD where practical)
- Acceptance criteria
- Verification command

---

## Dependency Graph

```
Phase 1 ──→ Phase 2 ──→ Phase 7
   │            │
   ├──→ Phase 3 ──→ Phase 5
   │            │
   ├──→ Phase 4 ─┘
   │
   └──→ Phase 6
```

- Phases 3, 4, 6 can run in parallel after Phase 1
- Phase 5 needs Phases 3 + 4 (terrain shape before visual polish)
- Phase 7 needs Phase 2 (JSON loading must exist before compiler outputs to it)
- Phase 2 can start right after Phase 1

---

## Phase Summaries

### Phase 1 — TileData Model
**Goal:** Replace `TileType` with rich `TileData` record internally, while keeping backward compatibility.  
**Deliverables:** `SurfaceType` enum, `TileData` record, `TileFlags`, updated `TileMapData`, legacy shim  
**Tests:** 15+ new unit tests, all existing tests still pass  
**Plan:** [plan-phase-1-tiledata.md](plan-phase-1-tiledata.md)

### Phase 2 — JSON Chunk Loading  
**Goal:** Load tile maps from JSON files instead of hardcoded builders.  
**Deliverables:** `MapLoader`, JSON schema, chunk serialization, builder migration  
**Tests:** round-trip serialize/deserialize tests, load-from-file tests  
**Plan:** [plan-phase-2-json-loading.md](plan-phase-2-json-loading.md)

### Phase 3 — Height System  
**Goal:** Tiles at different heights, slopes between them, height-aware pathfinding.  
**Deliverables:** height-based mesh generation, slope/cliff rendering, updated pathfinder cost  
**Tests:** height rendering tests, pathfinder slope cost tests, cliff impassable tests  
**Plan:** [plan-phase-3-height.md](plan-phase-3-height.md)

### Phase 4 — Water System  
**Goal:** Visual water planes at configurable levels, shoreline blending.  
**Deliverables:** water plane rendering, depth-based visuals, shore detection  
**Tests:** water presence tests, shore tile detection, render validation  
**Plan:** [plan-phase-4-water.md](plan-phase-4-water.md)

### Phase 5 — Sub-Tile Rendering  
**Goal:** Break the grid with 8-4-4 sub-tile assembly, triplanar texturing, edge jitter.  
**Deliverables:** sub-tile mesh system, neighbor analysis, quality presets, chunk batching  
**Tests:** neighbor detection tests, sub-tile selection logic tests, quality preset switching  
**Plan:** [plan-phase-5-subtiles.md](plan-phase-5-subtiles.md)

### Phase 6 — Buildings & Props  
**Goal:** Place 3D building models and static props on the tile map.  
**Deliverables:** `BuildingDefinition`, prop placement, footprint walkability, model loading  
**Tests:** footprint blocking tests, prop placement validation, model reference tests  
**Plan:** [plan-phase-6-buildings.md](plan-phase-6-buildings.md)

### Phase 7 — Blueprint Compiler  
**Goal:** LLM-generated MapBlueprint JSON → compiled runtime chunks.  
**Deliverables:** blueprint schema, terrain compiler, road carving, validation  
**Tests:** compiler output tests, round-trip blueprint→chunks→load, validation error tests  
**Plan:** [plan-phase-7-blueprint.md](plan-phase-7-blueprint.md)

---

## Verification After Each Phase

```powershell
# Run after every phase to ensure nothing breaks
dotnet test tests/Oravey2.Tests --verbosity quiet
dotnet test tests/Oravey2.UITests --verbosity quiet
```

Each phase plan includes its own specific test commands for new tests.
