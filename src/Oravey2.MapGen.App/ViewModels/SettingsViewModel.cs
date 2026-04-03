using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace Oravey2.MapGen.App.ViewModels;

public sealed class SettingsViewModel : BaseViewModel
{
    private string _selectedModel = "gpt-4.1";
    public string SelectedModel { get => _selectedModel; set => SetProperty(ref _selectedModel, value); }

    public ObservableCollection<string> AvailableModels { get; } = new()
    {
        "gpt-4.1",
        "o4-mini",
        "claude-haiku-4.5",
        "claude-sonnet-4.6",
        "claude-opus-4.6"
    };

    private bool _useBYOK;
    public bool UseBYOK { get => _useBYOK; set => SetProperty(ref _useBYOK, value); }

    private string? _providerType;
    public string? ProviderType { get => _providerType; set => SetProperty(ref _providerType, value); }

    private string? _baseUrl;
    public string? BaseUrl { get => _baseUrl; set => SetProperty(ref _baseUrl, value); }

    private string? _apiKey;
    public string? ApiKey { get => _apiKey; set => SetProperty(ref _apiKey, value); }

    private string? _cliPath;
    public string? CliPath { get => _cliPath; set => SetProperty(ref _cliPath, value); }

    private string _exportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oravey2", "Blueprints");
    public string ExportPath { get => _exportPath; set => SetProperty(ref _exportPath, value); }

    private string? _statusMessage;
    public string? StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private bool _showInstallCopilot;
    public bool ShowInstallCopilot { get => _showInstallCopilot; set => SetProperty(ref _showInstallCopilot, value); }

    public ICommand SaveSettingsCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand InstallCopilotCommand { get; }
    public ICommand LoginCopilotCommand { get; }
    public ICommand BrowseExportPathCommand { get; }

    public SettingsViewModel()
    {
        SaveSettingsCommand = new Command(SaveSettings);
        TestConnectionCommand = new Command(async () => await TestConnectionAsync());
        InstallCopilotCommand = new Command(InstallCopilot);
        LoginCopilotCommand = new Command(LoginCopilot);
        BrowseExportPathCommand = new Command(async () => await BrowseExportPathAsync());
        LoadSettings();
    }

    private void SaveSettings()
    {
        Preferences.Set("SelectedModel", SelectedModel);
        Preferences.Set("UseBYOK", UseBYOK);
        Preferences.Set("ProviderType", ProviderType ?? string.Empty);
        Preferences.Set("BaseUrl", BaseUrl ?? string.Empty);
        Preferences.Set("CliPath", CliPath ?? string.Empty);
        Preferences.Set("ExportPath", ExportPath);
        // API key stored securely
        if (ApiKey is not null)
            SecureStorage.SetAsync("ApiKey", ApiKey).ConfigureAwait(false);

        StatusMessage = "Settings saved.";
    }

    private void LoadSettings()
    {
        SelectedModel = Preferences.Get("SelectedModel", "gpt-4.1");
        UseBYOK = Preferences.Get("UseBYOK", false);
        ProviderType = Preferences.Get("ProviderType", string.Empty);
        BaseUrl = Preferences.Get("BaseUrl", string.Empty);
        CliPath = Preferences.Get("CliPath", string.Empty);
        ExportPath = Preferences.Get("ExportPath",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oravey2", "Blueprints"));
    }

    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = "Testing connection...";
        ShowInstallCopilot = false;
        try
        {
            var cliName = string.IsNullOrWhiteSpace(CliPath) ? "copilot" : CliPath;
            var psi = new ProcessStartInfo
            {
                FileName = cliName,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                StatusMessage = "Copilot CLI not found. Install it with winget.";
                ShowInstallCopilot = true;
                return;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                StatusMessage = $"Copilot CLI found: {output.Trim()}";
                ShowInstallCopilot = false;
            }
            else
            {
                StatusMessage = "Copilot CLI returned an error. Reinstall may be needed.";
                ShowInstallCopilot = true;
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            StatusMessage = "Copilot CLI not found on PATH. Install it or set the CLI path above.";
            ShowInstallCopilot = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection test failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void InstallCopilot()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-NoExit -Command \"winget install GitHub.Copilot\"",
            UseShellExecute = true
        };
        Process.Start(psi);
        StatusMessage = "PowerShell launched — installing GitHub Copilot...";
    }
    private async Task BrowseExportPathAsync()
    {
        try
        {
#if WINDOWS
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            // Initialize the picker with the window handle
            var hwnd = ((MauiWinUIWindow)Application.Current!.Windows[0].Handler!.PlatformView!).WindowHandle;
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                ExportPath = folder.Path;
                StatusMessage = $"Export path set to {ExportPath}";
            }
#endif
        }
        catch (Exception ex)
        {
            StatusMessage = $"Browse failed: {ex.Message}";
        }
    }

    private void LoginCopilot()
    {
        var cliName = string.IsNullOrWhiteSpace(CliPath) ? "copilot" : CliPath;
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoExit -Command \"{cliName} login\"",
            UseShellExecute = true
        };
        Process.Start(psi);
        StatusMessage = "PowerShell launched \u2014 complete the login in the browser to select your GitHub account.";
    }}
