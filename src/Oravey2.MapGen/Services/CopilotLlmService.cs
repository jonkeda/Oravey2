using System.ComponentModel;
using System.Text;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;

namespace Oravey2.MapGen.Services;

/// <summary>
/// Wraps the GitHub Copilot SDK to provide LLM call delegates for pipeline steps.
/// Singleton — the CopilotClient is expensive to start (spawns CLI process).
/// </summary>
public sealed class CopilotLlmService : IAsyncDisposable
{
    private CopilotClient? _client;
    private readonly SemaphoreSlim _startLock = new(1, 1);

    public string Model { get; set; } = "gpt-4.1";
    public string? CliPath { get; set; }
    public bool UseBYOK { get; set; }
    public string? ProviderType { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }

    /// <summary>
    /// Returns a delegate for simple text completions.
    /// </summary>
    public Func<string, CancellationToken, Task<string>> GetLlmCall() => CallAsync;

    /// <summary>
    /// Returns a delegate that uses a tool-based approach for structured output.
    /// The LLM must call the <c>submit_result</c> tool with its JSON result.
    /// </summary>
    public Func<string, CancellationToken, Task<string>> GetToolCall(string systemMessage)
        => (prompt, ct) => CallWithToolAsync(systemMessage, prompt, ct);

    /// <summary>
    /// Returns a delegate that runs a session with caller-supplied tools.
    /// The caller captures results via the AIFunction callbacks.
    /// </summary>
    public Func<string, IList<AIFunction>, CancellationToken, Task> GetToolCallDelegate(string systemMessage)
        => (prompt, tools, ct) => CallWithToolsAsync(systemMessage, prompt, tools, ct);

    /// <summary>
    /// Simple text completion — accumulates AssistantMessageEvent text.
    /// </summary>
    public async Task<string> CallAsync(string prompt, CancellationToken ct)
    {
        var client = await EnsureClientAsync(ct);

        await using var session = await client.CreateSessionAsync(BuildSessionConfig());

        var response = new StringBuilder();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? errorMessage = null;

        using var sub = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    response.Append(msg.Data.Content);
                    break;
                case SessionErrorEvent err:
                    errorMessage = err.Data.Message;
                    done.TrySetResult();
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
            }
        });

        using var reg = ct.Register(() => done.TrySetCanceled(ct));

        await session.SendAsync(new MessageOptions { Prompt = prompt });
        await done.Task;

        if (errorMessage is not null)
            throw new InvalidOperationException($"Copilot error: {errorMessage}");

        return response.ToString();
    }

    /// <summary>
    /// Tool-based structured call — LLM must call submit_result(json).
    /// This avoids markdown fences and preamble text in the response.
    /// </summary>
    public async Task<string> CallWithToolAsync(
        string systemMessage, string prompt, CancellationToken ct)
    {
        var client = await EnsureClientAsync(ct);

        string? capturedJson = null;

        var submitTool = AIFunctionFactory.Create(
            ([Description("The complete JSON result")] string json) =>
            {
                capturedJson = json;
                return "Accepted.";
            },
            "submit_result",
            "Submit the final JSON result. Call this exactly once with the complete JSON array/object.");

        var config = BuildSessionConfig();
        config.SystemMessage = new SystemMessageConfig
        {
            Mode = SystemMessageMode.Append,
            Content = systemMessage,
        };
        config.Tools = [submitTool];
        config.ExcludedTools = ["edit_file", "read_file", "shell", "run_command"];

        await using var session = await client.CreateSessionAsync(config);

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? errorMessage = null;

        using var sub = session.On(evt =>
        {
            switch (evt)
            {
                case SessionErrorEvent err:
                    errorMessage = err.Data.Message;
                    done.TrySetResult();
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
            }
        });

        using var reg = ct.Register(() => done.TrySetCanceled(ct));

        await session.SendAsync(new MessageOptions { Prompt = prompt });
        await done.Task;

        if (errorMessage is not null)
            throw new InvalidOperationException($"Copilot error: {errorMessage}");

        return capturedJson
            ?? throw new InvalidOperationException("LLM did not call submit_result tool.");
    }

    /// <summary>
    /// Runs a session with caller-supplied tools. Returns when the session goes idle.
    /// The caller captures results via the AIFunction callbacks.
    /// </summary>
    public async Task CallWithToolsAsync(
        string systemMessage, string prompt, IList<AIFunction> tools, CancellationToken ct)
    {
        var client = await EnsureClientAsync(ct);

        var config = BuildSessionConfig();
        config.SystemMessage = new SystemMessageConfig
        {
            Mode = SystemMessageMode.Append,
            Content = systemMessage,
        };
        config.Tools = tools.ToList();
        config.ExcludedTools = ["edit_file", "read_file", "shell", "run_command"];

        await using var session = await client.CreateSessionAsync(config);

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? errorMessage = null;

        using var sub = session.On(evt =>
        {
            switch (evt)
            {
                case SessionErrorEvent err:
                    errorMessage = err.Data.Message;
                    done.TrySetResult();
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
            }
        });

        using var reg = ct.Register(() => done.TrySetCanceled(ct));

        await session.SendAsync(new MessageOptions { Prompt = prompt });
        await done.Task;

        if (errorMessage is not null)
            throw new InvalidOperationException($"Copilot error: {errorMessage}");
    }

    private SessionConfig BuildSessionConfig() => new()
    {
        Model = Model,
        OnPermissionRequest = PermissionHandler.ApproveAll,
        Provider = UseBYOK ? new ProviderConfig
        {
            Type = ProviderType,
            BaseUrl = BaseUrl,
            ApiKey = ApiKey,
        } : null,
    };

    private async Task<CopilotClient> EnsureClientAsync(CancellationToken ct)
    {
        if (_client is not null) return _client;

        await _startLock.WaitAsync(ct);
        try
        {
            if (_client is not null) return _client;

            var options = new CopilotClientOptions { AutoStart = true };

            if (!string.IsNullOrWhiteSpace(CliPath))
                options.CliPath = CliPath;

            _client = new CopilotClient(options);
            await _client.StartAsync();
            return _client;
        }
        finally
        {
            _startLock.Release();
        }
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
