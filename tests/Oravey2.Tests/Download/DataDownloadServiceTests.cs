using System.Net;
using Oravey2.MapGen.Download;
using Xunit;

namespace Oravey2.Tests.Download;

public class DataDownloadServiceTests
{
    // --- Tile name calculation tests (no network) ---

    [Fact]
    public void GetRequiredTiles_NoordHolland_Returns4Tiles()
    {
        var svc = CreateService();

        var tiles = svc.GetRequiredSrtmTileNames(
            northLat: 53.0, southLat: 52.2,
            eastLon: 5.5, westLon: 4.0);

        Assert.Equal(4, tiles.Count);
        Assert.Contains("N52E004", tiles);
        Assert.Contains("N52E005", tiles);
        Assert.Contains("N53E004", tiles);
        Assert.Contains("N53E005", tiles);
    }

    [Fact]
    public void GetRequiredTiles_NegativeCoords_FormatsCorrectly()
    {
        var svc = CreateService();

        // Southern hemisphere, western hemisphere: -34 to -33, -71 to -70
        var tiles = svc.GetRequiredSrtmTileNames(
            northLat: -33.0, southLat: -34.0,
            eastLon: -70.0, westLon: -71.0);

        Assert.Contains("S34W071", tiles);
        Assert.Contains("S34W070", tiles);
        Assert.Contains("S33W071", tiles);
        Assert.Contains("S33W070", tiles);
    }

    [Fact]
    public void GetRequiredTiles_SingleCell_Returns1Tile()
    {
        var svc = CreateService();

        var tiles = svc.GetRequiredSrtmTileNames(
            northLat: 52.5, southLat: 52.1,
            eastLon: 4.5, westLon: 4.1);

        Assert.Single(tiles);
        Assert.Equal("N52E004", tiles[0]);
    }

    [Fact]
    public void GetRequiredTiles_ExactDegreeBoundary_CoversFullRange()
    {
        var svc = CreateService();

        // 1°×1° box spanning two tile rows and two tile columns
        var tiles = svc.GetRequiredSrtmTileNames(
            northLat: 53.0, southLat: 52.0,
            eastLon: 5.0, westLon: 4.0);

        // floor(52)=52, floor(53)=53, floor(4)=4, floor(5)=5 → 2×2 = 4 tiles
        Assert.Equal(4, tiles.Count);
        Assert.Contains("N52E004", tiles);
        Assert.Contains("N53E005", tiles);
    }

    [Fact]
    public void GetExistingTiles_FindsHgtFiles()
    {
        var svc = CreateService();
        var tempDir = Path.Combine(Path.GetTempPath(), $"srtm_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllBytes(Path.Combine(tempDir, "N52E004.hgt"), [0]);
            File.WriteAllBytes(Path.Combine(tempDir, "N52E005.hgt"), [0]);
            File.WriteAllText(Path.Combine(tempDir, "readme.txt"), "not a tile");

            var existing = svc.GetExistingSrtmTiles(tempDir);

            Assert.Equal(2, existing.Count);
            Assert.Contains("N52E004", existing);
            Assert.Contains("N52E005", existing);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetExistingTiles_NonexistentDir_ReturnsEmpty()
    {
        var svc = CreateService();
        var result = svc.GetExistingSrtmTiles(@"C:\nonexistent_dir_12345");
        Assert.Empty(result);
    }

    [Fact]
    public void GetExistingTiles_FindsHgtGzFiles()
    {
        var svc = CreateService();
        var tempDir = Path.Combine(Path.GetTempPath(), $"srtm_gz_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllBytes(Path.Combine(tempDir, "N52E004.hgt.gz"), [0]);
            File.WriteAllBytes(Path.Combine(tempDir, "N52E005.hgt.gz"), [0]);

            var existing = svc.GetExistingSrtmTiles(tempDir);

            Assert.Equal(2, existing.Count);
            Assert.Contains("N52E004", existing);
            Assert.Contains("N52E005", existing);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetExistingTiles_MixedFormats_Deduplicates()
    {
        var svc = CreateService();
        var tempDir = Path.Combine(Path.GetTempPath(), $"srtm_mixed_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllBytes(Path.Combine(tempDir, "N52E004.hgt"), [0]);
            File.WriteAllBytes(Path.Combine(tempDir, "N52E004.hgt.gz"), [0]);
            File.WriteAllBytes(Path.Combine(tempDir, "N52E005.hgt.gz"), [0]);

            var existing = svc.GetExistingSrtmTiles(tempDir);

            Assert.Equal(2, existing.Count);
            Assert.Contains("N52E004", existing);
            Assert.Contains("N52E005", existing);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetExistingTiles_CountsOceanMarkers()
    {
        var svc = CreateService();
        var tempDir = Path.Combine(Path.GetTempPath(), $"srtm_ocean_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllBytes(Path.Combine(tempDir, "N52E004.hgt"), [0]);
            File.WriteAllBytes(Path.Combine(tempDir, "N52E005.hgt"), [0]);
            File.WriteAllBytes(Path.Combine(tempDir, "N52E003.ocean"), []);

            var existing = svc.GetExistingSrtmTiles(tempDir);

            Assert.Equal(3, existing.Count);
            Assert.Contains("N52E003", existing);
            Assert.Contains("N52E004", existing);
            Assert.Contains("N52E005", existing);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetExistingTiles_OceanAndHgtDeduplicates()
    {
        var svc = CreateService();
        var tempDir = Path.Combine(Path.GetTempPath(), $"srtm_dedup_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // If a tile has both .hgt and .ocean (shouldn't happen, but be safe)
            File.WriteAllBytes(Path.Combine(tempDir, "N52E004.hgt"), [0]);
            File.WriteAllBytes(Path.Combine(tempDir, "N52E004.ocean"), []);

            var existing = svc.GetExistingSrtmTiles(tempDir);

            Assert.Single(existing);
            Assert.Contains("N52E004", existing);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FormatTileName_PositiveCoords()
    {
        Assert.Equal("N52E004", DataDownloadService.FormatTileName(52, 4));
        Assert.Equal("N00E000", DataDownloadService.FormatTileName(0, 0));
    }

    [Fact]
    public void FormatTileName_NegativeCoords()
    {
        Assert.Equal("S34W071", DataDownloadService.FormatTileName(-34, -71));
        Assert.Equal("S01E010", DataDownloadService.FormatTileName(-1, 10));
    }

    // --- OSM download tests (mocked HTTP) ---

    [Fact]
    public async Task DownloadOsm_ReportsProgress()
    {
        var content = new byte[3 * 1024 * 1024]; // 3 MB
        Random.Shared.NextBytes(content);

        var handler = new MockHttpHandler(content, HttpStatusCode.OK);
        var svc = CreateService(handler);

        var tempFile = Path.Combine(Path.GetTempPath(), $"osm_test_{Guid.NewGuid()}.pbf");
        var progressReports = new List<DownloadProgress>();
        var progress = new SyncProgress<DownloadProgress>(p => progressReports.Add(p));

        try
        {
            await svc.DownloadOsmExtractAsync(
                new OsmDownloadRequest("https://example.com/test.osm.pbf", tempFile),
                progress);

            Assert.True(File.Exists(tempFile));
            Assert.Equal(content.Length, new FileInfo(tempFile).Length);
            // Should have reported progress (at least the final report)
            Assert.NotEmpty(progressReports);
            Assert.Equal(1, progressReports[^1].FilesCompleted);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task DownloadOsm_Cancelled_DeletesTempFile()
    {
        // 10 MB content to ensure cancellation happens mid-stream
        var content = new byte[10 * 1024 * 1024];
        var handler = new SlowMockHttpHandler(content, HttpStatusCode.OK);
        var svc = CreateService(handler);

        var tempFile = Path.Combine(Path.GetTempPath(), $"osm_cancel_{Guid.NewGuid()}.pbf");
        var cts = new CancellationTokenSource();

        // Cancel after first progress report
        var progress = new Progress<DownloadProgress>(_ => cts.Cancel());

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                svc.DownloadOsmExtractAsync(
                    new OsmDownloadRequest("https://example.com/test.osm.pbf", tempFile),
                    progress,
                    cts.Token));

            // Temp file should be cleaned up
            Assert.False(File.Exists(tempFile));
            Assert.False(File.Exists(tempFile + ".tmp"));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(tempFile + ".tmp")) File.Delete(tempFile + ".tmp");
        }
    }

    [Fact]
    public async Task DownloadSrtm_MissingTile_SkipsGracefully()
    {
        var handler = new MockHttpHandler([], HttpStatusCode.NotFound);
        var svc = CreateService(handler);

        var tempDir = Path.Combine(Path.GetTempPath(), $"srtm_404_{Guid.NewGuid()}");
        var progressReports = new List<DownloadProgress>();
        var progress = new SyncProgress<DownloadProgress>(p => progressReports.Add(p));

        try
        {
            // Request a single tile that returns 404
            await svc.DownloadSrtmTilesAsync(
                new SrtmDownloadRequest(
                    NorthLat: 52.5, SouthLat: 52.1,
                    EastLon: 4.5, WestLon: 4.1,
                    TargetDirectory: tempDir),
                progress);

            // Should not throw — 404 is treated as ocean tile
            Assert.NotEmpty(progressReports);
            Assert.Equal(1, progressReports[^1].FilesCompleted);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // --- Test helpers ---

    private static DataDownloadService CreateService(HttpMessageHandler? handler = null)
    {
        var client = handler != null
            ? new HttpClient(handler)
            : new HttpClient();
        return new DataDownloadService(client);
    }

    /// <summary>
    /// Mock handler that returns content immediately with the given status code.
    /// </summary>
    private sealed class MockHttpHandler(byte[] content, HttpStatusCode statusCode)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new ByteArrayContent(content)
            };
            response.Content.Headers.ContentLength = content.Length;
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Mock handler that returns content through a slow stream (supports cancellation testing).
    /// </summary>
    private sealed class SlowMockHttpHandler(byte[] content, HttpStatusCode statusCode)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var stream = new SlowStream(content, ct);
            var httpContent = new StreamContent(stream);
            httpContent.Headers.ContentLength = content.Length;
            var response = new HttpResponseMessage(statusCode) { Content = httpContent };
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// A stream that yields data in small chunks and respects cancellation.
    /// </summary>
    private sealed class SlowStream(byte[] data, CancellationToken ct) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ct.ThrowIfCancellationRequested();
            int toRead = Math.Min(count, Math.Min(1024, data.Length - _position));
            if (toRead <= 0) return 0;
            Array.Copy(data, _position, buffer, offset, toRead);
            _position += toRead;
            return toRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// IProgress that invokes the callback synchronously (no SynchronizationContext marshalling).
    /// </summary>
    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
