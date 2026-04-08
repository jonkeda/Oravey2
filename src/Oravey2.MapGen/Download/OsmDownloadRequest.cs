namespace Oravey2.MapGen.Download;

public record OsmDownloadRequest(
    string DownloadUrl,
    string TargetFilePath);
