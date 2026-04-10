using Oravey2.MapGen.App.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class PipelineWizardView : ContentPage
{
    private readonly PipelineWizardViewModel _viewModel;
    private readonly IServiceProvider _services;

    public PipelineWizardView(PipelineWizardViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _services = services;
        _viewModel.ContentRoot = FindContentRoot();
        BindingContext = viewModel;

        RegisterStepViewFactories();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeDefaultAsync();
    }

    private void RegisterStepViewFactories()
    {
        _viewModel.RegisterStepViewFactory(1, () =>
            new RegionStepView(_viewModel.RegionStepVM, _services));
        _viewModel.RegisterStepViewFactory(2, () =>
        {
            _viewModel.DownloadStepVM.CheckExistingFiles();
            return new DownloadStepView(_viewModel.DownloadStepVM);
        });
        _viewModel.RegisterStepViewFactory(3, () =>
            new ParseStepView(_viewModel.ParseStepVM));
        _viewModel.RegisterStepViewFactory(4, () =>
        {
            _viewModel.TownSelectionStepVM.SetParsedTemplate(_viewModel.ParseStepVM.ParsedTemplate);
            return new TownSelectionStepView(_viewModel.TownSelectionStepVM);
        });
        _viewModel.RegisterStepViewFactory(5, () =>
            new TownDesignStepView(_viewModel.TownDesignStepVM));
        _viewModel.RegisterStepViewFactory(6, () => new TownMapsStepView());
        _viewModel.RegisterStepViewFactory(7, () => new AssetsStepView());
        _viewModel.RegisterStepViewFactory(8, () => new AssemblyStepView());
    }

    private static string FindContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "content");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "content");
    }
}
