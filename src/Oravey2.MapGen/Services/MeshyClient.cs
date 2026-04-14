using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Oravey2.MapGen.Models.Meshy;

namespace Oravey2.MapGen.Services;

public sealed class MeshyClient : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    private static readonly TimeSpan CrudTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StreamTimeout = TimeSpan.FromMinutes(5);

    public event Action<MeshyProgress>? OnProgress;

    public MeshyClient(string apiKey, string baseUrl = "https://api.meshy.ai/openapi")
        : this(apiKey, baseUrl, handler: null)
    {
    }

    internal MeshyClient(string apiKey, string baseUrl, HttpMessageHandler? handler)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl.TrimEnd('/');
        _http = handler is not null ? new HttpClient(handler) : new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new("Bearer", _apiKey);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    // --- Text-to-3D ---

    public async Task<MeshyTaskResponse> CreateTextTo3DAsync(TextTo3DRequest request, CancellationToken ct = default)
    {
        OnProgress?.Invoke(new MeshyProgress { Phase = MeshyPhase.Submitting, Message = "Submitting text-to-3D task..." });
        return await PostAsync<TextTo3DRequest, MeshyTaskResponse>("/v2/text-to-3d", request, ct);
    }

    public async Task<MeshyTaskStatus> GetTextTo3DStatusAsync(string taskId, CancellationToken ct = default)
    {
        return await GetAsync<MeshyTaskStatus>($"/v2/text-to-3d/{taskId}", ct);
    }

    public IAsyncEnumerable<MeshyTaskStatus> StreamTextTo3DAsync(string taskId, CancellationToken ct = default)
    {
        return StreamTaskAsync("/v2/text-to-3d", taskId, ct);
    }

    // --- Image-to-3D ---

    public async Task<MeshyTaskResponse> CreateImageTo3DAsync(ImageTo3DRequest request, CancellationToken ct = default)
    {
        OnProgress?.Invoke(new MeshyProgress { Phase = MeshyPhase.Submitting, Message = "Submitting image-to-3D task..." });
        return await PostAsync<ImageTo3DRequest, MeshyTaskResponse>("/v1/image-to-3d", request, ct);
    }

    public IAsyncEnumerable<MeshyTaskStatus> StreamImageTo3DAsync(string taskId, CancellationToken ct = default)
    {
        return StreamTaskAsync("/v1/image-to-3d", taskId, ct);
    }

    // --- Remesh ---

    public async Task<MeshyTaskResponse> CreateRemeshAsync(RemeshRequest request, CancellationToken ct = default)
    {
        OnProgress?.Invoke(new MeshyProgress { Phase = MeshyPhase.Submitting, Message = "Submitting remesh task..." });
        return await PostAsync<RemeshRequest, MeshyTaskResponse>("/v1/remesh", request, ct);
    }

    public IAsyncEnumerable<MeshyTaskStatus> StreamRemeshAsync(string taskId, CancellationToken ct = default)
    {
        return StreamTaskAsync("/v1/remesh", taskId, ct);
    }

    // --- Rigging ---

    public async Task<MeshyTaskResponse> CreateRiggingAsync(RiggingRequest request, CancellationToken ct = default)
    {
        OnProgress?.Invoke(new MeshyProgress { Phase = MeshyPhase.Submitting, Message = "Submitting rigging task..." });
        return await PostAsync<RiggingRequest, MeshyTaskResponse>("/v1/rigging", request, ct);
    }

    public IAsyncEnumerable<MeshyTaskStatus> StreamRiggingAsync(string taskId, CancellationToken ct = default)
    {
        return StreamTaskAsync("/v1/rigging", taskId, ct);
    }

    // --- Animation ---

    public async Task<MeshyTaskResponse> CreateAnimationAsync(AnimationRequest request, CancellationToken ct = default)
    {
        OnProgress?.Invoke(new MeshyProgress { Phase = MeshyPhase.Submitting, Message = "Submitting animation task..." });
        return await PostAsync<AnimationRequest, MeshyTaskResponse>("/v1/animation", request, ct);
    }

    public IAsyncEnumerable<MeshyTaskStatus> StreamAnimationAsync(string taskId, CancellationToken ct = default)
    {
        return StreamTaskAsync("/v1/animation", taskId, ct);
    }

    // --- Balance ---

    public async Task<MeshyBalance> GetBalanceAsync(CancellationToken ct = default)
    {
        return await GetAsync<MeshyBalance>("/v1/balance", ct);
    }

    // --- Streaming (SSE) ---

    public async IAsyncEnumerable<MeshyTaskStatus> StreamTaskAsync(
        string path,
        string taskId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = $"{_baseUrl}{path}/{taskId}/stream";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new("text/event-stream"));

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!line.StartsWith("data:")) continue;

            var json = line["data:".Length..].Trim();
            if (string.IsNullOrEmpty(json)) continue;

            var status = JsonSerializer.Deserialize<MeshyTaskStatus>(json, _jsonOptions);
            if (status is null) continue;

            var phase = status.Status switch
            {
                "PENDING" => MeshyPhase.Pending,
                "IN_PROGRESS" => MeshyPhase.Processing,
                "SUCCEEDED" => MeshyPhase.Complete,
                "FAILED" or "CANCELED" => MeshyPhase.Error,
                _ => MeshyPhase.Processing
            };

            OnProgress?.Invoke(new MeshyProgress { Phase = phase, Message = $"{status.Status}: {status.Progress}%", PercentComplete = status.Progress });

            yield return status;

            if (status.Status is "SUCCEEDED" or "FAILED" or "CANCELED")
                yield break;
        }
    }

    // --- Download helper ---

    public async Task<byte[]> DownloadModelAsync(string url, CancellationToken ct = default)
    {
        OnProgress?.Invoke(new MeshyProgress { Phase = MeshyPhase.Downloading, Message = "Downloading model..." });
        return await _http.GetByteArrayAsync(url, ct);
    }

    // --- Internal helpers ---

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(CrudTimeout);

        var response = await _http.PostAsJsonAsync($"{_baseUrl}{path}", body, _jsonOptions, cts.Token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions, cts.Token))!;
    }

    private async Task<TResponse> GetAsync<TResponse>(string path, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(CrudTimeout);

        var response = await _http.GetAsync($"{_baseUrl}{path}", cts.Token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions, cts.Token))!;
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }
}
