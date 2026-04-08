using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Oravey2.MapGen.Download;

public class DataDownloadService : IDataDownloadService
{
    private const string SrtmBaseUrl = "https://e4ftl01.cr.usgs.gov/MEASURES/SRTMGL1.003/2000.02.11";
    private const string EarthdataTokenUrl = "https://urs.earthdata.nasa.gov/api/users/token";
    private const string UserAgent = "Oravey2.MapGen/1.0";
    private const int BufferSize = 65_536; // 64 KB
    private const long ProgressIntervalBytes = 1_048_576; // 1 MB

    private readonly HttpClient _httpClient;

    public DataDownloadService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public List<string> GetRequiredSrtmTileNames(
        double northLat, double southLat,
        double eastLon, double westLon)
    {
        var tiles = new List<string>();

        int minLat = (int)Math.Floor(southLat);
        int maxLat = (int)Math.Floor(northLat);
        int minLon = (int)Math.Floor(westLon);
        int maxLon = (int)Math.Floor(eastLon);

        for (int lat = minLat; lat <= maxLat; lat++)
        {
            for (int lon = minLon; lon <= maxLon; lon++)
            {
                tiles.Add(FormatTileName(lat, lon));
            }
        }

        return tiles;
    }

    public List<string> GetExistingSrtmTiles(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        return Directory.GetFiles(directory, "*.hgt")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToList();
    }

    public async Task DownloadSrtmTilesAsync(
        SrtmDownloadRequest request,
        IProgress<DownloadProgress> progress,
        CancellationToken ct = default)
    {
        var required = GetRequiredSrtmTileNames(
            request.NorthLat, request.SouthLat,
            request.EastLon, request.WestLon);
        var existing = GetExistingSrtmTiles(request.TargetDirectory);
        var missing = required.Where(t => !existing.Contains(t)).ToList();

        if (missing.Count == 0) return;

        Directory.CreateDirectory(request.TargetDirectory);

        string? bearerToken = null;
        if (request.EarthdataUsername != null && request.EarthdataPassword != null)
        {
            bearerToken = await GetEarthdataTokenAsync(
                request.EarthdataUsername, request.EarthdataPassword, ct);
        }

        int completed = existing.Count;
        int total = required.Count;

        foreach (var tile in missing)
        {
            ct.ThrowIfCancellationRequested();

            var url = $"{SrtmBaseUrl}/{tile}.SRTMGL1.hgt.zip";
            var tempZip = Path.Combine(request.TargetDirectory, $"{tile}.hgt.zip.tmp");

            try
            {
                using var requestMsg = new HttpRequestMessage(HttpMethod.Get, url);
                if (bearerToken != null)
                    requestMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

                using var response = await _httpClient.SendAsync(
                    requestMsg, HttpCompletionOption.ResponseHeadersRead, ct);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // Ocean tile — no data available, skip gracefully
                    completed++;
                    progress.Report(new DownloadProgress(tile, 0, 0, completed, total));
                    continue;
                }

                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[BufferSize];
                long bytesRead = 0;
                int read;
                long lastReport = 0;

                while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    bytesRead += read;

                    if (bytesRead - lastReport >= ProgressIntervalBytes)
                    {
                        progress.Report(new DownloadProgress(tile, bytesRead, totalBytes, completed, total));
                        lastReport = bytesRead;
                    }
                }

                fileStream.Close();

                // Extract .hgt from zip
                ExtractHgtFromZip(tempZip, request.TargetDirectory, tile);

                completed++;
                progress.Report(new DownloadProgress(tile, totalBytes, totalBytes, completed, total));
            }
            finally
            {
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
            }
        }
    }

    public async Task DownloadOsmExtractAsync(
        OsmDownloadRequest request,
        IProgress<DownloadProgress> progress,
        CancellationToken ct = default)
    {
        var targetDir = Path.GetDirectoryName(request.TargetFilePath);
        if (targetDir != null)
            Directory.CreateDirectory(targetDir);

        var tempFile = request.TargetFilePath + ".tmp";
        var fileName = Path.GetFileName(request.TargetFilePath);

        try
        {
            using var response = await _httpClient.GetAsync(
                request.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[BufferSize];
            long bytesRead = 0;
            int read;
            long lastReport = 0;

            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                bytesRead += read;

                if (bytesRead - lastReport >= ProgressIntervalBytes)
                {
                    progress.Report(new DownloadProgress(fileName, bytesRead, totalBytes, 0, 1));
                    lastReport = bytesRead;
                }
            }

            fileStream.Close();

            // Atomic replace
            if (File.Exists(request.TargetFilePath))
                File.Delete(request.TargetFilePath);
            File.Move(tempFile, request.TargetFilePath);

            progress.Report(new DownloadProgress(fileName, totalBytes, totalBytes, 1, 1));
        }
        catch (OperationCanceledException)
        {
            // Cleanup temp file on cancellation
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            throw;
        }
        catch
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            throw;
        }
    }

    // --- Private helpers ---

    internal static string FormatTileName(int lat, int lon)
    {
        char latPrefix = lat >= 0 ? 'N' : 'S';
        char lonPrefix = lon >= 0 ? 'E' : 'W';
        return $"{latPrefix}{Math.Abs(lat):D2}{lonPrefix}{Math.Abs(lon):D3}";
    }

    private async Task<string> GetEarthdataTokenAsync(
        string username, string password, CancellationToken ct)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, EarthdataTokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString()
               ?? throw new InvalidOperationException("No access_token in Earthdata response");
    }

    private static void ExtractHgtFromZip(string zipPath, string targetDir, string tileName)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase));

        if (entry == null)
            throw new InvalidOperationException($"No .hgt file found in {zipPath}");

        var targetPath = Path.Combine(targetDir, $"{tileName}.hgt");
        entry.ExtractToFile(targetPath, overwrite: true);
    }
}
