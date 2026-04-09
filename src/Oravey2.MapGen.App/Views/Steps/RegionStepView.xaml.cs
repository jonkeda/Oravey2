using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class RegionStepView : ContentView
{
    private readonly RegionStepViewModel _viewModel;
    private readonly IServiceProvider _services;

    public RegionStepView(RegionStepViewModel viewModel, IServiceProvider services)
    {
        _viewModel = viewModel;
        _services = services;
        BindingContext = viewModel;
        InitializeComponent();
    }

    private async void OnSelectRegionClicked(object? sender, EventArgs e)
    {
        var pickerVM = _services.GetRequiredService<RegionPickerViewModel>();
        var dialog = new RegionPickerDialog(pickerVM);

        pickerVM.RegionSelected += preset =>
        {
            _viewModel.ApplyRegion(preset);
        };

        if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page)
        {
            await page.Navigation.PushModalAsync(new NavigationPage(dialog));
        }
    }
}
