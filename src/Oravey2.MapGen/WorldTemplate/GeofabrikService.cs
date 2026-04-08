namespace Oravey2.MapGen.WorldTemplate;

public class GeofabrikService : IGeofabrikService
{
    private static readonly TimeSpan StaleDuration = TimeSpan.FromDays(7);
    private const string IndexUrl = "https://download.geofabrik.de/index-v1.json";
    private const string CacheFileName = "geofabrik-index-v1.json";

    private readonly HttpClient _http;
    private readonly string _cacheDir;
    private GeofabrikIndex? _cached;

    public GeofabrikService(HttpClient http, string cacheDir)
    {
        _http = http;
        _cacheDir = cacheDir;
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
                await File.WriteAllTextAsync(cachePath, json);
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
            var json = await File.ReadAllTextAsync(cachePath);
            _cached = GeofabrikIndex.Parse(json);
            return _cached;
        }

        throw new InvalidOperationException(
            "Geofabrik index is not cached and could not be downloaded.");
    }
}
