using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Oravey2.MapGen.App.ViewModels;
using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Services;
using Oravey2.MapGen.Validation;

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
        builder.Services.AddSingleton<IBlueprintValidator, TerrainBlueprintValidator>();
        builder.Services.AddSingleton<MapGeneratorService>();

        // ViewModels
        builder.Services.AddTransient<GeneratorViewModel>();
        builder.Services.AddTransient<BlueprintPreviewViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
