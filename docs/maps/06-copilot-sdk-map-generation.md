# Map Generation via GitHub Copilot SDK

> **Status:** Proposal  
> **Date:** 2026-04-03  
> **Depends on:** [03-map-model-llm-generation.md](03-map-model-llm-generation.md)  
> **SDK:** [github/copilot-sdk](https://github.com/github/copilot-sdk) (.NET, public preview, v0.2.0)

---

## 1. Motivation

Document 03 defines the `MapBlueprint` JSON schema and a multi-step LLM pipeline:

```
Real Geography → LLM Prompt → MapBlueprint.json → Validator → (fix loop) → Map Compiler
```

That document is model-agnostic — it doesn't specify how to call the LLM. The GitHub Copilot SDK provides a production-ready .NET integration layer that:

- Manages the LLM lifecycle (session, context, model selection)
- Supports **custom tools** the LLM can call mid-generation (asset lookup, validation, spatial checks)
- Handles iterative fix loops natively via multi-turn sessions
- Supports BYOK (Azure OpenAI, OpenAI, Anthropic) and GitHub-hosted models
- Provides streaming, telemetry, and session persistence out of the box

This eliminates the need to build custom HTTP/API client code, prompt chaining, or tool-call orchestration.

---

## 2. Architecture

```
┌──────────────────────────────────────────────────────┐
│  MapGeneratorService  (Oravey2 console/tool)         │
│                                                      │
│  ┌────────────┐    ┌──────────────────────────┐      │
│  │ GeographyDB│───▶│   Prompt Builder          │      │
│  │ AssetIndex │    │   (schema + location +    │      │
│  │ QuestDefs  │    │    constraints + assets)  │      │
│  └────────────┘    └──────────┬───────────────┘      │
│                               │                      │
│                               ▼                      │
│                    ┌──────────────────┐               │
│                    │  CopilotClient   │               │
│                    │  (Copilot SDK)   │               │
│                    └────────┬─────────┘               │
│                             │                         │
│              ┌──────────────┼──────────────┐          │
│              ▼              ▼              ▼          │
│     ┌──────────────┐ ┌───────────┐ ┌────────────┐   │
│     │validate_     │ │lookup_    │ │check_       │   │
│     │blueprint     │ │asset      │ │walkability  │   │
│     │(custom tool) │ │(custom)   │ │(custom)     │   │
│     └──────────────┘ └───────────┘ └────────────┘   │
│                             │                         │
│                             ▼                         │
│                    MapBlueprint.json                  │
│                             │                         │
│                             ▼                         │
│                      Map Compiler                     │
└──────────────────────────────────────────────────────┘
```

The Copilot SDK sits between the prompt builder and the output. The LLM can call custom tools during generation to self-validate, look up available assets, and resolve spatial conflicts — all within a single session turn rather than requiring external fix loops.

---

## 3. .NET SDK Integration

### 3.1 Package Reference

```xml
<PackageReference Include="GitHub.Copilot.SDK" Version="0.2.*" />
```

Requires: .NET 8.0+, Copilot CLI installed and in PATH.

### 3.2 Client Setup

```csharp
public sealed class MapGeneratorService : IAsyncDisposable
{
    private readonly CopilotClient _client;
    private readonly AssetRegistry _assets;
    private readonly IBlueprintValidator _validator;

    public MapGeneratorService(AssetRegistry assets, IBlueprintValidator validator)
    {
        _assets = assets;
        _validator = validator;
        _client = new CopilotClient(new CopilotClientOptions
        {
            AutoStart = true,
            LogLevel = "info",
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _client.StopAsync();
    }
}
```

### 3.3 Session Configuration

```csharp
public async Task<MapBlueprint> GenerateMapAsync(MapGenerationRequest request)
{
    await using var session = await _client.CreateSessionAsync(new SessionConfig
    {
        Model = "gpt-4.1",              // or "claude-sonnet-4.5" for spatial reasoning
        Streaming = true,
        OnPermissionRequest = PermissionHandler.ApproveAll,

        SystemMessage = new SystemMessageConfig
        {
            Mode = SystemMessageMode.Replace,
            Content = BuildSystemPrompt(),
        },

        Tools = [
            BuildValidateBlueprintTool(),
            BuildLookupAssetTool(),
            BuildCheckOverlapTool(),
            BuildCheckWalkabilityTool(),
            BuildListAvailablePrefabsTool(),
        ],
    });

    // Send the generation prompt
    var blueprint = await SendAndCollectBlueprint(session, request);
    return blueprint;
}
```

### 3.4 System Prompt

```csharp
private string BuildSystemPrompt() => """
    You are a post-apocalyptic map designer for the game Oravey2.
    You produce MapBlueprint JSON documents that conform to the map-blueprint-v1 schema.

    RULES:
    - Output ONLY valid JSON inside a ```json code fence
    - All chunk coordinates must be within the specified dimensions
    - All prefabId, meshAsset, dialogueId, lootTable values must exist —
      call the lookup_asset tool to verify before using any asset reference
    - No two buildings may have overlapping footprints —
      call check_overlap after placing buildings
    - Player start must be on a walkable tile —
      call check_walkability to verify
    - After completing the full blueprint, call validate_blueprint with the
      entire JSON to get a final validation report
    - If validation fails, fix the errors and call validate_blueprint again

    SCHEMA:
    <<< insert MapBlueprint schema from doc 03 §2 >>>
    """;
```

---

## 4. Custom Tools

The SDK's tool system lets the LLM call back into our code during generation. This replaces the external "generate → validate → feed errors back → regenerate" loop from doc 03 §8.4 with an inline, self-correcting flow.

### 4.1 Blueprint Validator Tool

```csharp
private AIFunction BuildValidateBlueprintTool()
{
    return AIFunctionFactory.Create(
        async ([Description("Complete MapBlueprint JSON string")] string blueprintJson) =>
        {
            var errors = _validator.Validate(blueprintJson);
            if (errors.Count == 0)
                return new { valid = true, errors = Array.Empty<string>() };

            return new { valid = false, errors = errors.Select(e => e.ToString()).ToArray() };
        },
        "validate_blueprint",
        "Validate a MapBlueprint JSON against the schema and game rules. " +
        "Returns a list of errors. Call this after generating the full blueprint.",
        new AIFunctionFactoryOptions
        {
            AdditionalProperties = new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?> { ["skip_permission"] = true })
        });
}
```

### 4.2 Asset Lookup Tool

```csharp
private AIFunction BuildLookupAssetTool()
{
    return AIFunctionFactory.Create(
        ([Description("Asset type: prefab, mesh, dialogue, loot_table")] string assetType,
         [Description("Query string to search for")] string query) =>
        {
            var results = _assets.Search(assetType, query);
            return new
            {
                found = results.Any(),
                matches = results.Select(a => new { a.Id, a.Description }).Take(10).ToArray()
            };
        },
        "lookup_asset",
        "Search available game assets by type and query. " +
        "Use this to find valid prefabIds, meshAssets, dialogueIds, and lootTables.",
        new AIFunctionFactoryOptions
        {
            AdditionalProperties = new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?> { ["skip_permission"] = true })
        });
}
```

### 4.3 Overlap Check Tool

```csharp
private AIFunction BuildCheckOverlapTool()
{
    return AIFunctionFactory.Create(
        ([Description("JSON array of building footprints: [{id, chunkX, chunkY, localTileX, localTileY, footprintTiles}]")]
         string buildingsJson) =>
        {
            var buildings = JsonSerializer.Deserialize<List<BuildingFootprint>>(buildingsJson);
            var overlaps = SpatialUtils.FindOverlaps(buildings!);
            return new
            {
                hasOverlaps = overlaps.Any(),
                overlaps = overlaps.Select(o => $"{o.A} overlaps {o.B}").ToArray()
            };
        },
        "check_overlap",
        "Check if any buildings have overlapping footprints. Returns conflicts.",
        new AIFunctionFactoryOptions
        {
            AdditionalProperties = new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?> { ["skip_permission"] = true })
        });
}
```

### 4.4 Walkability Check Tool

```csharp
private AIFunction BuildCheckWalkabilityTool()
{
    return AIFunctionFactory.Create(
        ([Description("Chunk X")] int chunkX,
         [Description("Chunk Y")] int chunkY,
         [Description("Local tile X")] int localTileX,
         [Description("Local tile Y")] int localTileY,
         [Description("JSON of terrain regions and building footprints")] string contextJson) =>
        {
            var ctx = JsonSerializer.Deserialize<WalkabilityContext>(contextJson);
            var walkable = SpatialUtils.IsTileWalkable(chunkX, chunkY, localTileX, localTileY, ctx!);
            return new { chunkX, chunkY, localTileX, localTileY, walkable };
        },
        "check_walkability",
        "Check whether a specific tile coordinate is walkable (not water, not building footprint, not off-map).",
        new AIFunctionFactoryOptions
        {
            AdditionalProperties = new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?> { ["skip_permission"] = true })
        });
}
```

### 4.5 List Prefabs Tool

```csharp
private AIFunction BuildListAvailablePrefabsTool()
{
    return AIFunctionFactory.Create(
        ([Description("Category: npc, enemy, container, building")] string category) =>
        {
            var prefabs = _assets.ListPrefabs(category);
            return prefabs.Select(p => new { p.Id, p.Description, p.Tags }).ToArray();
        },
        "list_available_prefabs",
        "List all available prefab IDs for a given category. " +
        "Use this to discover what NPCs, enemies, containers, and buildings exist.",
        new AIFunctionFactoryOptions
        {
            AdditionalProperties = new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?> { ["skip_permission"] = true })
        });
}
```

---

## 5. Generation Flow

### 5.1 Single-Turn with Tool Calls

Unlike the external fix loop in doc 03 §8.4, the Copilot SDK session is multi-turn by design. The LLM:

1. Receives the system prompt (schema + rules) and user prompt (location + constraints)
2. Calls `list_available_prefabs` to discover assets
3. Calls `lookup_asset` to verify specific references as it builds the blueprint
4. Calls `check_overlap` after placing buildings
5. Calls `check_walkability` for player start and NPC positions
6. Outputs the full `MapBlueprint` JSON
7. Calls `validate_blueprint` as a final check
8. If errors are returned, fixes them in-place and re-validates
9. Session reaches idle — blueprint is complete

```
Session Turn 1:  User prompt (location, constraints)
                 ↓
                 LLM calls list_available_prefabs("npc")
                 LLM calls list_available_prefabs("enemy")
                 LLM calls list_available_prefabs("building")
                 LLM calls lookup_asset("loot_table", "raider")
                 ...
                 LLM generates full MapBlueprint JSON
                 LLM calls validate_blueprint(json)
                 ↓
                 Tool returns: { valid: false, errors: ["building overlap at ..."] }
                 ↓
                 LLM adjusts coordinates
                 LLM calls validate_blueprint(json_v2)
                 ↓
                 Tool returns: { valid: true, errors: [] }
                 ↓
                 LLM outputs final JSON
                 → SessionIdleEvent
```

### 5.2 Collecting the Output

```csharp
private async Task<MapBlueprint> SendAndCollectBlueprint(
    CopilotSession session,
    MapGenerationRequest request)
{
    var responseBuilder = new StringBuilder();
    var done = new TaskCompletionSource<MapBlueprint>();

    session.On(evt =>
    {
        switch (evt)
        {
            case AssistantMessageEvent msg:
                responseBuilder.Append(msg.Data.Content);
                break;

            case SessionIdleEvent:
                var json = ExtractJsonFromResponse(responseBuilder.ToString());
                var blueprint = JsonSerializer.Deserialize<MapBlueprint>(json);
                done.SetResult(blueprint!);
                break;

            case SessionErrorEvent err:
                done.SetException(new MapGenerationException(err.Data.Message));
                break;
        }
    });

    await session.SendAsync(new MessageOptions
    {
        Prompt = BuildUserPrompt(request),
    });

    return await done.Task;
}

private static string ExtractJsonFromResponse(string response)
{
    // Extract content between ```json and ``` fences
    var match = Regex.Match(response, @"```json\s*([\s\S]*?)\s*```");
    return match.Success ? match.Groups[1].Value : response;
}
```

---

## 6. Prompt Construction

### 6.1 User Prompt Builder

```csharp
private string BuildUserPrompt(MapGenerationRequest request)
{
    var sb = new StringBuilder();
    sb.AppendLine("Generate a MapBlueprint JSON for the following location:");
    sb.AppendLine();

    sb.AppendLine($"LOCATION: {request.LocationName}");
    sb.AppendLine(request.GeographyDescription);
    sb.AppendLine();

    sb.AppendLine("POST-APOCALYPTIC CONTEXT:");
    sb.AppendLine(request.PostApocContext);
    sb.AppendLine();

    sb.AppendLine("GAME PARAMETERS:");
    sb.AppendLine($"- Dimensions: {request.ChunksWide} chunks wide × {request.ChunksHigh} chunks high");
    sb.AppendLine($"- Player level range: {request.MinLevel}–{request.MaxLevel}");
    sb.AppendLine($"- Difficulty tiers: {request.DifficultyDescription}");
    sb.AppendLine($"- Factions: {string.Join(", ", request.Factions)}");
    sb.AppendLine($"- Quest chains needed: {request.QuestChainCount}");
    sb.AppendLine();

    sb.AppendLine("IMPORTANT: Use the available tools to look up valid prefab IDs, ");
    sb.AppendLine("verify asset references, check for overlaps, and validate the final output.");
    sb.AppendLine("Do NOT invent asset IDs — always call lookup_asset or list_available_prefabs first.");

    return sb.ToString();
}
```

### 6.2 Request Model

```csharp
public sealed record MapGenerationRequest
{
    public required string LocationName { get; init; }
    public required string GeographyDescription { get; init; }
    public required string PostApocContext { get; init; }
    public required int ChunksWide { get; init; }
    public required int ChunksHigh { get; init; }
    public required int MinLevel { get; init; }
    public required int MaxLevel { get; init; }
    public required string DifficultyDescription { get; init; }
    public required string[] Factions { get; init; }
    public required int QuestChainCount { get; init; }
}
```

---

## 7. Model Selection

The Copilot SDK supports multiple models. Different models have different strengths for map generation:

| Model | Strengths | Recommended For |
|-------|-----------|-----------------|
| `gpt-4.1` | Strong structured output, good JSON conformance | Default for most maps |
| `claude-sonnet-4.5` | Better spatial reasoning, creative geography | Complex terrain layouts |
| `o4-mini` | Fast, cheaper, good for simple maps | Iteration/testing |

The model is set per-session, so the generator can select based on map complexity:

```csharp
var model = request.ChunksWide * request.ChunksHigh > 30
    ? "claude-sonnet-4.5"   // large maps need better spatial reasoning
    : "gpt-4.1";           // standard maps
```

---

## 8. BYOK Option

For production use without GitHub Copilot billing, use BYOK (Bring Your Own Key):

```csharp
await using var session = await _client.CreateSessionAsync(new SessionConfig
{
    Provider = new ProviderConfig
    {
        Type = "openai",                          // or "azure", "anthropic"
        BaseUrl = "https://api.openai.com/v1",
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    },
    Tools = [ /* same tools */ ],
    SystemMessage = new SystemMessageConfig
    {
        Mode = SystemMessageMode.Replace,
        Content = BuildSystemPrompt(),
    },
});
```

This uses the same tool infrastructure but routes through your own API key. No GitHub auth required.

---

## 9. Session Persistence & Iteration

The SDK supports session resumption. This enables a workflow where a designer:

1. Generates a map → reviews it → requests changes in the same session context
2. The LLM retains full context of what it already generated
3. Targeted fixes don't require regenerating the entire blueprint

```csharp
// Initial generation
var session = await _client.CreateSessionAsync(config);
var sessionId = session.SessionId;
await session.SendAsync(new MessageOptions { Prompt = BuildUserPrompt(request) });
// ... collect blueprint ...
await session.DisposeAsync();  // preserves session data on disk

// Later: request modifications
var session2 = await _client.ResumeSessionAsync(sessionId, new ResumeSessionConfig
{
    OnPermissionRequest = PermissionHandler.ApproveAll,
});
await session2.SendAsync(new MessageOptions
{
    Prompt = "Move the raider camp 2 chunks east and add a second bridge."
});
// ... collect updated blueprint ...
```

---

## 10. Disabling Built-in Tools

The Copilot SDK enables all built-in CLI tools by default (file system, git, web). For map generation, we don't want the LLM writing files or running commands — only our custom tools should be available:

```csharp
var session = await _client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4.1",
    Tools = [ /* custom tools only */ ],
    ExcludedTools = ["*"],          // disable all built-in tools
    OnPermissionRequest = (request, _) =>
    {
        // Only allow custom tools
        if (request.Kind == "custom_tool")
            return Task.FromResult(new PermissionRequestResult
            {
                Kind = PermissionRequestResultKind.Approved
            });

        return Task.FromResult(new PermissionRequestResult
        {
            Kind = PermissionRequestResultKind.DeniedByRules
        });
    },
});
```

---

## 11. Hooks for Logging & Cost Tracking

Session hooks let us observe every tool call and track generation cost:

```csharp
Hooks = new SessionHooks
{
    OnPreToolUse = async (input, _) =>
    {
        Log.Information("Tool call: {Tool} args={Args}", input.ToolName, input.ToolArgs);
        return new PreToolUseHookOutput { PermissionDecision = "allow" };
    },

    OnPostToolUse = async (input, _) =>
    {
        Log.Information("Tool result: {Tool} elapsed={Elapsed}ms", input.ToolName, input.Elapsed);
        return new PostToolUseHookOutput();
    },

    OnSessionEnd = async (input, _) =>
    {
        Log.Information("Map generation session ended: {Reason}", input.Reason);
        return null;
    },
}
```

---

## 12. Error Handling & Retries

```csharp
public async Task<MapBlueprint> GenerateWithRetryAsync(
    MapGenerationRequest request,
    int maxAttempts = 3)
{
    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            return await GenerateMapAsync(request);
        }
        catch (MapGenerationException ex) when (attempt < maxAttempts)
        {
            Log.Warning("Generation attempt {Attempt} failed: {Error}. Retrying...",
                attempt, ex.Message);
        }
    }

    throw new MapGenerationException("Failed after max retry attempts.");
}
```

---

## 13. CLI Tool for Map Designers

Wrap the service in a simple console app that map designers can use:

```
Usage: oravey2-mapgen <location-file.json> [--model gpt-4.1] [--output map.json]

  location-file.json   JSON file with MapGenerationRequest fields
  --model              LLM model to use (default: gpt-4.1)
  --output             Output path for generated MapBlueprint (default: stdout)
  --validate-only      Run validation on an existing blueprint without generating
  --resume <id>        Resume a previous generation session for iterative edits
```

---

## 14. Comparison: Direct API vs Copilot SDK

| Concern | Direct OpenAI/Anthropic API | Copilot SDK |
|---------|----------------------------|-------------|
| Auth setup | Manual API key management per provider | GitHub auth or BYOK — single interface |
| Tool calling | Manual JSON schema + dispatch loop | `AIFunctionFactory.Create` + automatic dispatch |
| Multi-turn fix loop | Custom state machine | Native session multi-turn |
| Model switching | Different client per provider | Change `Model` string, same code |
| Streaming | Provider-specific SSE parsing | `session.On(evt => ...)` |
| Context management | Manual token counting | Infinite sessions with auto-compaction |
| Session persistence | Build your own | Built-in checkpoint/resume |
| Cost tracking | Manual token math | Hooks + telemetry |
| CLI tools (file read etc.) | N/A | Available if needed via built-in tools |

---

## 15. Implementation Plan

| Step | Description | Depends On |
|------|-------------|------------|
| 1 | Add `GitHub.Copilot.SDK` NuGet to a new `Oravey2.MapGen` project | — |
| 2 | Define `AssetRegistry` interface backed by game asset manifests | Asset pipeline |
| 3 | Implement `IBlueprintValidator` from doc 03 §8.3 validation rules | Schema finalization |
| 4 | Build the 5 custom tools (§4.1–§4.5) | Steps 2, 3 |
| 5 | Implement `MapGeneratorService` (§3.2–§3.3) | Step 4 |
| 6 | Build CLI wrapper (§13) | Step 5 |
| 7 | Test with Portland map from doc 03 §8.2 as benchmark | Steps 1–6 |
| 8 | Benchmark model accuracy (gpt-4.1 vs claude-sonnet-4.5 vs o4-mini) | Step 7 |

---

## 16. Open Questions

1. **Tool count limit:** The Copilot SDK passes tool schemas to the model. 5 tools is fine; if we add more (e.g., road network validator, faction territory checker), does the context overhead degrade output quality?
2. **Token budget:** Large blueprints (8×6 chunks, 20+ buildings, 10+ enemy groups) produce ~3,000 tokens of JSON. Plus tool call overhead. Need to verify this fits within model context windows.
3. **SDK stability:** The Copilot SDK is in public preview (v0.2.0). Breaking changes are expected. Pin to a specific version and test on upgrades.
4. **Offline generation:** The Copilot SDK requires the Copilot CLI and network access. For fully offline map generation, BYOK with a local model endpoint (e.g., Ollama) may work but is untested.
5. **Determinism:** Same prompt + same model + same seed should produce the same blueprint. Need to verify if the SDK exposes temperature/seed controls, or if those must be set via BYOK provider config.
