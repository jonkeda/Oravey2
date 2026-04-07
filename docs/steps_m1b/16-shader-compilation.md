# Step 16 — Shader Compilation Pipeline

**Work streams:** WS-Infra (Build Infrastructure)
**Depends on:** Step 06 (liquid shaders exist as .sdsl files)
**User-testable result:** `dotnet build` compiles all .sdsl shaders → liquid surfaces use animated GPU shaders instead of flat-colour fallback materials.

---

## Goals

1. Enable `Stride.Core.Assets.CompilerApp` build-time shader compilation in the project.
2. Wire all existing .sdsl shaders into the Stride effect system.
3. Replace runtime fallback materials (`CreateTerrainMaterial`/`CreateEmissiveMaterial`) with compiled shader effects.
4. Keep the project code-only (no Stride Game Studio dependency).

---

## Background

The project is code-only — no `.sdproj` or Stride Game Studio. Shaders are authored as `.sdsl` files in `src/Oravey2.Core/Rendering/Shaders/` and currently sit unused at runtime. Materials are built programmatically via `MaterialDescriptor` + `ComputeColor`. To activate the GPU shaders we need the Stride asset compiler to process `.sdsl` → `.sdfx` bytecode at build time.

### Existing .sdsl files

| File | Purpose |
|------|---------|
| `TerrainSplatEffect.sdsl` | 8-surface splat-map terrain blending |
| `WaterShader.sdsl` | Animated UV scroll, waves, foam, caustics |
| `LavaShader.sdsl` | Voronoi crust, emissive cracks, pulsing glow |
| `ToxicShader.sdsl` | Voronoi bubbles, green emissive pulse |
| `OilShader.sdsl` | Thin-film interference rainbow sheen |
| `FrozenShader.sdsl` | Static ice crack pattern |
| `AnomalyShader.sdsl` | Spiral UV distortion, purple emissive |

---

## Tasks

### 16.1 — Add Stride Shader Compilation to Oravey2.Windows

- [ ] Add `Stride.Core.Assets.CompilerApp` package to `Oravey2.Windows.csproj`:
  ```xml
  <PackageReference Include="Stride.Core.Assets.CompilerApp"
                    Version="4.3.0.2507"
                    IncludeAssets="build;buildTransitive"
                    PrivateAssets="all" />
  ```
- [ ] This package provides the MSBuild targets that detect `.sdsl` files and compile them during build
- [ ] Note: `Stride.CommunityToolkit.Windows` may already bring this transitively — verify with `dotnet list package --include-transitive | Select-String CompilerApp`

### 16.2 — Register .sdsl Files as Stride Shader Sources

- [ ] In `Oravey2.Core.csproj`, add an ItemGroup to include `.sdsl` files:
  ```xml
  <ItemGroup>
    <StrideShaderSource Include="Rendering\Shaders\*.sdsl" />
  </ItemGroup>
  ```
- [ ] Alternatively, if the compiler only scans the executable project, move or link the `.sdsl` files into `Oravey2.Windows` or use a shared items project
- [ ] Verify with build output: look for `StrideEffectCompiler` task in MSBuild verbose log:
  ```bash
  dotnet build src/Oravey2.Windows -v detailed 2>&1 | Select-String "Stride.*Shader|StrideEffect|sdsl"
  ```

### 16.3 — Verify Effect Database Generation

- [ ] After build, check for compiled effect database in output:
  ```
  src/Oravey2.Windows/bin/Debug/net10.0/EffectDatabase/
  ```
  or
  ```
  src/Oravey2.Windows/bin/Debug/net10.0/data/db/
  ```
- [ ] Each `.sdsl` should produce a corresponding compiled effect entry
- [ ] If the database directory doesn't appear, check:
  - MSBuild property `<StrideCompileEffects>true</StrideCompileEffects>` may be needed
  - The `.sdsl` file names must match the `shader` declaration inside (e.g., `WaterShader.sdsl` declares `shader WaterShader`)

### 16.4 — Create LiquidEffectFactory

- [ ] Create `Rendering/LiquidEffectFactory.cs`
- [ ] Load compiled effects at runtime using `EffectSystem`:
  ```csharp
  var effect = game.EffectSystem.LoadEffect("WaterShader").WaitForResult();
  ```
- [ ] Build `Material` from the loaded effect:
  ```csharp
  var material = new Material();
  material.Passes.Add(new MaterialPass { Effect = effect });
  ```
- [ ] Map `LiquidType` → shader name:
  - `Water` → `WaterShader`
  - `Lava` → `LavaShader`
  - `Toxic` → `ToxicShader`
  - `Oil` → `OilShader`
  - `Frozen` → `FrozenShader`
  - `Anomaly` → `AnomalyShader`
  - `Acid` → `ToxicShader` (reuse with different parameters)
  - `Sewage` → `WaterShader` (reuse with brown tint)
- [ ] Fallback: if effect load fails (e.g., effects not compiled), return the existing `CreateEmissiveMaterial`/`CreateTerrainMaterial` flat colour

### 16.5 — Wire Effect Materials into HeightmapTerrainScript

- [ ] Replace `RenderLiquidMesh()` material creation:
  ```csharp
  // Before
  model.Materials.Add(liquidMesh.Emissive
      ? CreateEmissiveMaterial(color)
      : CreateTerrainMaterial(color));

  // After
  model.Materials.Add(LiquidEffectFactory.CreateMaterial(
      GraphicsDevice, liquidMesh.Type, color));
  ```
- [ ] Pass per-type shader parameters (uniforms) via `material.Passes[0].Parameters.Set()`:
  - `WaterColor`, `ScrollSpeed`, `WaveAmplitude`, `QualityLevel`
  - `CrustColor`, `CrackColor`, `EmissiveIntensity`, `FlowSpeed`
  - etc. (see each `.sdsl` for declared uniforms)

### 16.6 — Wire TerrainSplatEffect into Terrain Rendering

- [ ] Load `TerrainSplatEffect` via `EffectSystem`
- [ ] Create terrain material with two splat map textures + 8 albedo textures
- [ ] Upload splat data from `ChunkTerrainMesh.SplatMap0`/`SplatMap1` as textures
- [ ] For now, use procedural 1×1 textures for the 8 albedo slots (solid colour per surface type)
- [ ] Replace `GetDominantColor` single-colour material with the splat-blended material

### 16.7 — Quality Preset Integration

- [ ] Set shader `QualityLevel` uniform based on `QualityPreset`:
  - `Low` → 0 (flat tint, no animation)
  - `Medium` → 1 (animated UV, wave normals)
  - `High` → 2 (foam, caustics, reflections)
- [ ] On `Low` quality, skip effect loading entirely and use the flat-colour fallback

### 16.8 — Unit Tests

File: `tests/Oravey2.Tests/Rendering/LiquidEffectFactoryTests.cs`

- [ ] `AllLiquidTypes_MapToShaderName` — every `LiquidType` except `None` returns a valid shader name
- [ ] `FallbackMaterial_WhenEffectNotLoaded` — factory returns non-null material even when effects unavailable

### 16.9 — Verify

```bash
# Build with shader compilation
dotnet build src/Oravey2.Windows

# Check for compiled effects
Get-ChildItem src/Oravey2.Windows/bin/Debug/net10.0 -Recurse -Filter "*.sdfx" 2>$null
Get-ChildItem src/Oravey2.Windows/bin/Debug/net10.0 -Recurse -Filter "EffectDatabase*" 2>$null

# Run unit tests
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~LiquidEffectFactory"

# Visual test
dotnet run --project src/Oravey2.Windows -- --scenario terrain_test
```

**User test:** Launch the game with `terrain_test` scenario. Water lake shows animated waves instead of flat blue. Lava pool has visible crust pattern with glowing cracks. Toxic puddle pulses green. Terrain chunks show splat-blended surfaces instead of single-colour fills.

---

## Notes

- The `Stride.Core.Assets.CompilerApp` package version **must match** the `Stride.Engine` version (`4.3.0.2507`).
- If package restore fails due to the compiler app requiring additional runtime components, try adding: `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` to the Windows project (already present via `Stride.CommunityToolkit.Windows`).
- Shader compilation happens at build time, not runtime. Changing a `.sdsl` file requires a rebuild.
- The `.sdsl` files must be in a project that the compiler can discover. If they live in `Oravey2.Core` (class library), ensure the compiler's MSBuild targets scan referenced projects or move them to the executable project.
