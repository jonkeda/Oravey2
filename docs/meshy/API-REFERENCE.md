# Meshy AI — API Reference

Base URL: `https://api.meshy.ai/openapi`
Auth: `Authorization: Bearer <MESHY_API_KEY>`

Source: [meshy-ai-mcp-server](https://github.com/pasie15/meshy-ai-mcp-server/blob/main/src/index.ts) + [Meshy Docs](https://docs.meshy.ai/)

---

## Common Patterns

### Task Lifecycle

All generation endpoints follow the same pattern:

```
POST /create  →  { "result": "<task_id>" }
GET  /retrieve →  { "id", "status", "progress", "model_urls", ... }
GET  /list     →  paginated array
GET  /stream   →  SSE until status ∈ {SUCCEEDED, FAILED, CANCELED}
```

### Task Status Values

| Status | Meaning |
|--------|---------|
| `PENDING` | Task queued, not started |
| `IN_PROGRESS` | Actively processing |
| `SUCCEEDED` | Complete — `model_urls` populated |
| `FAILED` | Error — check `task_error` |
| `CANCELED` | User-cancelled |

### Streaming (SSE)

All `/stream` endpoints return `text/event-stream`:

```
GET /v2/text-to-3d/{task_id}/stream
Accept: text/event-stream

data: {"id":"abc","status":"IN_PROGRESS","progress":45,...}
data: {"id":"abc","status":"SUCCEEDED","progress":100,"model_urls":{...},...}
```

Stream terminates when `status` is `SUCCEEDED`, `FAILED`, or `CANCELED`.

---

## 1. Text-to-3D (v2)

### Create Task

```
POST /v2/text-to-3d
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `mode` | string | Yes | `"preview"` (fast, lower quality) or `"refine"` (high quality) |
| `prompt` | string | Yes | Text description of the 3D model |
| `art_style` | string | No | Style hint: `realistic`, `cartoon`, `low-poly`, `sculpture`, `pbr` |
| `should_remesh` | bool | No | Auto-remesh after generation |

**Response:** `{ "result": "<task_id>" }`

### Retrieve Task

```
GET /v2/text-to-3d/{task_id}
```

**Response:**
```json
{
  "id": "task_abc123",
  "status": "SUCCEEDED",
  "progress": 100,
  "model_urls": {
    "glb": "https://...",
    "fbx": "https://...",
    "obj": "https://..."
  },
  "thumbnail_url": "https://...",
  "prompt": "a medieval stone house",
  "art_style": "realistic",
  "created_at": "2024-01-01T00:00:00Z",
  "finished_at": "2024-01-01T00:02:30Z"
}
```

### List Tasks

```
GET /v2/text-to-3d?page_size=10&page=1
```

### Stream Task

```
GET /v2/text-to-3d/{task_id}/stream
```

---

## 2. Image-to-3D (v1)

### Create Task

```
POST /v1/image-to-3d
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `image_url` | string | Yes | URL of the reference image |
| `prompt` | string | No | Additional text guidance |
| `art_style` | string | No | Style hint |

**Response:** `{ "result": "<task_id>" }`

### Retrieve / List / Stream

Same pattern as Text-to-3D:
- `GET /v1/image-to-3d/{task_id}`
- `GET /v1/image-to-3d?page_size=10&page=1`
- `GET /v1/image-to-3d/{task_id}/stream`

---

## 3. Text-to-Texture (v1)

### Create Task

```
POST /v1/text-to-texture
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `model_url` | string | Yes | URL of the 3D model to texture |
| `object_prompt` | string | Yes | What the object is |
| `style_prompt` | string | No | Texture style description |
| `enable_original_uv` | bool | No | Keep original UV mapping |
| `enable_pbr` | bool | No | Generate PBR material maps |
| `resolution` | string | No | Texture resolution |
| `negative_prompt` | string | No | What to avoid |
| `art_style` | string | No | Style hint |

### Retrieve / List / Stream

- `GET /v1/text-to-texture/{task_id}`
- `GET /v1/text-to-texture?page_size=10&page=1`
- `GET /v1/text-to-texture/{task_id}/stream`

---

## 4. Remesh (v1)

### Create Task

```
POST /v1/remesh
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `input_task_id` | string | Yes | ID of the source 3D task |
| `target_formats` | string[] | No | e.g. `["glb", "fbx"]` |
| `topology` | string | No | Mesh topology type |
| `target_polycount` | int | No | Target polygon count |
| `resize_height` | number | No | Resize model height |
| `origin_at` | string | No | Origin point placement |

### Retrieve / List / Stream

- `GET /v1/remesh/{task_id}`
- `GET /v1/remesh?page_size=10&page=1`
- `GET /v1/remesh/{task_id}/stream`

---

## 5. Rigging (v1)

### Create Task

```
POST /v1/rigging
```

Body is a passthrough JSON object — consult [Meshy rigging docs](https://docs.meshy.ai/) for the full schema. Key field is typically `input_task_id` referencing the 3D model task.

### Retrieve / List / Stream

- `GET /v1/rigging/{task_id}`
- `GET /v1/rigging?page_size=10&page=1`
- `GET /v1/rigging/{task_id}/stream`

---

## 6. Animation (v1)

### Create Task

```
POST /v1/animation
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action_id` | string | Yes | Animation action from the Meshy animation library |
| *(additional)* | varies | No | Passthrough properties for the animation request |

### Retrieve / List / Stream

- `GET /v1/animation/{task_id}`
- `GET /v1/animation?page_size=10&page=1`
- `GET /v1/animation/{task_id}/stream`

---

## 7. Balance

```
GET /v1/balance
```

**Response:**
```json
{
  "balance": 4500
}
```

---

## Environment Variables (Reference)

| Variable | Default | Description |
|----------|---------|-------------|
| `MESHY_API_KEY` | *(required)* | API key from [Meshy Dashboard](https://app.meshy.ai/settings/api) |
| `MESHY_API_BASE` | `https://api.meshy.ai/openapi` | Override base URL |
| `MESHY_STREAM_TIMEOUT_MS` | `300000` (5 min) | SSE stream timeout |

---

## C# Model Mapping

For our implementation, all request/response models use `System.Text.Json` with `JsonPropertyName` for snake_case:

```csharp
public sealed record TextTo3DRequest(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("art_style")] string? ArtStyle = null,
    [property: JsonPropertyName("should_remesh")] bool? ShouldRemesh = null
);

public sealed record MeshyTaskStatus(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("progress")] int Progress,
    [property: JsonPropertyName("model_urls")] Dictionary<string, string>? ModelUrls = null,
    [property: JsonPropertyName("thumbnail_url")] string? ThumbnailUrl = null
);
```
