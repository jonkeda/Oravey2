using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Oravey2.MapGen.Download;
using Xunit.Abstractions;

namespace Oravey2.Tests.Download;

/// <summary>
/// Integration tests for Earthdata SRTM download diagnostics. These require network
/// access and real credentials, so they are skipped when env vars are not set.
///
/// To run locally:
///   $env:EARTHDATA_USERNAME = "your_username"
///   $env:EARTHDATA_PASSWORD = "your_password"
///   dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~EarthdataDiagnosticTests"
/// </summary>
[Trait("Category", "Integration")]
public class EarthdataDiagnosticTests
{
    private readonly ITestOutputHelper _output;

    private static string? Username => Environment.GetEnvironmentVariable("EARTHDATA_USERNAME");
    private static string? Password => Environment.GetEnvironmentVariable("EARTHDATA_PASSWORD");
    private static bool HasCredentials => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    public EarthdataDiagnosticTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Earthdata_ListTokens_ReturnsValidResponse()
    {
        if (!HasCredentials) { _output.WriteLine("SKIP: Set EARTHDATA_USERNAME/PASSWORD env vars"); return; }

        using var http = CreateHttpClient();
        var credentials = EncodeCredentials();

        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://urs.earthdata.nasa.gov/api/users/tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"GET /api/users/tokens → {response.StatusCode}");
        _output.WriteLine(body);

        Assert.True(response.IsSuccessStatusCode,
            $"List tokens failed: {response.StatusCode} — {body}");

        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        _output.WriteLine($"User has {doc.RootElement.GetArrayLength()} existing token(s)");
    }

    [Fact]
    public async Task Earthdata_FindOrCreateToken_GetsAccessToken()
    {
        if (!HasCredentials) { _output.WriteLine("SKIP: Set EARTHDATA_USERNAME/PASSWORD env vars"); return; }

        using var http = CreateHttpClient();
        var credentials = EncodeCredentials();

        // find_or_create_token reuses existing tokens — no 403 risk
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://urs.earthdata.nasa.gov/api/users/find_or_create_token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"POST /api/users/find_or_create_token → {response.StatusCode}");
        _output.WriteLine(body);

        Assert.True(response.IsSuccessStatusCode,
            $"find_or_create_token failed: {response.StatusCode} — {body}");

        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("access_token", out var tokenProp),
            $"No access_token in response: {body}");
        Assert.False(string.IsNullOrEmpty(tokenProp.GetString()), "access_token is empty");

        _output.WriteLine($"Token obtained (length={tokenProp.GetString()!.Length})");
    }

    [Fact]
    public async Task Earthdata_CreateToken_MayHitMaxLimit()
    {
        if (!HasCredentials) { _output.WriteLine("SKIP: Set EARTHDATA_USERNAME/PASSWORD env vars"); return; }

        using var http = CreateHttpClient();
        var credentials = EncodeCredentials();

        // This is the endpoint currently used in DataDownloadService.GetEarthdataTokenAsync
        // It creates a NEW token and fails with 403 if you already have 2
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://urs.earthdata.nasa.gov/api/users/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"POST /api/users/token → {response.StatusCode}");
        _output.WriteLine(body);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            _output.WriteLine("*** BUG CONFIRMED: max_token_limit hit — code should use find_or_create_token ***");
            Assert.Contains("max_token_limit", body);
        }
        else
        {
            Assert.True(response.IsSuccessStatusCode,
                $"Token creation failed: {response.StatusCode} — {body}");
        }
    }

    [Fact]
    public async Task Srtm_HeadRequest_SingleTile_WithToken()
    {
        if (!HasCredentials) { _output.WriteLine("SKIP: Set EARTHDATA_USERNAME/PASSWORD env vars"); return; }

        using var http = CreateHttpClient();
        var bearerToken = await GetBearerTokenAsync(http);

        // HEAD request for a known SRTM tile: N52E004 (Noord-Holland area)
        var tileUrl = "https://data.lpdaac.earthdatacloud.nasa.gov/lp-prod-protected/SRTMGL1.003/N52E004.SRTMGL1.hgt/N52E004.SRTMGL1.hgt.zip";
        using var tileRequest = new HttpRequestMessage(HttpMethod.Head, tileUrl);
        tileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await http.SendAsync(tileRequest);
        _output.WriteLine($"HEAD {tileUrl} → {response.StatusCode}");
        _output.WriteLine($"Content-Length: {response.Content.Headers.ContentLength}");

        Assert.True(response.IsSuccessStatusCode,
            $"SRTM tile HEAD failed: {response.StatusCode}");
    }

    [Fact]
    public void DataDownloadService_GetRequiredTiles_NoordHolland()
    {
        var service = new DataDownloadService(new HttpClient());

        // Noord-Holland approximate bounds
        var tiles = service.GetRequiredSrtmTileNames(
            northLat: 53.0, southLat: 52.0,
            eastLon: 5.0, westLon: 4.0);

        _output.WriteLine($"Required tiles: {string.Join(", ", tiles)}");

        Assert.NotEmpty(tiles);
        Assert.Contains("N52E004", tiles);
    }

    // --- Helpers ---

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Oravey2.Tests/1.0");
        return http;
    }

    private static string EncodeCredentials()
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Password}"));

    private async Task<string> GetBearerTokenAsync(HttpClient http)
    {
        var credentials = EncodeCredentials();
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://urs.earthdata.nasa.gov/api/users/find_or_create_token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }
}
