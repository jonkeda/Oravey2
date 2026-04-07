using System.Diagnostics;
using System.Text;
using GitHub.Copilot.SDK;
using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Models;
using Oravey2.MapGen.Tools;

namespace Oravey2.MapGen.Services;

public sealed class MapGeneratorService : IAsyncDisposable
{
    private readonly IAssetRegistry _assets;
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

    public MapGeneratorService(IAssetRegistry assets)
    {
        _assets = assets;
    }

    public Task<GenerationResult> GenerateAsync(
        MapGenerationRequest request,
        CancellationToken ct = default)
    {
        // Old LLM blueprint pipeline removed — will be replaced in Step 09 (procedural generation).
        return Task.FromResult(new GenerationResult
        {
            Success = false,
            ErrorMessage = "Blueprint generation pipeline has been removed. Procedural generation coming soon.",
            Elapsed = TimeSpan.Zero
        });
    }

    public Task<GenerationResult> RefineAsync(
        string sessionId,
        string refinementPrompt,
        CancellationToken ct = default)
    {
        return Task.FromResult(new GenerationResult
        {
            Success = false,
            ErrorMessage = "Blueprint generation pipeline has been removed.",
            SessionId = sessionId,
            Elapsed = TimeSpan.Zero
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            try { await _client.StopAsync(); } catch { }
            _client = null;
        }
    }
}
