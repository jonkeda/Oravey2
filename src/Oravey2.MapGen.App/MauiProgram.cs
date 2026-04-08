using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Oravey2.MapGen.App.ViewModels;
using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Download;
using Oravey2.MapGen.Services;
using Oravey2.MapGen.ViewModels;
using Oravey2.MapGen.WorldTemplate;

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
        builder.Services.AddSingleton<IAssetRegistry, AssetRegistry>();
        builder.Services.AddSingleton<MapGeneratorService>();
        builder.Services.AddSingleton<HttpClient>();
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

        // ViewModels
        builder.Services.AddTransient<WorldTemplateViewModel>();
        builder.Services.AddTransient<GeneratorViewModel>();
        builder.Services.AddTransient<BlueprintPreviewViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<HouseGeneratorViewModel>();
        builder.Services.AddTransient<FigureGeneratorViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
