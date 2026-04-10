# Integration Steps — Overview

Development steps for the integration refactors (`01`–`04`). Each
step is a self-contained unit of work with clear inputs, outputs, and
tests.

## Numbering

Steps are prefixed `i` to distinguish from existing `steps_tools`
and `steps_m1b` numbering.

## Step list

| Step | Title | Design doc | Deliverables |
|------|-------|-----------|-------------|
| i01 | [Unify LinearFeatureType enum](i01-unify-linear-feature-type.md) | QA-review §6 | One enum, delete `RoadClass` |
| i02 | [Fix surface value mismatch](i02-fix-surface-values.md) | QA-review §7 | MapGen condenser uses `SurfaceType` directly |
| i03 | [Add missing DTO fields](i03-add-missing-dto-fields.md) | Compat review | `InteriorChunkId`, `Footprint`, `Style` |
| i04 | [ChunkSplitter utility](i04-chunk-splitter.md) | 01 phase 4, 02 | Shared `ChunkSplitter` in Core |
| i05 | [WorldMapStore query extensions](i05-worldmapstore-queries.md) | 03 | `GetRegionByName`, `GetEntitySpawnsForRegion`, `GetAllRegions` |
| i06 | [WorldDbSeeder — debug.db](i06-world-db-seeder.md) | 01, QA §3 | Seeds 5 built-in scenarios into `debug.db` |
| i07 | [IEntitySpawnerFactory + spawners](i07-entity-spawner-factories.md) | 03 | Interface + NPC, enemy, zone_exit, building, prop factories |
| i08 | [RegionLoader](i08-region-loader.md) | 03 | Data-driven loader, replaces ScenarioLoader switch |
| i09 | [ContentPackImporter — game-side](i09-content-pack-importer.md) | 02, QA §8 | Import content pack JSON into `world.db` |
| i10 | [ContentPackExporter — tool-side](i10-content-pack-exporter.md) | 02 | Export pipeline output to `world.db` |
| i11 | [RegionSelectorScript](i11-region-selector.md) | 04 | Region picker from DB, debug tag, import button |
| i12 | [Zone transitions between regions](i12-zone-transitions.md) | 04 | ZoneManager refactor, save `current_region` |
| i13 | [Delete hardcoded scenarios](i13-delete-hardcoded.md) | 01 phase 5 | Remove LoadTown, LoadWasteland, etc. |

## Dependency graph

```
i01 ─┐
i02 ─┤
i03 ─┤─→ i04 ─→ i06 ─→ i08 ─→ i13
     │         ↗      ↗
i05 ─┘───→ i07 ┘
                      i08 ─→ i11 ─→ i12
     i04 ─→ i09 ─→ i10
```

Steps i01–i05 can be done in parallel (no dependencies between them).
i06 needs i04+i05. i07 needs i05. i08 needs i06+i07. i09 needs i04.
i10 needs i09. i11 needs i08. i12 needs i11. i13 is last.
