using System.Net;
using System.Text;
using System.Text.Json;
using Oravey2.MapGen.Models.Meshy;
using Oravey2.MapGen.Services;

namespace Oravey2.Tests;

public class MeshyClientTests : IAsyncDisposable
{
    private readonly MockHttpHandler _handler = new();
    private readonly MeshyClient _client;

    public MeshyClientTests()
    {
        _client = new MeshyClient("test-api-key", "https://api.meshy.ai/openapi", _handler);
    }

    [Fact]
    public async Task CreateTextTo3DAsync_PostsAndReturnsTaskId()
    {
        _handler.SetResponse("""{"result":"task_001"}""");

        var req = new TextTo3DRequest { Mode = "refine", Prompt = "a stone house", ArtStyle = "realistic" };
        var resp = await _client.CreateTextTo3DAsync(req);

        Assert.Equal("task_001", resp.Result);
        Assert.Equal(HttpMethod.Post, _handler.LastRequest!.Method);
        Assert.Contains("/v2/text-to-3d", _handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateImageTo3DAsync_PostsAndReturnsTaskId()
    {
        _handler.SetResponse("""{"result":"task_002"}""");

        var req = new ImageTo3DRequest { ImageUrl = "https://example.com/img.png", Prompt = "a warrior" };
        var resp = await _client.CreateImageTo3DAsync(req);

        Assert.Equal("task_002", resp.Result);
        Assert.Contains("/v1/image-to-3d", _handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRemeshAsync_PostsAndReturnsTaskId()
    {
        _handler.SetResponse("""{"result":"task_003"}""");

        var req = new RemeshRequest { InputTaskId = "task_001", TargetFormats = ["glb"], TargetPolycount = 5000 };
        var resp = await _client.CreateRemeshAsync(req);

        Assert.Equal("task_003", resp.Result);
        Assert.Contains("/v1/remesh", _handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateRiggingAsync_PostsAndReturnsTaskId()
    {
        _handler.SetResponse("""{"result":"task_004"}""");

        var req = new RiggingRequest { InputTaskId = "task_001" };
        var resp = await _client.CreateRiggingAsync(req);

        Assert.Equal("task_004", resp.Result);
        Assert.Contains("/v1/rigging", _handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateAnimationAsync_PostsAndReturnsTaskId()
    {
        _handler.SetResponse("""{"result":"task_005"}""");

        var req = new AnimationRequest { ActionId = "walk_cycle" };
        var resp = await _client.CreateAnimationAsync(req);

        Assert.Equal("task_005", resp.Result);
        Assert.Contains("/v1/animation", _handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetBalanceAsync_ReturnsBalance()
    {
        _handler.SetResponse("""{"balance":4500}""");

        var balance = await _client.GetBalanceAsync();

        Assert.Equal(4500, balance.Balance);
        Assert.Equal(HttpMethod.Get, _handler.LastRequest!.Method);
        Assert.Contains("/v1/balance", _handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetTextTo3DStatusAsync_ReturnsStatus()
    {
        _handler.SetResponse("""{"id":"task_001","status":"IN_PROGRESS","progress":65}""");

        var status = await _client.GetTextTo3DStatusAsync("task_001");

        Assert.Equal("task_001", status.Id);
        Assert.Equal("IN_PROGRESS", status.Status);
        Assert.Equal(65, status.Progress);
    }

    [Fact]
    public async Task StreamTaskAsync_YieldsStatuses_UntilSucceeded()
    {
        var sseData = "data: {\"id\":\"t1\",\"status\":\"IN_PROGRESS\",\"progress\":50}\n\n" +
                      "data: {\"id\":\"t1\",\"status\":\"SUCCEEDED\",\"progress\":100,\"model_urls\":{\"glb\":\"https://cdn/m.glb\"}}\n\n";
        _handler.SetSseResponse(sseData);

        var statuses = new List<MeshyTaskStatus>();
        await foreach (var status in _client.StreamTaskAsync("/v2/text-to-3d", "t1"))
        {
            statuses.Add(status);
        }

        Assert.Equal(2, statuses.Count);
        Assert.Equal("IN_PROGRESS", statuses[0].Status);
        Assert.Equal(50, statuses[0].Progress);
        Assert.Equal("SUCCEEDED", statuses[1].Status);
        Assert.NotNull(statuses[1].ModelUrls);
        Assert.Equal("https://cdn/m.glb", statuses[1].ModelUrls!["glb"]);
    }

    [Fact]
    public async Task StreamTaskAsync_StopsOnFailed()
    {
        var sseData = "data: {\"id\":\"t2\",\"status\":\"IN_PROGRESS\",\"progress\":30}\n\n" +
                      "data: {\"id\":\"t2\",\"status\":\"FAILED\",\"progress\":30,\"task_error\":\"Out of credits\"}\n\n" +
                      "data: {\"id\":\"t2\",\"status\":\"IN_PROGRESS\",\"progress\":40}\n\n";  // should not be reached
        _handler.SetSseResponse(sseData);

        var statuses = new List<MeshyTaskStatus>();
        await foreach (var status in _client.StreamTaskAsync("/v2/text-to-3d", "t2"))
        {
            statuses.Add(status);
        }

        Assert.Equal(2, statuses.Count);
        Assert.Equal("FAILED", statuses[1].Status);
        Assert.Equal("Out of credits", statuses[1].TaskError);
    }

    [Fact]
    public async Task StreamTaskAsync_SkipsNonDataLines()
    {
        var sseData = ": heartbeat\n\n" +
                      "event: message\n" +
                      "data: {\"id\":\"t3\",\"status\":\"SUCCEEDED\",\"progress\":100}\n\n";
        _handler.SetSseResponse(sseData);

        var statuses = new List<MeshyTaskStatus>();
        await foreach (var status in _client.StreamTaskAsync("/v2/text-to-3d", "t3"))
        {
            statuses.Add(status);
        }

        Assert.Single(statuses);
        Assert.Equal("SUCCEEDED", statuses[0].Status);
    }

    [Fact]
    public async Task CreateTextTo3DAsync_SetsAuthHeader()
    {
        _handler.SetResponse("""{"result":"task_auth"}""");

        await _client.CreateTextTo3DAsync(new TextTo3DRequest { Mode = "preview", Prompt = "test" });

        var authHeader = _handler.LastRequest!.Headers.Authorization;
        Assert.NotNull(authHeader);
        Assert.Equal("Bearer", authHeader.Scheme);
        Assert.Equal("test-api-key", authHeader.Parameter);
    }

    [Fact]
    public async Task OnProgress_FiresOnSubmit()
    {
        _handler.SetResponse("""{"result":"task_p"}""");

        var progressMessages = new List<MeshyProgress>();
        _client.OnProgress += p => progressMessages.Add(p);

        await _client.CreateTextTo3DAsync(new TextTo3DRequest { Mode = "refine", Prompt = "test" });

        Assert.Single(progressMessages);
        Assert.Equal(MeshyPhase.Submitting, progressMessages[0].Phase);
    }

    public ValueTask DisposeAsync() => _client.DisposeAsync();

    /// <summary>
    /// Mock HTTP handler for testing MeshyClient without network.
    /// </summary>
    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private string _responseBody = "{}";
        private string _contentType = "application/json";
        private HttpStatusCode _statusCode = HttpStatusCode.OK;
        public HttpRequestMessage? LastRequest { get; private set; }

        public void SetResponse(string body, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseBody = body;
            _statusCode = statusCode;
            _contentType = "application/json";
        }

        public void SetSseResponse(string body)
        {
            _responseBody = body;
            _statusCode = HttpStatusCode.OK;
            _contentType = "text/event-stream";
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, _contentType)
            };
            return Task.FromResult(response);
        }
    }
}
