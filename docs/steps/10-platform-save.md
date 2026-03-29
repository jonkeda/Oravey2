# Step 10 — Platform & Save

**Goal:** Per-platform builds (Windows/iOS/Android), save/load system, cloud sync, store integration.

**Depends on:** Steps 1-9

---

## Deliverables

1. `ISaveService` interface: Save, Load, Delete, ListSaves, AutoSave
2. `SaveData` model: player state, world state (modified chunks), quest state, in-game time, settings
3. Binary serialization with MessagePack (release) / JSON (debug) — versioned format with migration support
4. Auto-save: triggers on zone transition, every N minutes (configurable), before quit
5. Save slot system: 3 manual slots + 1 auto-save + 1 quicksave
6. `Oravey2.iOS` project: iOS launcher, Metal rendering, touch input defaults, iCloud save sync
7. `Oravey2.Android` project: Android launcher, Vulkan/GLES rendering, touch input defaults, Google Play Games save sync
8. Platform service abstraction: `IPlatformServices` — file paths, cloud sync, haptics, notifications
9. Quality presets: Low / Medium / High — automatically selected per device, user-overridable
10. App lifecycle: handle backgrounding (auto-save + pause), resume, memory warnings
11. CI/CD pipeline config: build scripts for all 3 platforms (GitHub Actions / Azure DevOps)
12. Store metadata: app icons, screenshots, descriptions, age rating prep

---

## Save Format Versioning

```csharp
public class SaveHeader
{
    public int FormatVersion { get; set; }     // Incremented on breaking changes
    public string GameVersion { get; set; }     // Semantic version
    public DateTime Timestamp { get; set; }
    public string PlayerName { get; set; }
    public int PlayerLevel { get; set; }
    public TimeSpan PlayTime { get; set; }
}
```

- On load: check `FormatVersion`, run migration chain if behind current.
- Migration functions: `MigrateV1ToV2()`, `MigrateV2ToV3()`, etc.

---

## Platform Matrix

| Feature | Windows | iOS | Android |
|---------|---------|-----|---------|
| Rendering | DX11/Vulkan | Metal | Vulkan/GLES 3.1 |
| Input | KB+Mouse, Gamepad | Touch | Touch |
| Save Storage | AppData | Documents + iCloud | Internal + Google Play |
| Haptics | Gamepad rumble | Taptic Engine | Vibration API |
| Store | Steam / Microsoft Store | App Store | Google Play |
