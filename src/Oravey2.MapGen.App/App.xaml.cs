using Oravey2.MapGen.App.Pages;

namespace Oravey2.MapGen.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_services.GetRequiredService<MainPage>())
        {
            Title = "Oravey2 Map Generator",
            Width = 900,
            Height = 700
        };
    }
}
