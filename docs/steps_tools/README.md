# Tools Development Steps — MapGen.App WorldTemplate Tab

Each step builds on the previous and ends with a **user-testable result**.

| Step | Name | User Can Test |
|------|------|---------------|
| [01](01-data-models.md) | CullSettings & Region Presets | Unit tests pass for serialization round-trip; Noord-Holland preset loads |
| [02](02-feature-culling.md) | Feature Culling Engine | Unit tests prove town/road/water culling and geometry simplification |
| [03](03-data-downloads.md) | Data Download Service | Integration tests download an SRTM tile and a small OSM extract |
| [04](04-viewmodel-settings.md) | ViewModel & Settings Persistence | Unit tests for ViewModel commands; settings round-trip via Preferences |
| [05](05-tab-layout.md) | WorldTemplate Tab XAML | Launch app → new WorldTemplate tab visible with source section |
| [06](06-map-canvas.md) | Map Canvas Rendering | Parse data → map canvas shows elevation, towns, roads, water |
| [07](07-feature-lists-culling-ui.md) | Feature Lists & Auto-Cull Dialog | Feature panels with checkboxes → Auto-cull dialog applies rules |
| [08](08-cli-integration.md) | CLI --cull Flag | `WorldTemplateTool --cull settings.json` produces a culled template |

**Design document:** [mapgen-app-worldtemplate-v2.md](../design_tools/mapgen-app-worldtemplate-v2.md)

**Prerequisites:**
- Existing `Oravey2.MapGen` project with `OsmParser`, `SrtmParser`, `WorldTemplateBuilder`
- Existing `Oravey2.MapGen.App` MAUI app with `TabbedPage` and `BaseViewModel`
- Existing `Oravey2.WorldTemplateTool` console app
