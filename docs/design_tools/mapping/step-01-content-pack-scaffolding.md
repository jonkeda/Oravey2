# Step 01 — Content Pack Scaffolding

## Goal

Create the content-pack project structure for the three-tier hierarchy so all
subsequent steps have a target folder to write into.

## Deliverables

### 1.1 Create `content/Oravey2.Apocalyptic.NL/`

```
content/Oravey2.Apocalyptic.NL/
├── Oravey2.Apocalyptic.NL.csproj
├── manifest.json
├── catalog.json          (empty categories, populated later)
├── assets/meshes/        (empty — shared Dutch assets go here)
└── data/                 (empty — country-wide lore/factions later)
```

- `manifest.json`: id `oravey2.apocalyptic.nl`, parent `oravey2.apocalyptic`
- `.csproj`: content-only project matching `Oravey2.Apocalyptic.csproj` pattern

### 1.2 Create `content/Oravey2.Apocalyptic.NL.NH/`

```
content/Oravey2.Apocalyptic.NL.NH/
├── Oravey2.Apocalyptic.NL.NH.csproj
├── manifest.json
├── catalog.json          (empty, populated by step 09)
├── scenarios/            (empty — populated by step 10)
├── towns/                (empty — populated by steps 07–08)
├── overworld/            (empty — populated by step 08)
├── assets/meshes/        (empty — populated by step 09)
└── data/                 (empty — curated-towns.json goes here in step 06)
```

- `manifest.json`: id `oravey2.apocalyptic.nl.nh`, parent `oravey2.apocalyptic.nl`

### 1.3 Add projects to `Oravey2.sln`

Add both new `.csproj` files to the solution under a `content/` solution folder.

### 1.4 Verify build

`dotnet build` succeeds for the solution with the new content-only projects.

## Dependencies

- None — this is the first step.

## Estimated scope

- New files: ~6 (2× `.csproj` + 2× `manifest.json` + 2× `catalog.json`)
- Modified files: 1 (`Oravey2.sln`)
- No code changes
