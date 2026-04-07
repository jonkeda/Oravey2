# Step 13 — Weather & Atmosphere

**Work streams:** WS12 (Weather Visuals)
**Depends on:** Step 03 (heightmap renderer), Step 06 (liquid renderer)
**User-testable result:** Toggle weather in debug menu → rain, snow, dust storm, or radiation fog visible on the terrain with appropriate sound ambience.

---

## Goals

1. `WeatherState` driving shader overlays and particle systems.
2. Four weather types: rain, snow, dust storm, radiation fog.
3. Day/night cycle lighting tied to `GameTimeService`.
4. Smooth transitions between weather states.

---

## Tasks

### 13.1 — WeatherState Model

- [ ] Create `Weather/WeatherState.cs`
- [ ] Fields: `WeatherType` (enum: Clear, Rain, Snow, DustStorm, RadiationFog, AcidRain), `Intensity` (0–1), `WindDirection` (Vector2), `WindSpeed` (float)
- [ ] `WeatherService.cs`: updates `WeatherState` per-tick from biome + season + random variation
- [ ] Transition blending: when type changes, old intensity fades out while new fades in over 5–10 seconds

### 13.2 — Rain

- [ ] Particle system: angled raindrops following `WindDirection`
- [ ] Terrain wet overlay: surface darkening + specular increase (shader parameter `_Wetness`)
- [ ] Splashes on ground: screen-space or decal-based ripple effect
- [ ] Water bodies: raindrop ripple rings (modify liquid shader input)
- [ ] Sound: rain ambient loop, volume = intensity

### 13.3 — Snow

- [ ] Particle system: drifting snowflakes (larger, slower than rain)
- [ ] Terrain snow accumulation: vertex-colour blend toward white based on `_SnowCoverage` (0–1)
- [ ] Accumulation rises over time when snowing, melts slowly when clear
- [ ] Tracks in snow: character/vehicle movement reduces local `_SnowCoverage`
- [ ] Sound: muffled wind ambient

### 13.4 — Dust Storm

- [ ] Volumetric haze overlay: reduce visibility, tint scene yellow-brown
- [ ] Particle system: swirling debris particles
- [ ] Increased wind sound, direction-dependent
- [ ] Reduced camera far clip at high intensity
- [ ] Character stats effect: movement slowed, visibility reduced (gameplay hook)

### 13.5 — Radiation Fog

- [ ] Green-tinted exponential fog
- [ ] Subtle glow particles drifting upward
- [ ] Post-apocalyptic-specific: spawns near irradiated zones
- [ ] Geiger counter sound effect if player has detector equipped
- [ ] Gameplay hook: exposure timer starts, radiation damage

### 13.6 — Acid Rain

- [ ] Reuse rain particle system, green-tinted droplets
- [ ] Terrain corrosion overlay: surface pits + neon green puddles on flat areas
- [ ] Damage to unprotected characters/vehicles (gameplay hook)
- [ ] Sound: hissing rain loop

### 13.7 — Day/Night Cycle

- [ ] `DayNightController.cs`: drives sun light direction + colour from `GameTimeService.TimeOfDay`
- [ ] Dawn (05:00–07:00): warm orange, low angle
- [ ] Day (07:00–17:00): white to cool-white, high angle
- [ ] Dusk (17:00–19:00): warm red-orange, low angle
- [ ] Night (19:00–05:00): blue ambient, moonlight directional
- [ ] Sky gradient or skybox rotation matching time
- [ ] Entity shadows stretch at dawn/dusk, short at midday

### 13.8 — Weather ↔ Zoom Integration

- [ ] L1: full particle effects + sound
- [ ] L2: particles disabled, overlay effects only (wet/snow/fog tinting)
- [ ] L3: weather shown as regional icons (cloud icon, snowflake icon)
- [ ] L4 (globe): weather patterns as animated cloud texture overlay

### 13.9 — Debug Menu

- [ ] Add weather override to debug menu
- [ ] Dropdown: weather type selection
- [ ] Slider: intensity (0–1)
- [ ] Wind direction + speed controls
- [ ] Time-of-day override slider (0–24 h)

### 13.10 — Unit Tests

File: `tests/Oravey2.Tests/Weather/WeatherServiceTests.cs`

- [ ] `DefaultWeather_IsClear` — new service starts clear
- [ ] `SetWeather_Rain_IntensityRamps` — switching to rain, intensity increases per tick
- [ ] `Transition_OldTypeFadesOut` — old weather intensity decreases during transition
- [ ] `WindDirection_NormalisedVector` — always unit length

File: `tests/Oravey2.Tests/Weather/DayNightControllerTests.cs`

- [ ] `Noon_SunAtHighAngle` — 12:00 → sun elevation > 60°
- [ ] `Midnight_LowAmbient_MoonDirection` — 00:00 → blue ambient, moon light active
- [ ] `Dawn_WarmColour` — 06:00 → colour temperature warm
- [ ] `TimeAdvance_CyclesCorrectly` — 48 hours of updates cycles through 2 full day/night

File: `tests/Oravey2.Tests/Weather/SnowAccumulationTests.cs`

- [ ] `Snowing_AccumulationIncreases` — `_SnowCoverage` increases each tick
- [ ] `ClearAfterSnow_Melts` — coverage decreases when weather clears
- [ ] `TrackReducesCoverage` — character movement reduces local value

### 13.11 — UI Tests

File: `tests/Oravey2.UITests/Weather/WeatherTests.cs`

- [ ] `ToggleRain_ParticlesVisible` — enable rain in debug menu, screenshot shows rain streaks over terrain
- [ ] `ToggleSnow_TerrainWhitens` — enable snow, wait for accumulation, terrain colour shifts toward white
- [ ] `DayNightCycle_LightingChanges` — set time to noon, screenshot, set time to midnight, second screenshot differs in brightness

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Weather."
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~Weather"
```

**User test:** Open debug menu. Select "Rain" from weather dropdown, set intensity to 0.8. Rain particles fall over the terrain; the ground becomes darker/wetter. Switch to "Snow" — watch the transition as rain fades and snowflakes appear. After 30 seconds, terrain turns white. Set time to midnight — lighting goes dark blue with moonlight.
