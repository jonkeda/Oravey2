# Meshy AI Integration Plan

## Overview

Integrate [Meshy AI](https://meshy.ai/) into **Oravey2.MapGen.App** to generate 3D models for the game directly from text/image prompts. Two new tabs in the MAUI app:

| Tab | Purpose | Pipeline |
|-----|---------|----------|
| **Houses** | Generate building/structure models | Text/Image → 3D → (Remesh) → Export |
| **Figures** | Generate character models with animation | Text/Image → 3D → Rig → Animate → Export |

Reference implementation: [meshy-ai-mcp-server](https://github.com/pasie15/meshy-ai-mcp-server)

---

## Phase 1 — Meshy Client Library (`Oravey2.MapGen`)

Add a C# HTTP client for the Meshy REST API inside the existing `Oravey2.MapGen` class library.

### 1.1 — `MeshyClient` Service

| Item | Detail |
|------|--------|
| Location | `src/Oravey2.MapGen/Services/MeshyClient.cs` |
| Pattern | Matches existing `MapGeneratorService` — sealed class, `IAsyncDisposable`, injected via DI |
| Auth | Bearer token from `SecureStorage` (same pattern as BYOK ApiKey) |
| Base URL | `https://api.meshy.ai/openapi` (configurable) |
| HTTP | `HttpClient` with `System.Net.Http.Json` — no external packages needed |
| Timeout | 5 min default for streaming, 30 s for CRUD |

**Core methods:**

```csharp
public sealed class MeshyClient : IAsyncDisposable
{
    // Text-to-3D
    Task<MeshyTaskResponse> CreateTextTo3DAsync(TextTo3DRequest req, CancellationToken ct);
    Task<MeshyTaskStatus>   GetTaskStatusAsync(string taskId, CancellationToken ct);

    // Image-to-3D
    Task<MeshyTaskResponse> CreateImageTo3DAsync(ImageTo3DRequest req, CancellationToken ct);

    // Remesh
    Task<MeshyTaskResponse> CreateRemeshAsync(RemeshRequest req, CancellationToken ct);

    // Rigging
    Task<MeshyTaskResponse> CreateRiggingAsync(RiggingRequest req, CancellationToken ct);

    // Animation
    Task<MeshyTaskResponse> CreateAnimationAsync(AnimationRequest req, CancellationToken ct);

    // Streaming (SSE)
    IAsyncEnumerable<MeshyTaskStatus> StreamTaskAsync(string endpoint, string taskId, CancellationToken ct);

    // Utility
    Task<MeshyBalance> GetBalanceAsync(CancellationToken ct);
}
```

### 1.2 — Request/Response Models

| File | Models |
|------|--------|
| `Models/Meshy/TextTo3DRequest.cs` | `mode`, `prompt`, `art_style`, `should_remesh` |
| `Models/Meshy/ImageTo3DRequest.cs` | `image_url`, `prompt`, `art_style` |
| `Models/Meshy/RemeshRequest.cs` | `input_task_id`, `target_formats`, `topology`, `target_polycount` |
| `Models/Meshy/RiggingRequest.cs` | `input_task_id` + passthrough properties |
| `Models/Meshy/AnimationRequest.cs` | `action_id` + passthrough properties |
| `Models/Meshy/MeshyTaskStatus.cs` | `id`, `status` (PENDING/IN_PROGRESS/SUCCEEDED/FAILED), `progress`, `model_urls`, `thumbnail_url` |
| `Models/Meshy/MeshyBalance.cs` | `balance` |

All models are **sealed records** with `System.Text.Json` serialization (snake_case via `JsonPropertyName`).

### 1.3 — Progress Reporting

Reuse the existing `event Action<T>` pattern from `MapGeneratorService`:

```csharp
public sealed record MeshyProgress(MeshyPhase Phase, string Message, int? PercentComplete);

public enum MeshyPhase
{
    Submitting,
    Pending,
    Processing,
    Downloading,
    Complete,
    Error
}
```

---

## Phase 2 — House Generation Screen (`HouseGeneratorView`)

### 2.1 — ViewModel: `HouseGeneratorViewModel`

| Property | Type | Binding |
|----------|------|---------|
| `Prompt` | `string` | Two-way Entry |
| `ImageUrl` | `string` | Two-way Entry (optional) |
| `ArtStyle` | `string` | Picker (realistic, cartoon, low-poly, sculpture, pbr) |
| `ShouldRemesh` | `bool` | Switch |
| `TargetPolycount` | `int` | Entry (default 10000) |
| `IsGenerating` | `bool` | Controls button/spinner |
| `Progress` | `string` | Streaming log label |
| `PreviewThumbnail` | `string` | Thumbnail URL → Image |
| `DownloadUrls` | `Dictionary<string,string>` | GLB/FBX/OBJ links |
| `GenerateCommand` | `ICommand` | Triggers pipeline |
| `CancelCommand` | `ICommand` | Cancels CTS |
| `DownloadCommand` | `ICommand` | Downloads GLB to export path |

**Pipeline logic:**

```
1. User fills prompt (+ optional image URL)
2. GenerateCommand →
   a. If ImageUrl set → CreateImageTo3DAsync
      Else            → CreateTextTo3DAsync(mode: "refine", prompt, art_style)
   b. StreamTaskAsync → update Progress + PreviewThumbnail
   c. If ShouldRemesh → CreateRemeshAsync(task_id, target_polycount)
      → StreamTaskAsync → update Progress
   d. Set DownloadUrls from final task status
3. DownloadCommand → HttpClient.GetByteArrayAsync(glb_url) → save to ExportPath
```

### 2.2 — View: `HouseGeneratorView.xaml`

Standard MAUI ContentPage matching existing tabs:

```
┌─────────────────────────────────────────┐
│  Prompt: [________________________]     │
│  Image URL (optional): [__________]     │
│  Art Style: [Picker ▼]                  │
│  ☐ Remesh after generation              │
│     Target polycount: [10000]           │
│                                         │
│  [Generate]  [Cancel]                   │
│                                         │
│  ┌─────────────┐  Status: Processing... │
│  │  Thumbnail   │  Progress: 65%        │
│  │  Preview     │                       │
│  └─────────────┘                        │
│                                         │
│  ── Log ──────────────────────────────  │
│  Submitting text-to-3d task...          │
│  Task abc123 created, streaming...      │
│  Progress: 65% ...                      │
│                                         │
│  [Download GLB]  [Download FBX]         │
└─────────────────────────────────────────┘
```

---

## Phase 3 — Figure Generation Screen (`FigureGeneratorView`)

### 3.1 — ViewModel: `FigureGeneratorViewModel`

Same base properties as House plus:

| Property | Type | Binding |
|----------|------|---------|
| `ShouldRig` | `bool` | Switch (default true) |
| `ShouldAnimate` | `bool` | Switch |
| `AnimationActionId` | `string` | Entry / Picker for animation presets |

**Pipeline logic:**

```
1. User fills prompt (+ optional image URL) for character
2. GenerateCommand →
   a. Text-to-3D or Image-to-3D → stream to completion
   b. If ShouldRig → CreateRiggingAsync(task_id)
      → StreamTaskAsync → update Progress
   c. If ShouldAnimate → CreateAnimationAsync(action_id)
      → StreamTaskAsync → update Progress
   d. Set DownloadUrls from final task status
3. DownloadCommand → save to ExportPath
```

### 3.2 — View: `FigureGeneratorView.xaml`

```
┌─────────────────────────────────────────┐
│  Prompt: [________________________]     │
│  Image URL (optional): [__________]     │
│  Art Style: [Picker ▼]                  │
│                                         │
│  ☑ Auto-Rig after generation            │
│  ☐ Animate after rigging                │
│     Animation: [Picker ▼ / action_id]   │
│                                         │
│  [Generate]  [Cancel]                   │
│                                         │
│  ┌─────────────┐  Status: Rigging...    │
│  │  Thumbnail   │  Progress: 100% (3D)  │
│  │  Preview     │  Rigging: 40%         │
│  └─────────────┘                        │
│                                         │
│  ── Log ──────────────────────────────  │
│  ...                                    │
│                                         │
│  [Download GLB]  [Download FBX]         │
└─────────────────────────────────────────┘
```

---

## Phase 4 — Integration & Wiring

### 4.1 — DI Registration (`MauiProgram.cs`)

```csharp
// Meshy
builder.Services.AddSingleton<MeshyClient>();
builder.Services.AddTransient<HouseGeneratorViewModel>();
builder.Services.AddTransient<FigureGeneratorViewModel>();
```

### 4.2 — Navigation (`MainPage.xaml`)

Add two new tabs to the existing TabbedPage:

```xml
<views:GeneratorView Title="Generate" />
<views:BlueprintPreviewView Title="Preview" />
<views:HouseGeneratorView Title="Houses" />
<views:FigureGeneratorView Title="Figures" />
<views:SettingsView Title="Settings" />
```

### 4.3 — Settings (`SettingsView`)

Add a "Meshy AI" section to the existing Settings tab:

| Setting | Storage | Default |
|---------|---------|---------|
| Meshy API Key | `SecureStorage` | (empty) |
| Export Path | `Preferences` | `%DOCS%/Oravey2/Models` |
| Default Art Style | `Preferences` | `realistic` |

---

## Phase 5 — Testing

| Layer | What | Where |
|-------|------|-------|
| Unit | `MeshyClient` with mocked `HttpMessageHandler` | `tests/Oravey2.Tests/MeshyClientTests.cs` |
| Unit | Request model serialization round-trips | `tests/Oravey2.Tests/MeshyModelTests.cs` |
| Unit | ViewModel state transitions (generate → progress → complete) | `tests/Oravey2.Tests/HouseGeneratorViewModelTests.cs` |
| UI | Tab navigation, field entry, button state | `tests/Oravey2.UITests/` (Brinell.Stride) |

---

## File Inventory (New Files)

```
src/Oravey2.MapGen/
├── Models/Meshy/
│   ├── TextTo3DRequest.cs
│   ├── ImageTo3DRequest.cs
│   ├── RemeshRequest.cs
│   ├── RiggingRequest.cs
│   ├── AnimationRequest.cs
│   ├── MeshyTaskStatus.cs
│   ├── MeshyBalance.cs
│   └── MeshyProgress.cs
└── Services/
    └── MeshyClient.cs

src/Oravey2.MapGen.App/
├── ViewModels/
│   ├── HouseGeneratorViewModel.cs
│   └── FigureGeneratorViewModel.cs
└── Views/
    ├── HouseGeneratorView.xaml(.cs)
    └── FigureGeneratorView.xaml(.cs)

tests/Oravey2.Tests/
├── MeshyClientTests.cs
├── MeshyModelTests.cs
└── HouseGeneratorViewModelTests.cs
```

---

## Dependencies

| Package | Version | Why |
|---------|---------|-----|
| (none new) | — | `System.Net.Http` + `System.Text.Json` are already in .NET 10 BCL |

No new NuGet packages required. The Meshy API is plain REST + SSE.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| API key leaked in source | High | Use `SecureStorage` (same as BYOK key) |
| Long generation times (2-5 min) | Medium | SSE streaming + progress bar + cancel support |
| API rate limits / balance exhaustion | Medium | Show balance on Settings, check before generate |
| GLB import into Stride | Medium | Stride supports glTF/GLB natively; test with sample models first |
| Animation library IDs unknown | Low | Start with passthrough `action_id` field; add picker in future phase |
