# Meshy AI Integration — Architecture

## System Context

```
┌───────────────────────────────────────────────────────┐
│                   MapGen.App (MAUI)                    │
│                                                        │
│  ┌──────────┐ ┌──────────┐ ┌────────┐ ┌────────────┐ │
│  │ Generate  │ │ Preview  │ │ Houses │ │  Figures   │ │
│  │   Tab     │ │   Tab    │ │  Tab   │ │    Tab     │ │
│  └────┬─────┘ └────┬─────┘ └───┬────┘ └─────┬──────┘ │
│       │             │           │             │        │
│  ┌────┴─────┐ ┌────┴─────┐ ┌──┴──────┐ ┌───┴──────┐ │ ◄─ ViewModels
│  │Generator │ │Blueprint │ │ House   │ │ Figure   │ │
│  │ViewModel │ │Preview VM│ │Gen VM   │ │Gen VM    │ │
│  └────┬─────┘ └──────────┘ └──┬──────┘ └───┬──────┘ │
│       │                        │             │        │
│  ┌────┴────────────────┐  ┌───┴─────────────┴──┐    │ ◄─ Services
│  │ MapGeneratorService │  │    MeshyClient      │    │
│  │  (Copilot SDK)      │  │  (HttpClient/REST)  │    │
│  └─────────────────────┘  └─────────┬───────────┘    │
│                                      │                │
└──────────────────────────────────────┼────────────────┘
                                       │ HTTPS
                                       ▼
                            ┌─────────────────────┐
                            │   Meshy AI API      │
                            │ api.meshy.ai/openapi│
                            └─────────────────────┘
```

---

## Component Design

### MeshyClient

Lives in the **Oravey2.MapGen** library (not the App) so it can be unit-tested without MAUI.

```
Oravey2.MapGen/
└── Services/
    └── MeshyClient.cs          ← Sealed, IAsyncDisposable
        ├── HttpClient           ← Injected or owned
        ├── ApiKey               ← From constructor (app reads SecureStorage)
        ├── BaseUrl              ← Configurable
        ├── POST methods         ← Create tasks
        ├── GET methods          ← Retrieve/list tasks
        └── StreamTaskAsync()    ← IAsyncEnumerable<MeshyTaskStatus> via SSE
```

**Key decisions:**
- `IAsyncEnumerable<MeshyTaskStatus>` for streaming (natural C# async iteration)
- `CancellationToken` on every method (matches existing pattern)
- No retry logic in v1 — caller can retry
- `JsonSerializerOptions` with `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`

### ViewModels

Both `HouseGeneratorViewModel` and `FigureGeneratorViewModel` extend `BaseViewModel`.

```
BaseViewModel (existing)
├── SetProperty<T>()
├── IsBusy
│
├── HouseGeneratorViewModel
│   ├── Prompt, ImageUrl, ArtStyle, ShouldRemesh, TargetPolycount
│   ├── IsGenerating, Progress, PreviewThumbnail, DownloadUrls
│   ├── GenerateCommand → RunHousePipeline()
│   ├── CancelCommand → _cts.Cancel()
│   └── DownloadCommand → SaveGlbToExportPath()
│
└── FigureGeneratorViewModel
    ├── (same base props)
    ├── ShouldRig, ShouldAnimate, AnimationActionId
    ├── GenerateCommand → RunFigurePipeline()
    ├── CancelCommand
    └── DownloadCommand
```

---

## Pipelines

### House Pipeline

```
┌────────┐    ┌───────────────┐    ┌──────────┐    ┌──────────┐
│ Prompt │───►│ Text-to-3D    │───►│ Stream   │───►│ Complete │
│  or    │    │ or Image-to-3D│    │ Progress │    │          │
│ Image  │    └───────────────┘    └────┬─────┘    └────┬─────┘
└────────┘                              │               │
                                        │ if remesh     │
                                        ▼               │
                                   ┌──────────┐         │
                                   │ Remesh   │─────────┘
                                   │ (opt.)   │
                                   └──────────┘
                                        │
                                        ▼
                                   ┌──────────┐
                                   │ Download │
                                   │ GLB/FBX  │
                                   └──────────┘
```

### Figure Pipeline

```
┌────────┐    ┌───────────────┐    ┌──────────┐    ┌──────────┐
│ Prompt │───►│ Text-to-3D    │───►│ Stream   │───►│ 3D Done  │
│  or    │    │ or Image-to-3D│    │ Progress │    │          │
│ Image  │    └───────────────┘    └──────────┘    └────┬─────┘
└────────┘                                              │
                                                        │ if rig
                                                        ▼
                                                   ┌──────────┐
                                                   │ Rigging  │
                                                   │ Auto-rig │
                                                   └────┬─────┘
                                                        │
                                                        │ if animate
                                                        ▼
                                                   ┌──────────┐
                                                   │ Animate  │
                                                   │ action_id│
                                                   └────┬─────┘
                                                        │
                                                        ▼
                                                   ┌──────────┐
                                                   │ Download │
                                                   │ GLB/FBX  │
                                                   └──────────┘
```

---

## SSE Streaming Implementation

The MCP server reads SSE via `ReadableStream`; our C# client uses `HttpClient` + `Stream`:

```csharp
public async IAsyncEnumerable<MeshyTaskStatus> StreamTaskAsync(
    string path,
    string taskId,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var url = $"{_baseUrl}{path}/{taskId}/stream";
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Accept.Add(new("text/event-stream"));
    request.Headers.Authorization = new("Bearer", _apiKey);

    using var response = await _http.SendAsync(
        request, HttpCompletionOption.ResponseHeadersRead, ct);
    response.EnsureSuccessStatusCode();

    using var stream = await response.Content.ReadAsStreamAsync(ct);
    using var reader = new StreamReader(stream);

    while (!reader.EndOfStream && !ct.IsCancellationRequested)
    {
        var line = await reader.ReadLineAsync(ct);
        if (line is null || !line.StartsWith("data:")) continue;

        var json = line["data:".Length..].Trim();
        var status = JsonSerializer.Deserialize<MeshyTaskStatus>(json, _jsonOptions);
        if (status is null) continue;

        yield return status;

        if (status.Status is "SUCCEEDED" or "FAILED" or "CANCELED")
            yield break;
    }
}
```

**Consumer in ViewModel:**

```csharp
await foreach (var status in _meshyClient.StreamTaskAsync("/v2/text-to-3d", taskId, _cts.Token))
{
    Progress = $"{status.Status}: {status.Progress}%";
    PreviewThumbnail = status.ThumbnailUrl;

    if (status.Status == "SUCCEEDED")
        DownloadUrls = status.ModelUrls;
}
```

---

## DI & Wiring

### MauiProgram.cs additions

```csharp
// Meshy HTTP client
builder.Services.AddSingleton<MeshyClient>(sp =>
{
    // ApiKey loaded from SecureStorage at app startup
    var apiKey = SecureStorage.Default.GetAsync("MeshyApiKey").Result ?? "";
    return new MeshyClient(apiKey);
});

// ViewModels
builder.Services.AddTransient<HouseGeneratorViewModel>();
builder.Services.AddTransient<FigureGeneratorViewModel>();
```

### MainPage.xaml additions

```xml
<views:HouseGeneratorView Title="Houses" />
<views:FigureGeneratorView Title="Figures" />
```

### Settings additions

Meshy API Key stored/retrieved via `SecureStorage` (same pattern as existing BYOK key).

---

## Output Format & Stride Compatibility

| Format | Stride Support | Use Case |
|--------|---------------|----------|
| **GLB** | Native (glTF binary) | Primary — single-file, includes textures |
| FBX | Via asset compiler | Alternative for rigged/animated models |
| OBJ | Via asset compiler | Fallback, no animation support |

**Recommended flow:**
1. Download GLB from Meshy
2. Place in `Assets/Models/Houses/` or `Assets/Models/Figures/`
3. Stride asset compiler processes on next build
4. Reference in game code via `Content.Load<Model>("Models/Houses/medieval_house")`

---

## Error Handling

| Scenario | Handling |
|----------|----------|
| No API key | Disable Generate button, show prompt to set key in Settings |
| Task FAILED | Show `task_error` message in log, enable Retry |
| Network error | Catch `HttpRequestException`, show message, enable Retry |
| Stream timeout (5 min) | `OperationCanceledException` → show timeout message |
| User cancel | `_cts.Cancel()` → clean `OperationCanceledException` handling |
| Insufficient balance | Check balance before generate, warn if low |

---

## Security

| Concern | Mitigation |
|---------|------------|
| API key storage | `SecureStorage` (Windows DPAPI) — never in plaintext/source |
| API key in HTTP | HTTPS only (Meshy enforces TLS) |
| User-supplied image URLs | Passed directly to Meshy API — no local file access |
| Downloaded models | Saved to user-chosen export path, no auto-execution |
