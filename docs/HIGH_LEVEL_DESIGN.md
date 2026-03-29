# Oravey2 — Post-Apocalyptic Isometric RPG

## High-Level Design Document

---

## 1. Game Overview

**Title:** Oravey2 (working title)
**Genre:** Post-Apocalyptic RPG
**Perspective:** Top-down isometric
**Engine:** Stride 3D (formerly Xenko)
**Language:** C#
**Target Platforms:** Windows, iOS, Android

**Elevator Pitch:**
A top-down isometric RPG set in a shattered world after civilisation's collapse. Players explore ruined cities, irradiated wastelands, and makeshift settlements — scavenging resources, forging alliances, and making choices that reshape what's left of humanity.

---

## 2. Target Platforms & Requirements

| Platform | Min Target | Notes |
|----------|-----------|-------|
| Windows | Windows 10+ x64 | Primary dev platform, keyboard + mouse + gamepad |
| iOS | iOS 15+ (iPhone 11+) | Touch controls, Metal rendering |
| Android | Android 10+ (API 29) | Touch controls, Vulkan / OpenGL ES 3.1 |

### Cross-Platform Strategy

- **Stride 3D** compiles to each platform via its multi-platform pipeline.
- Shared C# game logic in a **platform-agnostic core assembly**.
- Platform-specific projects for input, rendering backend, and store integration.
- Adaptive UI layer: physical controls on desktop, virtual joystick + tap on mobile.

```
Oravey2/
├── Oravey2.Core/          # Shared game logic, ECS components, systems
├── Oravey2.Windows/       # Windows launcher & platform services
├── Oravey2.iOS/           # iOS launcher & platform services
├── Oravey2.Android/       # Android launcher & platform services
├── Oravey2.Assets/        # Shared art, audio, data assets
└── Oravey2.Tests/         # Unit & integration tests
```

---

## 3. Architecture Overview

### 3.1 Layered Architecture

```
┌─────────────────────────────────────────────────┐
│                  Presentation                    │
│  (Stride Rendering, UI, Camera, VFX, Audio)      │
├─────────────────────────────────────────────────┤
│                  Game Systems                     │
│  (Combat, Dialogue, Quests, AI, Crafting,        │
│   Inventory, World Events)                       │
├─────────────────────────────────────────────────┤
│                  Core / ECS                       │
│  (Entity-Component-System, State Machine,        │
│   Event Bus, Serialization, Save/Load)           │
├─────────────────────────────────────────────────┤
│                  Platform                         │
│  (Input Abstraction, File I/O, Networking,       │
│   Store/IAP, Notifications)                      │
└─────────────────────────────────────────────────┘
```

### 3.2 Entity-Component-System (ECS)

Stride's built-in ECS is the backbone. All game objects are **entities** composed of reusable **components**, processed by **systems** (processors).

| Layer | Examples |
|-------|----------|
| **Components** | `HealthComponent`, `InventoryComponent`, `FactionComponent`, `AIBehaviorComponent`, `DialogueComponent` |
| **Processors/Systems** | `CombatProcessor`, `AIProcessor`, `QuestProcessor`, `DayNightCycleProcessor`, `RadiationProcessor` |
| **Services** | `SaveService`, `AudioService`, `LocalizationService`, `AnalyticsService` |

### 3.3 Key Design Patterns

- **Service Locator** — global services (audio, save, input) registered at startup and retrieved via a lightweight locator.
- **Event Bus** — decoupled communication between systems (e.g., `OnPlayerEnterZone`, `OnNpcDeath`, `OnQuestUpdated`).
- **State Machine** — player states (Exploring, InCombat, InDialogue, InMenu) and AI states (Idle, Patrol, Alert, Attack, Flee).
- **Data-Driven Design** — quests, items, dialogues, and world events defined in JSON/YAML data files, not hard-coded.

---

## 4. Isometric Camera & Rendering

### 4.1 Camera

- Fixed-angle isometric camera: **~30° pitch, 45° yaw rotation**.
- Orthographic or near-orthographic projection for a clean isometric look.
- Smooth follow on the player entity with configurable deadzone.
- Zoom levels (close / medium / far) — clamped for mobile performance.
- Optional camera rotation in 90° snaps (Q/E keys, two-finger rotate on mobile).

### 4.2 Rendering Pipeline

| Feature | Approach |
|---------|----------|
| Lighting | Baked lightmaps for static environments; dynamic lights for torches, explosions, radiation glow |
| Shadows | Cascaded shadow maps on desktop; simplified blob shadows on mobile |
| Post-Processing | Desaturated palette, film grain, vignette, bloom (toned down on mobile) |
| LOD | Automatic mesh LOD + distance-based sprite swap for far objects on mobile |
| Tile System | Modular tile-based world built from prefab chunks |

### 4.3 Art Direction

- Muted, desaturated colour palette with pops of colour for interactables and radiation zones.
- Hand-modelled low-poly 3D assets with stylised textures.
- Ruin aesthetic: crumbling concrete, overgrown vegetation, makeshift structures.
- Weather: dust storms, acid rain, fog — all affecting visibility and gameplay.

---

## 5. Core Game Systems

### 5.1 World & Exploration

- **Tile-based world map** composed of modular chunks loaded/unloaded via streaming.
- **Zones:** Ruined cities, irradiated wastelands, underground bunkers, survivor settlements, industrial ruins.
- **Points of Interest:** Lootable buildings, random encounters, quest locations, hidden caches.
- **Day/Night Cycle:** Affects NPC schedules, enemy spawns, visibility, and radiation intensity.
- **Fast Travel:** Unlocked by discovering locations; costs in-game time and resources.

### 5.2 Character System

```
CharacterEntity
├── StatsComponent        // Strength, Perception, Endurance, Charisma, Intelligence, Agility, Luck
├── HealthComponent       // HP, radiation level, status effects (poisoned, bleeding, irradiated)
├── InventoryComponent    // Weight-limited bag, equipped gear slots
├── SkillsComponent       // Firearms, Melee, Survival, Science, Speech, Stealth, Mechanics
├── PerkTreeComponent     // Unlock perks at level-up thresholds
├── FactionComponent      // Reputation with each faction
└── QuestLogComponent     // Active, completed, failed quests
```

- **Levelling:** XP-based. Level-ups grant stat points and perk unlocks.
- **Skills** improve through use (hybrid use-based + point-allocation system).

### 5.3 Combat

- **Real-time with pause (RTwP)** — action flows in real time; player can pause to issue commands.
- **Action Point (AP) system** — each action (attack, reload, use item, move) costs AP; AP regenerates over time.
- **Cover system** — entities behind objects receive damage reduction and to-hit penalties for attackers.
- **Damage model** — weapon damage × hit location modifier × armour reduction × status effects.
- **Enemy AI states:** Idle → Alert → Engage → Flee/Retreat. Uses utility-based AI for decision-making.

### 5.4 Dialogue & Quests

- **Branching dialogue trees** loaded from data files (JSON/YAML).
- **Skill checks** in dialogue — visible or hidden depending on game settings.
- **Consequences** — dialogue choices affect faction reputation, quest progression, and world state.
- **Quest types:** Main storyline, faction quests, side quests, radiant/procedural quests.
- **Quest engine** driven by a condition/action graph:
  - Conditions: `HasItem`, `FactionRep >=`, `QuestStageComplete`, `PlayerLevel >=`
  - Actions: `GiveItem`, `SpawnEntity`, `UpdateJournal`, `TriggerEvent`, `ModifyFaction`

### 5.5 Inventory & Crafting

- **Weight-based inventory** — carry capacity derived from Strength stat.
- **Equipment slots:** Head, Torso, Legs, Feet, Primary Weapon, Secondary Weapon, Accessory × 2.
- **Crafting stations:** Workbench (weapons/armour), Chem Lab (drugs/medicine), Cooking Fire (food/drink).
- **Recipe system:** Recipes discovered via schematics, experimentation, or NPC teaching.
- **Item degradation** — weapons and armour degrade with use; repaired at workbenches or by NPCs.

### 5.6 Faction & Reputation

- Multiple factions with independent reputation tracks (e.g., -100 to +100).
- Actions, dialogue, and quest outcomes shift reputation.
- Reputation gates: vendor access, quest availability, safe passage, hostile encounters.
- End-game outcomes shaped by faction standings.

### 5.7 Survival Mechanics (Optional Layer)

- **Hunger/Thirst/Fatigue** as toggleable difficulty modifier.
- **Radiation exposure** — accumulates in irradiated zones, causes debuffs, cured with items.
- **Scavenging** — search containers and environmental objects for components.

---

## 6. AI System

### 6.1 NPC AI

```
┌───────────┐
│  Utility   │  Scores each possible action, picks highest
│  Selector  │
├───────────┤
│  Actions   │  Patrol, Guard, Investigate, Attack, Flee, Interact, Trade
├───────────┤
│  Sensors   │  Sight (cone), Hearing (radius), Faction awareness
├───────────┤
│  Blackboard│  Shared knowledge: last known player pos, threat level, alerts
└───────────┘
```

- **Utility-based AI** for enemies: each action scored by context (health, distance, ammo, allies nearby).
- **Schedule-based AI** for civilians: daily routines (wake, work, eat, sleep) driven by day/night cycle.
- **Pathfinding:** A* on nav-mesh with obstacle avoidance; chunked nav-mesh per zone.

---

## 7. UI / UX

### 7.1 HUD

- Minimal HUD: health bar, AP bar, minimap, active quest tracker, quick-slot bar.
- Auto-hide non-essential UI when nothing is happening.
- Radial menu for quick actions on mobile (hold-tap).

### 7.2 Menus

- Character sheet, inventory, quest log, map, crafting, dialogue — all full-screen overlays.
- Consistent back/close gesture on all platforms.
- Scalable UI anchored to safe areas on mobile (notch-aware).

### 7.3 Input Abstraction

```csharp
public interface IInputProvider
{
    Vector2 MovementAxis { get; }
    bool ActionPressed(GameAction action);
    bool ActionHeld(GameAction action);
    Vector2 PointerPosition { get; }
    // ...
}
```

| Platform | Implementation |
|----------|---------------|
| Windows | `KeyboardMouseInputProvider`, `GamepadInputProvider` |
| iOS/Android | `TouchInputProvider` (virtual joystick, tap-to-interact, gestures) |

---

## 8. Data Pipeline

### 8.1 Asset Pipeline

- 3D models: FBX → Stride asset compiler → optimised per platform.
- Textures: PNG/PSD → compressed (BC7 desktop, ASTC mobile).
- Audio: WAV → OGG (desktop), AAC (mobile).
- Data files: JSON/YAML → deserialised at load into C# POCOs, cached in memory.

### 8.2 Save System

- **Serialization:** Binary or MessagePack for speed; JSON for debug builds.
- **Save data:** Player state, world state (modified tiles, dead NPCs, container contents), quest state, time.
- **Auto-save** on zone transitions and at configurable intervals.
- **Cloud sync** via platform services (iCloud, Google Play Games, Steam Cloud).

---

## 9. Audio

| Category | Examples |
|----------|---------|
| Ambient | Wind, distant thunder, creaking metal, wildlife, Geiger counter |
| Music | Dynamic layered soundtrack — tension layer adds in combat, eerie layer in ruins |
| SFX | Gunshots, melee impacts, footsteps (surface-specific), UI clicks |
| Voice | Key dialogue lines voiced; secondary dialogue text-only |

- **Adaptive music system:** Stride's audio engine + custom `MusicStateProcessor` that crossfades layers based on game state.

---

## 10. Performance Budget (Mobile)

| Metric | Target |
|--------|--------|
| Frame Rate | 30 fps stable (60 fps on modern devices) |
| Draw Calls | < 200 per frame |
| Triangles | < 150K visible per frame |
| Memory | < 1.5 GB RAM |
| App Size | < 2 GB install (with asset packs for additional content) |
| Battery | > 3 hours continuous play on mid-range device |

### Optimisation Strategies

- Hybrid LOD: mesh LOD + billboard imposters at distance.
- Aggressive culling: frustum + occlusion (isometric camera helps).
- Texture atlasing for tiles and common objects.
- Object pooling for projectiles, VFX, and spawned entities.
- Chunked world streaming — only 3×3 grid of chunks loaded around player.
- Reduced post-processing pipeline on mobile (no SSAO, simplified bloom).

---

## 11. Networking (Future Phase)

- **Architecture:** Optional co-op (2-4 players), client-authoritative with anti-cheat validation.
- **Protocol:** Lightweight UDP for gameplay, TCP for chat/save sync.
- **Scope:** Co-op exploration and combat; quest progress synced to host.
- **Noted as Phase 2** — single-player first, multiplayer layered on.

---

## 12. Project Milestones

| Milestone | Deliverables |
|-----------|-------------|
| **M0 — Prototype** | Isometric camera, player movement, tile-based map loading, basic combat loop (Windows only) |
| **M1 — Vertical Slice** | One complete zone with exploration, combat, dialogue, and a short quest chain. Basic UI. Windows + Android. |
| **M2 — Core Loop** | Character progression, inventory, crafting, 3+ factions, day/night cycle, save/load. All platforms. |
| **M3 — Content Alpha** | Main storyline first act, 5+ zones, full AI, full UI/UX, audio pass. |
| **M4 — Beta** | Full content, balancing, performance optimisation, platform-specific polish, localisation. |
| **M5 — Release** | Store submissions, launch marketing, day-one patch pipeline. |

---

## 13. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Stride mobile maturity | Mobile build pipeline may need patches | Evaluate early in M0; maintain fallback to MonoGame renderer if needed |
| Performance on low-end Android | Frame drops, heat | Aggressive LOD, quality presets, chunk streaming from M0 |
| Scope creep | Endless feature additions | Data-driven design; content is data, not code. Feature-freeze at M3. |
| Cross-platform input feel | Touch controls feel clunky | Dedicated UX pass per platform; external playtesting at M1 |
| Save compatibility across updates | Players lose progress | Versioned save format with migration scripts |

---

## 14. Technology Stack Summary

| Component | Technology |
|-----------|-----------|
| Engine | Stride 3D 4.x |
| Language | C# (.NET 8+) |
| IDE | Visual Studio / Rider + Stride Game Studio |
| Build | MSBuild, Stride asset compiler, platform SDKs (Xcode, Android SDK) |
| Data Format | JSON (dialogue, quests, items), YAML (config), MessagePack (saves) |
| Version Control | Git + LFS for binary assets |
| CI/CD | GitHub Actions or Azure DevOps — build all platforms on push |
| Testing | xUnit (unit), Stride test framework (integration), manual playtesting |

---

*This document is a living reference. Update it as design decisions are validated or revised during development.*
