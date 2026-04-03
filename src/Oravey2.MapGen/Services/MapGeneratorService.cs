using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Models;
using Oravey2.MapGen.Spatial;
using Oravey2.MapGen.Tools;
using Oravey2.MapGen.Validation;

namespace Oravey2.MapGen.Services;

public sealed class MapGeneratorService : IAsyncDisposable
{
    private readonly IAssetRegistry _assets;
    private readonly IBlueprintValidator _validator;
    private readonly PromptBuilder _promptBuilder;
    private readonly BlueprintCollector _collector;
    private CopilotClient? _client;

    public event Action<GenerationProgress>? OnProgress;

    public string? CliPath { get; set; }
    public string? ProviderType { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public bool UseBYOK { get; set; }

    /// <summary>
    /// Optional override for the asset registry. When set, this is used instead
    /// of the default embedded catalog. Set from content pack catalog path.
    /// </summary>
    public IAssetRegistry? AssetRegistryOverride { get; set; }

    private IAssetRegistry EffectiveAssets => AssetRegistryOverride ?? _assets;

    public MapGeneratorService(IAssetRegistry assets, IBlueprintValidator validator)
    {
        _assets = assets;
        _validator = validator;
        _promptBuilder = new PromptBuilder();
        _collector = new BlueprintCollector();
        _collector.OnProgress += progress => OnProgress?.Invoke(progress);
    }

    public async Task<GenerationResult> GenerateAsync(
        MapGenerationRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            OnProgress?.Invoke(new GenerationProgress
            {
                Phase = GenerationPhase.Initializing,
                Message = "Starting Copilot CLI..."
            });

            await EnsureClientAsync();

            var systemPrompt = _promptBuilder.BuildSystemPrompt();
            var userPrompt = _promptBuilder.BuildUserPrompt(request);

            var writeTool = new WriteBlueprintTool(_validator);
            var tools = BuildTools(writeTool);

            OnProgress?.Invoke(new GenerationProgress
            {
                Phase = GenerationPhase.Initializing,
                Message = "Creating session..."
            });

            var sessionConfig = new SessionConfig
            {
                Model = request.Model ?? "gpt-4.1",
                Streaming = true,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Replace,
                    Content = systemPrompt
                },
                Tools = tools,
                ExcludedTools = ["edit_file", "read_file", "shell", "write_file"]
            };

            if (UseBYOK && !string.IsNullOrWhiteSpace(ApiKey))
            {
                sessionConfig.Provider = new ProviderConfig
                {
                    Type = ProviderType ?? "openai",
                    BaseUrl = BaseUrl,
                    ApiKey = ApiKey
                };
            }

            await using var session = await _client!.CreateSessionAsync(sessionConfig);

            OnProgress?.Invoke(new GenerationProgress
            {
                Phase = GenerationPhase.Prompting,
                Message = "Sending prompt to model..."
            });

            var done = new TaskCompletionSource<string>();
            var accumulated = new StringBuilder();

            using var subscription = session.On(evt =>
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
                            Message = $"Tool: {toolStart.Data.ToolName}",
                            ToolName = toolStart.Data.ToolName
                        });
                        break;

                    case ToolExecutionCompleteEvent toolComplete:
                        OnProgress?.Invoke(new GenerationProgress
                        {
                            Phase = GenerationPhase.ToolCall,
                            Message = $"Tool complete: {toolComplete.Data.ToolCallId}",
                            ToolName = toolComplete.Data.ToolCallId,
                            ToolResult = toolComplete.Data.Success ? "done" : "failed"
                        });
                        break;

                    case SessionIdleEvent:
                        done.TrySetResult(accumulated.ToString());
                        break;

                    case SessionErrorEvent err:
                        done.TrySetException(new InvalidOperationException(
                            $"Session error: {err.Data.Message}"));
                        break;
                }
            });

            await session.SendAsync(new MessageOptions { Prompt = userPrompt });

            using var ctr = ct.Register(() => done.TrySetCanceled());
            var fullResponse = await done.Task;

            sw.Stop();

            // Primary: use write_blueprint tool result if the LLM called it
            if (writeTool.LastAcceptedBlueprint is not null)
            {
                OnProgress?.Invoke(new GenerationProgress
                {
                    Phase = GenerationPhase.Complete,
                    Message = "Blueprint accepted via write_blueprint tool."
                });

                return new GenerationResult
                {
                    Success = true,
                    Blueprint = writeTool.LastAcceptedBlueprint,
                    RawJson = writeTool.LastAcceptedJson,
                    Elapsed = sw.Elapsed,
                    SessionId = session.SessionId
                };
            }

            // Fallback: extract JSON from response text (in case LLM ignored tool)
            var result = _collector.CollectFromResponse(fullResponse, sw.Elapsed, session.SessionId);

            if (result.Success && result.Blueprint is not null)
            {
                OnProgress?.Invoke(new GenerationProgress
                {
                    Phase = GenerationPhase.Validating,
                    Message = "Validating generated blueprint..."
                });

                var validation = _validator.Validate(result.Blueprint);
                if (!validation.IsValid)
                {
                    return result with
                    {
                        ValidationErrors = validation.Errors
                            .Select(e => $"[{e.Code}] {e.Message}").ToArray()
                    };
                }
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new GenerationResult
            {
                Success = false,
                ErrorMessage = "Generation cancelled.",
                Elapsed = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new GenerationResult
            {
                Success = false,
                ErrorMessage = $"Generation failed: {ex.Message}",
                Elapsed = sw.Elapsed
            };
        }
    }

    public async Task<GenerationResult> RefineAsync(
        string sessionId,
        string refinementPrompt,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await EnsureClientAsync();

            OnProgress?.Invoke(new GenerationProgress
            {
                Phase = GenerationPhase.Initializing,
                Message = $"Resuming session {sessionId}..."
            });

            await using var session = await _client!.ResumeSessionAsync(sessionId, new ResumeSessionConfig
            {
                OnPermissionRequest = PermissionHandler.ApproveAll
            });

            var done = new TaskCompletionSource<string>();
            var accumulated = new StringBuilder();

            using var subscription = session.On(evt =>
            {
                switch (evt)
                {
                    case AssistantMessageDeltaEvent delta:
                        accumulated.Append(delta.Data.DeltaContent);
                        OnProgress?.Invoke(new GenerationProgress
                        {
                            Phase = GenerationPhase.Streaming,
                            Message = "Refining...",
                            StreamDelta = delta.Data.DeltaContent
                        });
                        break;

                    case SessionIdleEvent:
                        done.TrySetResult(accumulated.ToString());
                        break;

                    case SessionErrorEvent err:
                        done.TrySetException(new InvalidOperationException(err.Data.Message));
                        break;
                }
            });

            await session.SendAsync(new MessageOptions { Prompt = refinementPrompt });

            using var ctr = ct.Register(() => done.TrySetCanceled());
            var fullResponse = await done.Task;

            sw.Stop();
            return _collector.CollectFromResponse(fullResponse, sw.Elapsed, session.SessionId);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new GenerationResult
            {
                Success = false,
                ErrorMessage = $"Refinement failed: {ex.Message}",
                SessionId = sessionId,
                Elapsed = sw.Elapsed
            };
        }
    }

    private async Task EnsureClientAsync()
    {
        if (_client is not null) return;

        var options = new CopilotClientOptions
        {
            AutoStart = true
        };

        if (!string.IsNullOrWhiteSpace(CliPath))
            options.CliPath = CliPath;

        _client = new CopilotClient(options);
        await _client.StartAsync();
    }

    private List<AIFunction> BuildTools(WriteBlueprintTool writeTool)
    {
        var assets = EffectiveAssets;
        var validateTool = new ValidateBlueprintTool(_validator);
        var lookupTool = new LookupAssetTool(assets);
        var overlapTool = new CheckOverlapTool();
        var listPrefabsTool = new ListPrefabsTool(assets);

        var skipPerm = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?> { ["skip_permission"] = true });

        return
        [
            AIFunctionFactory.Create(
                ([Description("The full MapBlueprint JSON to validate")] string blueprintJson) =>
                    validateTool.Handle(blueprintJson),
                new AIFunctionFactoryOptions
                {
                    Name = "validate_blueprint",
                    Description = "Validate a MapBlueprint JSON against terrain rules. Returns {valid, errors[]}.",
                    AdditionalProperties = skipPerm
                }),

            AIFunctionFactory.Create(
                ([Description("Asset category: building, surface, or terrain_mesh")] string assetType,
                 [Description("Search query (name, description, or tag)")] string query) =>
                    lookupTool.Handle(assetType, query),
                new AIFunctionFactoryOptions
                {
                    Name = "lookup_asset",
                    Description = "Search the asset registry for buildings, surfaces, or terrain meshes.",
                    AdditionalProperties = skipPerm
                }),

            AIFunctionFactory.Create(
                ([Description("JSON array of building footprints [{id,tileX,tileY,width,height}]")] string footprintsJson) =>
                {
                    var footprints = System.Text.Json.JsonSerializer.Deserialize<BuildingFootprint[]>(footprintsJson,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? Array.Empty<BuildingFootprint>();
                    return overlapTool.Handle(footprints);
                },
                new AIFunctionFactoryOptions
                {
                    Name = "check_overlap",
                    Description = "Check if any building footprints overlap. Input: JSON array of footprints.",
                    AdditionalProperties = skipPerm
                }),

            AIFunctionFactory.Create(
                ([Description("Asset category to list: building, surface, or terrain_mesh")] string category) =>
                    listPrefabsTool.Handle(category),
                new AIFunctionFactoryOptions
                {
                    Name = "list_available_prefabs",
                    Description = "List all available prefab assets in a category.",
                    AdditionalProperties = skipPerm
                }),

            AIFunctionFactory.Create(
                ([Description("The full MapBlueprint JSON to submit as the final result")] string blueprintJson) =>
                    writeTool.Handle(blueprintJson),
                new AIFunctionFactoryOptions
                {
                    Name = "write_blueprint",
                    Description = "Submit the final MapBlueprint JSON. Validates, checks overlaps, and accepts or rejects. Returns {accepted, errors[]}. Call this instead of outputting JSON as text.",
                    AdditionalProperties = skipPerm
                }),
        ];
    }

    public string BuildSystemPrompt() => _promptBuilder.BuildSystemPrompt();
    public string BuildUserPrompt(MapGenerationRequest request) => _promptBuilder.BuildUserPrompt(request);
    public BlueprintCollector Collector => _collector;

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            try { await _client.StopAsync(); } catch { }
            _client = null;
        }
    }
}
