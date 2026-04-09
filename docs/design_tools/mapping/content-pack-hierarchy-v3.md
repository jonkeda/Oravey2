# Content Pack Hierarchy — v3

## Problem

The current structure has one flat content pack per theme
(`Oravey2.Apocalyptic`, `Oravey2.Fantasy`). Generated data for all regions
goes into the same pack, making it hard to:

- Work on one region without touching another
- Share common assets across regions of the same country
- Track which assets are hand-made vs generated

## Proposed hierarchy

```
content/
├── Oravey2.Apocalyptic/                     ← base theme (existing)
│   ├── manifest.json                         genre-level data, enemies, items
│   ├── catalog.json                          base asset catalog
│   ├── data/                                 enemies.json, items.json, npcs.json, quests/
│   ├── assets/                               hand-made & generic assets
│   └── scenarios/                            (empty — regions provide scenarios)
│
├── Oravey2.Apocalyptic.NL/                  ← country overlay
│   ├── manifest.json                         country-level metadata
│   ├── catalog.json                          shared Dutch assets (windmills, dykes, canal boats…)
│   ├── assets/meshes/                        .glb files common to all NL regions
│   └── data/                                 country-wide factions, lore, Dutch name tables
│
├── Oravey2.Apocalyptic.NL.NH/              ← region pack (Noord-Holland)
│   ├── manifest.json                         region metadata + parent refs
│   ├── catalog.json                          region-specific asset catalog
│   ├── scenarios/
│   │   └── noord-holland.json                links all towns into a playable scenario
│   ├── towns/
│   │   ├── {gameName}/
│   │   │   ├── design.json                   LLM feature design (step ⑥)
│   │   │   ├── layout.json                   condensed tile map (step ⑦)
│   │   │   ├── buildings.json                placed buildings
│   │   │   ├── props.json                    placed props
│   │   │   └── zones.json                    biome/threat zones
│   │   └── …
│   ├── overworld/
│   │   ├── world.json                        region dimensions, player start
│   │   ├── roads.json                        inter-town road network
│   │   └── water.json                        rivers, lakes, sea
│   ├── assets/
│   │   ├── meshes/                           generated .glb files (Meshy)
│   │   │   ├── {assetId}.glb
│   │   │   └── {assetId}.meta.json           provenance: prompt, meshy task ID, date
│   │   ├── textures/                         generated or curated textures
│   │   └── sprites/
│   └── data/
│       ├── curated-towns.json                output of step ⑤ (town selection)
│       ├── factions.json                     region-specific factions
│       └── dialogues/
│
└── Oravey2.Fantasy/                         ← other theme (unchanged)
    └── …
```

## Manifest schema

Each pack's `manifest.json` declares its parent chain:

```json
{
  "id": "oravey2.apocalyptic.nl.nh",
  "name": "Noord-Holland",
  "version": "0.1.0",
  "description": "Post-apocalyptic Noord-Holland region",
  "author": "Oravey2 Team",
  "engineVersion": ">=0.1.0",
  "parent": "oravey2.apocalyptic.nl",
  "tags": ["apocalyptic", "netherlands", "noord-holland"],
  "defaultScenario": "noord-holland",
  "palette": {
    "primary": "#4A6741",
    "accent": "#C17817",
    "danger": "#8B2500"
  }
}
```

The loader resolves the chain:
```
oravey2.apocalyptic.nl.nh
  → oravey2.apocalyptic.nl       (country assets)
    → oravey2.apocalyptic         (base theme: enemies, items, generic meshes)
```

Asset lookups cascade upward: a building in NH that references
`meshes/windmill.glb` first checks the NH pack, then NL, then the base theme.

## Catalog merging

Each `catalog.json` follows the existing format (keyed by category: `building`,
`prop`, `surface`, `terrain_mesh`). At load time, catalogs are merged
bottom-up. Entries in a child pack override entries with the same `id` in a
parent pack.

```json
// Oravey2.Apocalyptic.NL/catalog.json
{
  "building": [
    { "id": "buildings/windmill.glb", "description": "Ruined Dutch windmill", "tags": ["large", "landmark", "dutch"] },
    { "id": "buildings/canal_house.glb", "description": "Narrow canal house", "tags": ["medium", "residential", "dutch"] }
  ],
  "prop": [
    { "id": "props/bicycle_wreck.glb", "description": "Rusted bicycle", "tags": ["vehicle", "small", "dutch"] },
    { "id": "props/dyke_section.glb", "description": "Broken dyke segment", "tags": ["terrain", "dutch"] }
  ]
}
```

## `.csproj` per pack

Each pack is a content-only `.csproj` (no compiled code), matching the
existing pattern:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <PackageId>Oravey2.Apocalyptic.NL.NH</PackageId>
    <Version>0.1.0</Version>
    <Description>Post-apocalyptic Noord-Holland region content</Description>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <ContentTargetFolders>contentFiles</ContentTargetFolders>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
  </PropertyGroup>

  <ItemGroup>
    <Content Include="manifest.json" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="catalog.json" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="scenarios\**\*" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="towns\**\*" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="overworld\**\*" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="assets\**\*" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="data\**\*" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

## Game-time loading

The game does **not** read the content pack files directly during play. At
"New Game" or "Load", the content-pack loader:

1. Resolves the parent chain
2. Merges catalogs
3. Reads town layouts, buildings, props, zones, overworld data
4. Writes everything into a runtime `world.db` (SQLite) for fast indexed
   queries during gameplay

This means the JSON files are the **source of truth** during authoring, and
`world.db` is a derived cache that can always be rebuilt.

## What about `data/regions/noord-holland/`?

The raw downloaded data (`.osm.pbf`, `.hgt.gz`, `region.json`) stays in
`data/regions/` — this is source data, not generated content. The content pack
under `content/` only contains pipeline outputs.

```
data/regions/noord-holland/          ← raw downloads (input)
content/Oravey2.Apocalyptic.NL.NH/  ← generated content (output)
```
