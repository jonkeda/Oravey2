# Integration Refactors — Overview

## Context

The MapGen pipeline (steps 01–10) produces a content pack with town
maps, overworld data, mesh assets, and metadata. The game currently
cannot load this output (see `pipeline-game-loading-review.md`). The
world-creation-flow design describes a New Game experience that picks
a template and generates into `world.db`.

These refactors unify both paths so:

1. **All scenarios are data-driven** — no hardcoded `LoadTown()` /
   `LoadWasteland()` methods
2. The **pipeline tool can export to `world.db`** so generated regions
   are loadable by the game
3. The **debug scenario selector** loads scenarios from the database
   instead of calling hardcoded methods
4. **Players can select any region** to start in, and later move between
   regions
5. Backward compatibility is not a concern — this is a clean break

## Documents

| # | Document | Scope |
|---|----------|-------|
| 1 | [01-softcode-scenarios.md](01-softcode-scenarios.md) | Convert all hardcoded scenarios into world.db rows |
| 2 | [02-pipeline-db-export.md](02-pipeline-db-export.md) | Pipeline tool exports content pack to world.db |
| 3 | [03-unified-loader.md](03-unified-loader.md) | Replace ScenarioLoader switch with data-driven RegionLoader |
| 4 | [04-region-travel.md](04-region-travel.md) | Region picker + inter-region travel at runtime |

## Dependency graph

```
[01-softcode-scenarios]
        │
        ├──→ [02-pipeline-db-export]   (uses same DB schema)
        │
        └──→ [03-unified-loader]       (consumes DB rows)
                  │
                  └──→ [04-region-travel]  (multi-region navigation)
```

01 is the foundation — once every scenario is a database row, the
other three become incremental.
