# Plan: Theme Content Packs (Apocalyptic & Fantasy)

## Problem

All game content is currently baked into `Oravey2.Core` (code + logic) and `Oravey2.Windows` (compiled maps). Theme-specific data — blueprints, item catalogs, meshes, textures, NPC definitions, dialogue trees, quest chains — is either hardcoded in C# or scattered across test fixtures. There's no way to swap between a post-apocalyptic world and a fantasy world without forking the entire codebase.

## Goal

Create two new content-only projects — **Oravey2.Apocalyptic** and **Oravey2.Fantasy** — that package all theme-specific data as standalone, swappable content packs. The game engine (`Oravey2.Core`) stays theme-agnostic; content packs supply the flavor.

## Current Content Locations (what moves)

| Content Type | Current Home | Notes |
|---|---|---|
| Map blueprints | `tests/.../Fixtures/Blueprints/` | Only `sample_portland.json` exists |
| Compiled maps | `src/Oravey2.Windows/Maps/portland/` | world.json + chunks + buildings.json + props.json + zones.json |
| Asset catalog | `src/Oravey2.MapGen/Assets/asset-catalog.json` | Building/prop/surface mesh IDs |
| Item definitions | `Oravey2.Core/Inventory/Items/M0Items.cs` | Hardcoded in C# |
| NPC definitions | `Oravey2.Core/Bootstrap/ScenarioLoader.cs` | Inline in `SpawnNpcs()` |
| Dialogue trees | `Oravey2.Core/NPC/TownDialogueTrees.cs` | Hardcoded in C# |
| Quest chains | `Oravey2.Core/Quests/QuestChainDefinitions.cs` | Hardcoded in C# |
| Town/wasteland maps | `TownMapBuilder.cs`, `WastelandMapBuilder.cs` | Procedural C# builders |
| Spawn points | Inline in `LoadWasteland()` | Hardcoded positions |
| JSON schemas | `docs/schemas/` | items, quests, dialogues, factions, etc. |

## Architecture

```
Oravey2.sln
├── src/Oravey2.Core/             # Engine — theme-agnostic game logic
├── src/Oravey2.Windows/          # Platform launcher
├── src/Oravey2.MapGen/           # LLM map generation (theme-agnostic)
├── src/Oravey2.MapGen.App/       # MAUI generator UI
│
├── content/Oravey2.Apocalyptic/  # Post-apocalyptic theme pack
├── content/Oravey2.Fantasy/      # Fantasy theme pack
│
└── tests/...
```

Content packs are **data-only projects** — no C# game logic, just JSON + assets + a manifest. The engine loads them by convention.

## Step 1: Folder Structure for Content Packs

```
content/Oravey2.Apocalyptic/
├── Oravey2.Apocalyptic.csproj        # SDK-style project (content-only NuGet pack)
├── manifest.json                      # Pack metadata + version + dependencies
│
├── blueprints/                        # Raw blueprint JSON files
│   ├── portland.json                  # LLM-generated or hand-authored
│   ├── denver.json
│   └── ...
│
├── maps/                              # Pre-compiled maps (ready to play)
│   ├── portland/
│   │   ├── world.json
│   │   ├── buildings.json
│   │   ├── props.json
│   │   ├── zones.json
│   │   └── chunks/
│   │       ├── 0_0.json
│   │       └── ...
│   └── denver/
│       └── ...
│
├── data/                              # Theme-specific JSON data
│   ├── items.json                     # Item catalog (weapons, armor, consumables)
│   ├── npcs.json                      # NPC definitions (id, name, role, schedule)
│   ├── factions.json                  # Faction definitions + reputation config
│   ├── dialogues/                     # Dialogue trees per NPC
│   │   ├── elder_dialogue.json
│   │   ├── merchant_dialogue.json
│   │   └── ...
│   ├── quests/                        # Quest chain definitions
│   │   ├── main_quest.json
│   │   ├── side_quests.json
│   │   └── ...
│   ├── enemies.json                   # Enemy types, stats, loot tables
│   ├── loot-tables.json               # Drop rate definitions
│   ├── recipes.json                   # Crafting recipes
│   └── surfaces.json                  # Surface type definitions
│
├── assets/                            # Binary assets (meshes, textures, audio)
│   ├── meshes/
│   │   ├── buildings/
│   │   │   ├── ruined_office.glb
│   │   │   ├── radio_tower.glb
│   │   │   └── ...
│   │   ├── props/
│   │   │   ├── car_wreck.glb
│   │   │   ├── barrel.glb
│   │   │   └── ...
│   │   ├── characters/
│   │   │   ├── raider.glb
│   │   │   ├── settler.glb
│   │   │   └── ...
│   │   └── terrain/
│   │       ├── cliff_face.glb
│   │       └── ...
│   ├── textures/
│   │   ├── tiles/                     # Tile surface textures
│   │   │   ├── asphalt_cracked.png
│   │   │   ├── dirt.png
│   │   │   └── ...
│   │   ├── buildings/
│   │   └── ui/                        # Theme-specific UI elements
│   │       ├── hud_frame.png
│   │       └── icons/
│   ├── audio/
│   │   ├── ambient/                   # Zone ambient loops
│   │   ├── music/                     # Theme-specific music layers
│   │   └── sfx/                       # Sound effects
│   └── sprites/
│       ├── item_icons/                # Inventory item icon atlas
│       └── portraits/                 # NPC portraits
│
├── catalog.json                       # Asset catalog (replaces MapGen's asset-catalog.json)
│
└── scenarios/                         # Scenario definitions (what the selector shows)
    ├── portland.json                   # { "id": "portland", "name": "Portland", "map": "maps/portland", ... }
    ├── denver.json
    └── tutorial.json
```

The Fantasy pack follows the **exact same structure** with different content:

```
content/Oravey2.Fantasy/
├── Oravey2.Fantasy.csproj
├── manifest.json
├── blueprints/
│   ├── eldergrove.json
│   └── ironhold.json
├── maps/
│   └── eldergrove/...
├── data/
│   ├── items.json                     # Swords, potions, spell scrolls
│   ├── npcs.json                      # Innkeepers, blacksmiths, wizards
│   ├── enemies.json                   # Goblins, dragons, undead
│   ├── factions.json                  # Guilds, kingdoms, cults
│   ├── dialogues/...
│   ├── quests/...
│   └── ...
├── assets/
│   ├── meshes/buildings/              # Taverns, castles, towers
│   ├── meshes/props/                  # Barrels, crates, torches
│   ├── meshes/characters/             # Knights, mages, orcs
│   └── ...
├── catalog.json
└── scenarios/...
```

## Step 2: manifest.json Schema

```json
{
  "id": "oravey2.apocalyptic",
  "name": "Post-Apocalyptic",
  "version": "0.1.0",
  "description": "Wasteland survival in a ruined civilization",
  "author": "Oravey2 Team",
  "engineVersion": ">=0.1.0",
  "tags": ["apocalyptic", "survival", "wasteland"],
  "defaultScenario": "portland",
  "palette": {
    "primary": "#4A6741",
    "accent": "#C17817",
    "danger": "#8B2500"
  }
}
```

The manifest gives the engine all it needs to list, validate, and load the pack.

## Step 3: Content Pack csproj (NuGet Packaging)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <PackageId>Oravey2.Apocalyptic</PackageId>
    <Version>0.1.0</Version>
    <Description>Post-apocalyptic content pack for Oravey2</Description>
    <NoBuild>true</NoBuild>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <ContentTargetFolders>contentFiles</ContentTargetFolders>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
  </PropertyGroup>

  <!-- Pack all content files into the NuGet package -->
  <ItemGroup>
    <Content Include="manifest.json" PackagePath="content/" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="catalog.json" PackagePath="content/" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="blueprints\**\*" PackagePath="content/blueprints/" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="maps\**\*" PackagePath="content/maps/" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="data\**\*" PackagePath="content/data/" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="assets\**\*" PackagePath="content/assets/" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="scenarios\**\*" PackagePath="content/scenarios/" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

### Packaging Options

| Method | When to use | Command |
|---|---|---|
| **NuGet package** | Distribution, versioning, CI/CD | `dotnet pack content/Oravey2.Apocalyptic/` |
| **Direct project reference** | Development, fast iteration | `<ProjectReference Include="..\..\content\Oravey2.Apocalyptic\" />` |
| **Loose folder** | Modding, user-created content | Copy to `{game}/ContentPacks/Oravey2.Apocalyptic/` |

The recommended approach is **NuGet for releases** + **project reference for dev**. This lets CI produce versioned `.nupkg` files while developers get instant hot-reload.

For loose-folder modding support, the engine scans `ContentPacks/` at startup and loads any folder containing a valid `manifest.json`.

## Step 4: Engine Integration (Oravey2.Core changes)

### 4a. ContentPackLoader

New class in `Oravey2.Core/Content/`:

```csharp
public sealed class ContentPackLoader
{
    public ContentPack LoadPack(string packRootDir);      // From loose folder
    public ContentPack LoadFromPackage(string nupkgPath);  // From NuGet
    public ContentPack[] DiscoverPacks(string searchDir);  // Scan ContentPacks/
}

public sealed record ContentPack(
    ContentManifest Manifest,
    string RootDirectory,
    AssetCatalog Catalog
);
```

### 4b. Data-Driven Loaders

Replace hardcoded C# definitions with JSON loaders:

| Current C# Class | Replacement |
|---|---|
| `M0Items.cs` | `ItemCatalog.LoadFromJson(pack.Data("items.json"))` |
| `TownDialogueTrees.cs` | `DialogueCatalog.LoadFromJson(pack.Data("dialogues/"))` |
| `QuestChainDefinitions.cs` | `QuestCatalog.LoadFromJson(pack.Data("quests/"))` |
| `SpawnNpcs()` inline | `NpcCatalog.LoadFromJson(pack.Data("npcs.json"))` |
| Inline `EnemySpawnPoint`s | `EnemyCatalog.LoadFromJson(pack.Data("enemies.json"))` |
| `TownMapBuilder.cs` | Compiled map in `pack.Maps("town/")` |

### 4c. ScenarioLoader Integration

The existing `LoadFromCompiledMap()` already loads maps from disk. Extend it to accept a content pack root:

```csharp
case var id when activePack.HasScenario(id):
    var scenarioDef = activePack.LoadScenario(id);
    LoadFromCompiledMap(id, scenarioDef.MapPath, ...);
    break;
```

## Step 5: Migration Path

### Phase A — Scaffolding (this plan)
1. Create `content/Oravey2.Apocalyptic/` with the folder structure above
2. Create `content/Oravey2.Fantasy/` with the same structure (empty data)
3. Add both .csproj files to the solution
4. Move `sample_portland.json` blueprint to `content/Oravey2.Apocalyptic/blueprints/`
5. Move compiled `Maps/portland/` to `content/Oravey2.Apocalyptic/maps/portland/`
6. Copy `asset-catalog.json` to `content/Oravey2.Apocalyptic/catalog.json`
7. Create `manifest.json` for both packs
8. Have `Oravey2.Windows` reference `Oravey2.Apocalyptic` so content copies to output

### Phase B — Data Extraction
1. Extract `M0Items` → `content/Oravey2.Apocalyptic/data/items.json`
2. Extract `TownDialogueTrees` → `content/Oravey2.Apocalyptic/data/dialogues/*.json`
3. Extract `QuestChainDefinitions` → `content/Oravey2.Apocalyptic/data/quests/*.json`
4. Extract NPC definitions → `content/Oravey2.Apocalyptic/data/npcs.json`
5. Extract enemy spawn configs → `content/Oravey2.Apocalyptic/data/enemies.json`
6. Write JSON loaders in Core to replace hardcoded C# classes

### Phase C — Fantasy Content
1. Author fantasy items, NPCs, enemies, dialogues, quests
2. Generate fantasy map blueprints via MapGen (swap asset catalog)
3. Compile blueprints → `content/Oravey2.Fantasy/maps/`
4. Create fantasy scenario definitions

### Phase D — Pack Selection UI
1. Add content pack picker to start menu (or game launcher)
2. ScenarioSelector sources from active pack instead of hardcoded list
3. MapGen App loads asset catalog from selected content pack

## Step 6: Scenario Definition Schema

Each scenario in `scenarios/` maps to a playable entry in the selector:

```json
{
  "id": "portland",
  "name": "Portland Ruins",
  "description": "Explore the shattered remains of Portland, Oregon.",
  "map": "maps/portland",
  "playerStart": { "chunkX": 0, "chunkY": 0, "tileX": 5, "tileY": 5 },
  "enemies": "data/enemies.json",
  "npcs": ["data/npcs.json"],
  "quests": ["data/quests/main_quest.json"],
  "music": "assets/audio/music/wasteland_theme.ogg",
  "ambient": "assets/audio/ambient/wind_ruins.ogg",
  "features": ["combat", "quests", "loot", "dialogue"],
  "difficulty": 2,
  "tags": ["exploration", "combat", "story"]
}
```

## Step 7: MapGen Integration

The MapGen app currently embeds `asset-catalog.json`. After this change:

1. MapGen loads the asset catalog from the **selected content pack**
2. User picks a pack before generating → LLM gets pack-appropriate assets
3. Generated blueprint saves to `content/{Pack}/blueprints/{name}.json`
4. Compile button outputs to `content/{Pack}/maps/{name}/`

This means the same MapGen app can generate apocalyptic cities or fantasy castles depending on which pack is active.

## Priority Order

| Step | Priority | Description |
|---|---|---|
| Phase A.1-A.3 | **P0** | Create folder structure + csproj files |
| Phase A.4-A.6 | **P0** | Move existing content to Apocalyptic pack |
| Phase A.7-A.8 | **P0** | Wire up project references |
| Phase B.1-B.5 | P1 | Extract hardcoded data to JSON |
| Phase B.6 | P1 | Build JSON loaders |
| Phase C | P2 | Author fantasy content |
| Phase D | P2 | Pack selection UI |
