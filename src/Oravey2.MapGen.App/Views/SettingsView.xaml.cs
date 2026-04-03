using Oravey2.MapGen.App.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class SettingsView : ContentPage
{
    public SettingsView()
    {
        InitializeComponent();
        BindingContext = new SettingsViewModel();
    }
}
