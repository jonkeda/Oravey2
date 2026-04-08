# Milestone 1b — Manual Test Guides

These guides describe how to manually verify each feature implemented in the M1b milestone. The primary workflow is:

1. **Build a world** from real-world data (Noord-Holland) using the WorldTemplate tool
2. **Generate a game world** from that template
3. **Walk around** and verify terrain, features, and gameplay systems

## Prerequisites

- .NET 10 SDK installed
- SRTM elevation data in `data/srtm/` (`.hgt` files covering Noord-Holland)
- OSM extract at `data/noordholland.osm.pbf`
- GPU with DirectX 11+ support

## Quick Start

```powershell
# 1. Build everything
dotnet build Oravey2.sln

# 2. Create world template from real-world data
dotnet run --project tools/Oravey2.WorldTemplateTool -- `
  --srtm data/srtm `
  --osm data/noordholland.osm.pbf `
  --output content/noordholland.worldtemplate `
  --name NoordHolland

# 3. Launch the game (start menu → New Game)
dotnet run --project src/Oravey2.Windows
```

## Keyboard Controls

| Key | Action |
|-----|--------|
| W / S / A / D | Move forward / back / left / right |
| Q / E | Rotate camera left / right |
| PageUp / PageDown | Zoom in / out |
| Mouse scroll | Zoom in / out |
| Space | Attack |
| F | Interact |
| I | Inventory |
| J | Quest journal |
| N | Location info panel |
| M | Toggle minimap |
| Escape | Pause menu |
| F5 / F9 | Quick save / Quick load |
| F11 | Toggle fullscreen |

## Test Guides

| Guide | Covers Steps | Topic |
|-------|-------------|-------|
| [01-world-creation.md](01-world-creation.md) | 1, 2, 8, 9 | Building a world from real-world data |
| [02-terrain-and-walking.md](02-terrain-and-walking.md) | 3, 4, 5, 6, 7, 17 | Terrain rendering and player movement |
| [03-navigation-and-zoom.md](03-navigation-and-zoom.md) | 10, 11, 12, 14 | Chunk streaming, zoom levels, minimap |
| [04-atmosphere-and-ui.md](04-atmosphere-and-ui.md) | 13, 15, 16 | Weather, descriptions, shaders |
