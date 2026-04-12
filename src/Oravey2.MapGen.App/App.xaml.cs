using Oravey2.MapGen.App.Pages;
using Oravey2.MapGen.App.ViewModels;
using Oravey2.MapGen.App.Views;

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
        Page page;

        // When running under UI test automation, skip the TabbedPage wrapper.
        // FlaUI cannot traverse TabbedPage content on WinUI3.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MAPGEN_AUTO_LOAD_REGION")))
        {
            var vm = _services.GetRequiredService<PipelineWizardViewModel>();
            page = new PipelineWizardView(vm, _services) { Title = "Pipeline v3" };
        }
        else
        {
            page = _services.GetRequiredService<MainPage>();
        }

        return new Window(page)
        {
            Title = "Oravey2 Map Generator",
            Width = 900,
            Height = 700
        };
    }
}
