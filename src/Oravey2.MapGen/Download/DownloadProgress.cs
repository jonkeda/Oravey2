namespace Oravey2.MapGen.Download;

public record DownloadProgress(
    string FileName,
    long BytesDownloaded,
    long TotalBytes,
    int FilesCompleted,
    int TotalFiles);
