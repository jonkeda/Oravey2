namespace Oravey2.MapGen.Download;

public record SrtmDownloadRequest(
    double NorthLat,
    double SouthLat,
    double EastLon,
    double WestLon,
    string TargetDirectory,
    string? EarthdataUsername = null,
    string? EarthdataPassword = null);
