using System.Windows.Input;
using Oravey2.MapGen.Models.Meshy;
using Oravey2.MapGen.Services;

namespace Oravey2.MapGen.App.ViewModels;

public sealed class HouseGeneratorViewModel : AppBaseViewModel
{
    private readonly MeshyClient _meshyClient;
    private CancellationTokenSource? _cts;

    // --- Input ---
    private string _prompt = string.Empty;
    public string Prompt { get => _prompt; set => SetProperty(ref _prompt, value); }

    private string _imageUrl = string.Empty;
    public string ImageUrl { get => _imageUrl; set => SetProperty(ref _imageUrl, value); }

    private string _artStyle = "realistic";
    public string ArtStyle { get => _artStyle; set => SetProperty(ref _artStyle, value); }

    private bool _shouldRemesh;
    public bool ShouldRemesh { get => _shouldRemesh; set => SetProperty(ref _shouldRemesh, value); }

    private int _targetPolycount = 10000;
    public int TargetPolycount { get => _targetPolycount; set => SetProperty(ref _targetPolycount, value); }

    // --- Output ---
    private bool _isGenerating;
    public bool IsGenerating { get => _isGenerating; private set => SetProperty(ref _isGenerating, value); }

    private string _progress = string.Empty;
    public string Progress { get => _progress; private set => SetProperty(ref _progress, value); }

    private string _streamingLog = string.Empty;
    public string StreamingLog { get => _streamingLog; private set => SetProperty(ref _streamingLog, value); }

    private string? _previewThumbnail;
    public string? PreviewThumbnail { get => _previewThumbnail; private set => SetProperty(ref _previewThumbnail, value); }

    private Dictionary<string, string>? _downloadUrls;
    public Dictionary<string, string>? DownloadUrls { get => _downloadUrls; private set => SetProperty(ref _downloadUrls, value); }

    private string _statusMessage = "Ready";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    // --- Commands ---
    public Command GenerateCommand { get; }
    public Command CancelCommand { get; }
    public Command DownloadGlbCommand { get; }
    public Command DownloadFbxCommand { get; }

    public HouseGeneratorViewModel(MeshyClient meshyClient)
    {
        _meshyClient = meshyClient;
        _meshyClient.OnProgress += OnMeshyProgress;

        GenerateCommand = new Command(async () => await GenerateAsync(), () => !IsGenerating);
        CancelCommand = new Command(Cancel, () => IsGenerating);
        DownloadGlbCommand = new Command(async () => await DownloadAsync("glb"), () => DownloadUrls?.ContainsKey("glb") == true);
        DownloadFbxCommand = new Command(async () => await DownloadAsync("fbx"), () => DownloadUrls?.ContainsKey("fbx") == true);
    }

    private async Task GenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(Prompt))
        {
            StatusMessage = "Please enter a prompt.";
            return;
        }

        IsGenerating = true;
        StreamingLog = string.Empty;
        Progress = string.Empty;
        DownloadUrls = null;
        PreviewThumbnail = null;
        StatusMessage = "Generating...";
        _cts = new CancellationTokenSource();

        try
        {
            string taskId;

            // Step 1: Create task (image-to-3D or text-to-3D)
            if (!string.IsNullOrWhiteSpace(ImageUrl))
            {
                var req = new ImageTo3DRequest(ImageUrl, Prompt, ArtStyle);
                var resp = await _meshyClient.CreateImageTo3DAsync(req, _cts.Token);
                taskId = resp.Result;
                AppendLog($"Image-to-3D task created: {taskId}");
            }
            else
            {
                var req = new TextTo3DRequest("refine", Prompt, ArtStyle);
                var resp = await _meshyClient.CreateTextTo3DAsync(req, _cts.Token);
                taskId = resp.Result;
                AppendLog($"Text-to-3D task created: {taskId}");
            }

            // Step 2: Stream progress
            var streamPath = !string.IsNullOrWhiteSpace(ImageUrl) ? "/v1/image-to-3d" : "/v2/text-to-3d";
            MeshyTaskStatus? finalStatus = null;

            await foreach (var status in _meshyClient.StreamTaskAsync(streamPath, taskId, _cts.Token))
            {
                Progress = $"{status.Status}: {status.Progress}%";
                PreviewThumbnail = status.ThumbnailUrl;
                AppendLog($"Progress: {status.Status} {status.Progress}%");
                finalStatus = status;
            }

            if (finalStatus?.Status == "FAILED")
            {
                StatusMessage = $"Failed: {finalStatus.TaskError ?? "Unknown error"}";
                return;
            }

            // Step 3: Optional remesh
            if (ShouldRemesh && finalStatus?.Status == "SUCCEEDED")
            {
                AppendLog("Starting remesh...");
                var remeshReq = new RemeshRequest(taskId, ["glb", "fbx"], TargetPolycount: TargetPolycount);
                var remeshResp = await _meshyClient.CreateRemeshAsync(remeshReq, _cts.Token);
                var remeshTaskId = remeshResp.Result;
                AppendLog($"Remesh task created: {remeshTaskId}");

                await foreach (var status in _meshyClient.StreamRemeshAsync(remeshTaskId, _cts.Token))
                {
                    Progress = $"Remesh: {status.Status} {status.Progress}%";
                    AppendLog($"Remesh: {status.Status} {status.Progress}%");
                    finalStatus = status;
                }
            }

            // Step 4: Set download URLs
            if (finalStatus?.ModelUrls is not null)
            {
                DownloadUrls = finalStatus.ModelUrls;
                StatusMessage = "Complete — ready to download.";
                AppendLog("Generation complete.");
            }
            else
            {
                StatusMessage = "Complete but no model URLs returned.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled";
            AppendLog("Operation cancelled.");
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"Network error: {ex.Message}";
            AppendLog($"Error: {ex.Message}");
        }
        finally
        {
            IsGenerating = false;
            _cts?.Dispose();
            _cts = null;
            RefreshCommands();
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }

    private async Task DownloadAsync(string format)
    {
        if (DownloadUrls is null || !DownloadUrls.TryGetValue(format, out var url)) return;

        try
        {
            IsBusy = true;
            StatusMessage = $"Downloading {format.ToUpperInvariant()}...";

            var bytes = await _meshyClient.DownloadModelAsync(url);

            var exportPath = Preferences.Get("MeshyExportPath",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oravey2", "Models"));
            Directory.CreateDirectory(exportPath);

            var safeName = string.Join("_", Prompt.Split(Path.GetInvalidFileNameChars()));
            if (safeName.Length > 50) safeName = safeName[..50];
            var filePath = Path.Combine(exportPath, $"{safeName}.{format}");

            await File.WriteAllBytesAsync(filePath, bytes);
            StatusMessage = $"Saved to {filePath}";
            AppendLog($"Downloaded: {filePath}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AppendLog(string message)
    {
        StreamingLog += $"{message}\n";
    }

    private void OnMeshyProgress(MeshyProgress progress)
    {
        Progress = progress.Message;
    }

    private void RefreshCommands()
    {
        GenerateCommand.ChangeCanExecute();
        CancelCommand.ChangeCanExecute();
        DownloadGlbCommand.ChangeCanExecute();
        DownloadFbxCommand.ChangeCanExecute();
    }
}
