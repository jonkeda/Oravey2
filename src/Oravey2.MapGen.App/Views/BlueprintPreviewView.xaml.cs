using Oravey2.MapGen.App.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class BlueprintPreviewView : ContentPage
{
    public BlueprintPreviewView()
    {
        InitializeComponent();
        BindingContext = App.Current?.Handler?.MauiContext?.Services.GetService<BlueprintPreviewViewModel>();
    }
}
