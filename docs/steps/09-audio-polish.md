# Step 9 — Audio & Polish

**Goal:** Adaptive music, SFX, ambient audio, post-processing, visual polish.

**Depends on:** Steps 1-8

---

## Deliverables

1. `AudioService` — central audio manager: play/stop/crossfade, volume categories (Master, Music, SFX, Ambient, Voice)
2. `MusicStateProcessor` — layered adaptive music: base layer always playing, tension/combat/exploration layers crossfade based on GameState
3. `AmbientAudioProcessor` — zone-specific ambient loops (wind, industrial hum, wildlife), crossfade on zone transition
4. SFX system: pooled audio emitters, fire-and-forget API, positional audio for 3D space
5. Surface-specific footsteps: detect tile type under player, play matching footstep SFX
6. UI audio: click, hover, open/close, error, quest complete jingles
7. Post-processing pipeline: desaturation, film grain, vignette, bloom — configurable quality presets
8. Weather VFX: particle systems for dust storms, acid rain, fog volumes
9. Weather system: `WeatherProcessor` — random weather events, affects visibility + gameplay modifiers
10. Screen shake on explosions and heavy impacts
11. Damage numbers / hit indicators (floating text)
12. Mobile optimisation pass: reduced post-processing preset, audio quality scaling

---

## Music Layers

| Layer | Trigger | Fade Time |
|-------|---------|-----------|
| Base | Always | — |
| Exploration | GameState == Exploring | 2s |
| Tension | Enemies nearby (not aggro) | 1.5s |
| Combat | GameState == InCombat | 0.5s |
| Eerie | Inside irradiated zone | 3s |
