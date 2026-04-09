using Oravey2.MapGen.ViewModels;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.App.Views;

public partial class AutoCullDialog : ContentPage
{
    private AutoCullDialogViewModel Vm => (AutoCullDialogViewModel)BindingContext;

    public AutoCullDialog(AutoCullDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async () => await Navigation.PopModalAsync();
    }

    private async void OnLoad(object? sender, EventArgs e)
    {
        var options = new PickOptions
        {
            PickerTitle = "Load Cull Settings",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, [".cullsettings", ".json"] }
            })
        };
        var result = await FilePicker.Default.PickAsync(options);
        if (result?.FullPath is string path)
        {
            var settings = CullSettings.Load(path);
            Vm.LoadFrom(settings);
        }
    }

    private async void OnSave(object? sender, EventArgs e)
    {
        var settings = Vm.BuildSettings();
        var options = new PickOptions
        {
            PickerTitle = "Save Cull Settings",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, [".cullsettings", ".json"] }
            })
        };
        var result = await FilePicker.Default.PickAsync(options);
        if (result?.FullPath is string path)
            settings.Save(path);
    }
}
