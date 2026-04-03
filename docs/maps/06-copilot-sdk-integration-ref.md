# Copilot SDK Integration Reference

> Quick reference for wiring `MapGeneratorService` to `GitHub.Copilot.SDK 0.2.x`

## SDK Package

```xml
<PackageReference Include="GitHub.Copilot.SDK" Version="0.2.*" />
```

## Client Lifecycle

```csharp
await using var client = new CopilotClient(new CopilotClientOptions
{
    CliPath = cliPath ?? "copilot",  // from settings
    AutoStart = true
});
await client.StartAsync();
```

## Session Creation

```csharp
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = request.Model ?? "gpt-4.1",
    Streaming = true,
    OnPermissionRequest = PermissionHandler.ApproveAll,
    SystemMessage = new SystemMessageConfig
    {
        Mode = SystemMessageMode.Replace,
        Content = promptBuilder.BuildSystemPrompt()
    },
    Tools = [validateTool, lookupTool, overlapTool, walkabilityTool, listPrefabsTool],
    ExcludedTools = ["edit_file", "read_file", "shell"]  // no filesystem access
});
```

## Custom Tools via AIFunctionFactory

```csharp
using Microsoft.Extensions.AI;
using System.ComponentModel;

var validateTool = AIFunctionFactory.Create(
    ([Description("Blueprint JSON")] string blueprintJson) =>
    {
        return new ValidateBlueprintTool(validator).Handle(blueprintJson);
    },
    "validate_blueprint",
    "Validate a MapBlueprint JSON against terrain rules",
    new AIFunctionFactoryOptions
    {
        AdditionalProperties = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?> { ["skip_permission"] = true })
    });
```

## Event Handling

```csharp
var done = new TaskCompletionSource<string>();
var accumulated = new StringBuilder();

session.On(evt =>
{
    switch (evt)
    {
        case AssistantMessageDeltaEvent delta:
            accumulated.Append(delta.Data.DeltaContent);
            OnProgress?.Invoke(new GenerationProgress
            {
                Phase = GenerationPhase.Streaming,
                Message = "Generating...",
                StreamDelta = delta.Data.DeltaContent
            });
            break;

        case ToolExecutionStartEvent toolStart:
            OnProgress?.Invoke(new GenerationProgress
            {
                Phase = GenerationPhase.ToolCall,
                Message = $"Calling {toolStart.Data.ToolName}...",
                ToolName = toolStart.Data.ToolName
            });
            break;

        case SessionIdleEvent:
            done.SetResult(accumulated.ToString());
            break;

        case SessionErrorEvent err:
            done.TrySetException(new Exception(err.Data.Message));
            break;
    }
});

await session.SendAsync(new MessageOptions
{
    Prompt = promptBuilder.BuildUserPrompt(request)
});

var fullResponse = await done.Task;
return collector.CollectFromResponse(fullResponse, sw.Elapsed, session.SessionId);
```

## BYOK

```csharp
Provider = useBYOK ? new ProviderConfig
{
    Type = providerType,   // "openai", "azure", "anthropic"
    BaseUrl = baseUrl,
    ApiKey = apiKey
} : null
```

## Resume Session

```csharp
await using var session = await client.ResumeSessionAsync(sessionId, new ResumeSessionConfig
{
    OnPermissionRequest = PermissionHandler.ApproveAll
});

await session.SendAsync(new MessageOptions { Prompt = refinementPrompt });
```
