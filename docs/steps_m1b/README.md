# M1b Development Steps — Map & Generation

Each step builds on the previous and ends with a **user-testable result**.

| Step | Name | User Can Test |
|------|------|---------------|
| [01](01-data-model-cleanup.md) | Data Model & Cleanup | Unit tests pass, solution compiles with expanded TileData |
| [02](02-sqlite-storage.md) | SQLite Storage Layer | Unit tests prove round-trip tile serialisation and DB operations |
| [03](03-heightmap-renderer.md) | Heightmap Terrain Renderer | Launch game → see heightmap terrain instead of per-tile quads |
| [04](04-linear-features.md) | Roads, Rails, Rivers | Launch game → roads and rivers render as continuous splines |
| [05](05-hybrid-overlay.md) | Hybrid Mode (Towns) | Launch game → town chunk shows floor tiles + walls on heightmap |
| [06](06-liquids.md) | Liquid Rendering | Launch game → water, lava, toxic pools visible with shaders |
| [07](07-trees.md) | Vegetation (Trees) | Launch game → trees render at L1, billboards at distance |
| [08](08-worldtemplate-pipeline.md) | Real-World Data Pipeline | Build tool produces a WorldTemplate from OSM + elevation data |
| [09](09-procedural-generation.md) | Procedural Map Generation | New game → explorable terrain generated from real-world data |
| [10](10-chunk-streaming.md) | Chunk Streaming & On-Demand Gen | Walk around → new chunks generate and stream in seamlessly |
| [11](11-zoom-levels.md) | Multi-Scale Zoom (L1–L3) | Scroll to zoom out → smooth transition from local to continental |
| [12](12-globe-view.md) | Globe View (L4) | Open globe UI → see planet with continents, travel between them |
| [13](13-weather.md) | Weather Visuals | Toggle weather → rain/snow/dust visible on terrain |
| [14](14-minimap.md) | Minimap & HUD | Corner minimap shows terrain, icons, fog of war |
| [15](15-location-descriptions.md) | Location Descriptions | Click a POI → info panel slides in with tiered descriptions |

**Prerequisites:** Decisions in [map-decisions-and-tasks.md](../designs_m1b/map-decisions-and-tasks.md) are all resolved.
