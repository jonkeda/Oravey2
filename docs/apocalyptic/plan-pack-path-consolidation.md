# Plan: Content Pack Path Consolidation

## Problem

The MapGen App currently has **three** path settings for content packs:
- **Export Path** — where blueprints are saved
- **Game Install Path** — where compiled maps are copied to (`{path}/Maps/{name}/`)
- **Content Pack Catalog** — which asset catalog the LLM uses for generation

These are redundant. The user should point to **one** content pack project directory (e.g. `content/Oravey2.Apocalyptic/`) and everything derives from that.

Additionally, `Oravey2.Windows.csproj` hardcodes both pack paths with MSBuild properties. Adding new packs requires editing the game's csproj.

## Goal

1. **One Path**: MapGen Settings has a single "Content Pack" path pointing to a pack project folder (e.g. `content/Oravey2.Apocalyptic/`)
2. **Auto-derive**: catalog, blueprint save, and compiled map output all resolve from that path
3. **Auto-copy**: building `Oravey2.Windows` auto-discovers and copies all packs from `content/` — no per-pack MSBuild entries

## Current Flow

```
User generates blueprint
  → Saved to ExportPath/
    → Compiled to ExportPath/compiled/{name}/
      → "Install to Game" copies to GameInstallPath/Maps/{name}/

Content pack catalog loaded from ContentPackCatalogPath (manual browse)
Game discovers packs at runtime from {bin}/ContentPacks/*/manifest.json
```

## New Flow

```
User sets Content Pack Path = content/Oravey2.Apocalyptic/
  → catalog.json loaded from {pack}/catalog.json (auto)
  → Blueprints saved to {pack}/blueprints/{name}.json
  → Compiled to {pack}/maps/{name}/
  → Next build of Oravey2.Windows auto-copies everything to bin/ContentPacks/

No "Install to Game" step needed — build handles it.
```

## Steps

### Step 1 — Replace three settings with one "Content Pack Path"

**Files**: `SettingsViewModel.cs`, `SettingsView.xaml`

- Remove `GameInstallPath` property, browse command, and preference
- Remove `ContentPackCatalogPath` property, browse command, and preference
- Add `ContentPackPath` — points to a content pack project directory
- Browse button selects a folder; validation checks for `manifest.json`
- Keep `ExportPath` as a separate fallback for users without a content pack

### Step 2 — Update GeneratorViewModel to use content pack path

**Files**: `GeneratorViewModel.cs`

- `SaveBlueprintAsync()` → saves to `{ContentPackPath}/blueprints/{name}.json` (fallback: ExportPath)
- `CompileBlueprint()` → outputs to `{ContentPackPath}/maps/{name}/` (fallback: ExportPath/compiled/)
- `GenerateAsync()` → loads catalog from `{ContentPackPath}/catalog.json` (fallback: embedded)
- Remove `InstallToGame` command and button — no longer needed
- Remove `InstallToGameCommand` from `GeneratorView.xaml`

### Step 3 — Auto-discover packs in Oravey2.Windows.csproj

**Files**: `Oravey2.Windows.csproj`

Replace the per-pack hardcoded `<Content>` items with a wildcard pattern:

```xml
<!-- Auto-discover all content packs from content/ -->
<PropertyGroup>
  <ContentRoot>$([MSBuild]::NormalizePath('$(MSBuildProjectDirectory)\..\..\content'))</ContentRoot>
</PropertyGroup>

<ItemGroup>
  <ContentPackDirs Include="$(ContentRoot)\*\manifest.json" />
  <Content Include="$(ContentRoot)\**\*"
           Exclude="$(ContentRoot)\**\bin\**;$(ContentRoot)\**\obj\**;$(ContentRoot)\**\*.csproj;$(ContentRoot)\**\.gitkeep"
           Link="ContentPacks\%(RecursiveDir)%(Filename)%(Extension)"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

This means:
- Adding `content/Oravey2.Steampunk/` automatically appears in the game's output
- No csproj edits required per pack
- Remove the per-pack `ApocalypticContentDir` / `FantasyContentDir` properties

### Step 4 — Update GeneratorView.xaml

**Files**: `GeneratorView.xaml`

- Remove "Install to Game" button
- Rename "Compile Blueprint" button to "Compile to Pack" for clarity

### Step 5 — Clean up

- Remove `BrowseGameInstallPathAsync()` and `BrowseContentPackAsync()` methods
- Remove unused `CopyDirectory()` from GeneratorViewModel
- Verify MapGen Build target still works

## Priority

| Step | Priority | Description |
|---|---|---|
| 1 | **P0** | Replace three settings with one Content Pack Path |
| 2 | **P0** | Update GeneratorViewModel to use content pack path |
| 3 | **P0** | Auto-discover packs in csproj |
| 4 | P1 | Update GeneratorView.xaml |
| 5 | P1 | Clean up dead code |
