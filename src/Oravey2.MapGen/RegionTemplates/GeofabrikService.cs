using System.IO.Compression;

namespace Oravey2.MapGen.RegionTemplates;

public class GeofabrikService : IGeofabrikService
{
    private static readonly TimeSpan StaleDuration = TimeSpan.FromDays(7);
    private const string IndexUrl = "https://download.geofabrik.de/index-v1.json";
    private const string CacheFileName = "geofabrik-index-v1.json.gz";

    private readonly HttpClient _http;
    private readonly string _cacheDir;
    private GeofabrikIndex? _cached;

    public GeofabrikService(HttpClient http, string? cacheDir = null)
    {
        _http = http;
        _cacheDir = cacheDir ?? Path.Combine("data", "cache");
    }

    public async Task<GeofabrikIndex> GetIndexAsync(bool forceRefresh = false)
    {
        if (_cached is not null && !forceRefresh)
            return _cached;

        var cachePath = Path.Combine(_cacheDir, CacheFileName);
        bool cacheExists = File.Exists(cachePath);
        bool isStale = !cacheExists || (DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath)) > StaleDuration;

        if (forceRefresh || isStale)
        {
            try
            {
                Directory.CreateDirectory(_cacheDir);
                var json = await _http.GetStringAsync(IndexUrl);

                await using (var output = File.Create(cachePath))
                await using (var gz = new GZipStream(output, CompressionLevel.Optimal))
                await using (var writer = new StreamWriter(gz))
                    await writer.WriteAsync(json);

                _cached = GeofabrikIndex.Parse(json);
                return _cached;
            }
            catch when (cacheExists)
            {
                // Fall back to stale cache on network error
            }
        }

        if (cacheExists)
        {
            string json;
            await using (var input = File.OpenRead(cachePath))
            await using (var gz = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(gz))
                json = await reader.ReadToEndAsync();

            _cached = GeofabrikIndex.Parse(json);
            return _cached;
        }

        throw new InvalidOperationException(
            "Geofabrik index is not cached and could not be downloaded.");
    }
}
