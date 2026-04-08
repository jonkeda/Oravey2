using Oravey2.MapGen.WorldTemplate;

namespace Oravey2.Tests.MapGen;

public class GeofabrikServiceTests
{
    private static string MakeMinimalGeoJson()
        => """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{"id":"test","name":"Test"},"geometry":null}]}""";

    [Fact]
    public async Task GetIndex_LoadsFromCache()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"geofabrik-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tmpDir);
            var cachePath = Path.Combine(tmpDir, "geofabrik-index-v1.json");
            await File.WriteAllTextAsync(cachePath, MakeMinimalGeoJson());
            // Set recent timestamp so it's not stale
            File.SetLastWriteTimeUtc(cachePath, DateTime.UtcNow);

            // HttpClient that would fail if called
            var http = new HttpClient(new FailingHandler());
            var service = new GeofabrikService(http, tmpDir);

            var index = await service.GetIndexAsync();

            Assert.NotNull(index);
            Assert.Single(index.Roots);
            Assert.Equal("test", index.Roots[0].Id);
        }
        finally
        {
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public async Task GetIndex_RefreshesWhenStale()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"geofabrik-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tmpDir);
            var cachePath = Path.Combine(tmpDir, "geofabrik-index-v1.json");
            await File.WriteAllTextAsync(cachePath, MakeMinimalGeoJson());
            // Set old timestamp so it's stale
            File.SetLastWriteTimeUtc(cachePath, DateTime.UtcNow.AddDays(-10));

            var updatedJson = """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{"id":"updated","name":"Updated"},"geometry":null}]}""";
            var http = new HttpClient(new FakeHandler(updatedJson));
            var service = new GeofabrikService(http, tmpDir);

            var index = await service.GetIndexAsync();

            Assert.Single(index.Roots);
            Assert.Equal("updated", index.Roots[0].Id);
        }
        finally
        {
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public async Task GetIndex_FallsBackToStaleCacheOnError()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"geofabrik-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tmpDir);
            var cachePath = Path.Combine(tmpDir, "geofabrik-index-v1.json");
            await File.WriteAllTextAsync(cachePath, MakeMinimalGeoJson());
            File.SetLastWriteTimeUtc(cachePath, DateTime.UtcNow.AddDays(-10));

            var http = new HttpClient(new FailingHandler());
            var service = new GeofabrikService(http, tmpDir);

            var index = await service.GetIndexAsync();

            Assert.NotNull(index);
            Assert.Equal("test", index.Roots[0].Id);
        }
        finally
        {
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        }
    }

    private class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Simulated network error");
    }

    private class FakeHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage
            {
                Content = new StringContent(responseJson)
            });
    }
}
