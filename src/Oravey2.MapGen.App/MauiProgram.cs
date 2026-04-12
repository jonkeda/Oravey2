using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Oravey2.MapGen.App.Services;
using Oravey2.MapGen.App.ViewModels;
using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Download;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.Services;
using Oravey2.MapGen.ViewModels;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
               .UseMauiCommunityToolkit()
               .ConfigureFonts(fonts =>
               {
                   fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                   fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
               });

        // Services
        builder.Services.AddSingleton<ISettingsService, MauiSettingsService>();
        builder.Services.AddSingleton<IAssetRegistry, AssetRegistry>();
        builder.Services.AddSingleton<MapGeneratorService>();
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<CopilotLlmService>();
        builder.Services.AddSingleton<IDataDownloadService, DataDownloadService>();
        builder.Services.AddSingleton<IGeofabrikService>(sp =>
            new GeofabrikService(
                sp.GetRequiredService<HttpClient>(),
                Path.Combine(FileSystem.AppDataDirectory, "cache")));
        builder.Services.AddSingleton<MeshyClient>(sp =>
        {
            var apiKey = SecureStorage.Default.GetAsync("MeshyApiKey").Result ?? "";
            return new MeshyClient(apiKey);
        });
        builder.Services.AddSingleton(sp =>
        {
            var dataRoot = Environment.GetEnvironmentVariable("MAPGEN_DATA_ROOT")
                ?? Preferences.Get("DataRoot", string.Empty);
            if (string.IsNullOrWhiteSpace(dataRoot))
                dataRoot = Path.Combine(FileSystem.AppDataDirectory, "data");
            return new PipelineStateService(dataRoot);
        });

        // ViewModels
        builder.Services.AddTransient<RegionStepViewModel>();
        builder.Services.AddTransient<DownloadStepViewModel>();
        builder.Services.AddTransient<ParseStepViewModel>();
        builder.Services.AddTransient<TownSelectionStepViewModel>();
        builder.Services.AddTransient<TownDesignStepViewModel>();
        builder.Services.AddTransient<TownMapsStepViewModel>();
        builder.Services.AddTransient<AssetsStepViewModel>();
        builder.Services.AddTransient<AssemblyStepViewModel>();
        builder.Services.AddTransient<PipelineWizardViewModel>();
        builder.Services.AddTransient<RegionPickerViewModel>();
        builder.Services.AddTransient<RegionTemplateViewModel>();
        builder.Services.AddTransient<GeneratorViewModel>();
        builder.Services.AddTransient<BlueprintPreviewViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<HouseGeneratorViewModel>();
        builder.Services.AddTransient<FigureGeneratorViewModel>();

        // Pages
        builder.Services.AddTransient<Oravey2.MapGen.App.Pages.MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
