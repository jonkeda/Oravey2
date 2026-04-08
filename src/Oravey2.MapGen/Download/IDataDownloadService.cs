namespace Oravey2.MapGen.Download;

public interface IDataDownloadService
{
    Task DownloadSrtmTilesAsync(
        SrtmDownloadRequest request,
        IProgress<DownloadProgress> progress,
        CancellationToken ct = default);

    Task DownloadOsmExtractAsync(
        OsmDownloadRequest request,
        IProgress<DownloadProgress> progress,
        CancellationToken ct = default);

    List<string> GetRequiredSrtmTileNames(
        double northLat, double southLat,
        double eastLon, double westLon);

    List<string> GetExistingSrtmTiles(string directory);
}
