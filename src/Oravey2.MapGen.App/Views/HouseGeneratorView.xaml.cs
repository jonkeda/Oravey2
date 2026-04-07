using Oravey2.MapGen.App.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class HouseGeneratorView : ContentPage
{
    public HouseGeneratorView()
    {
        InitializeComponent();
        BindingContext = App.Current?.Handler?.MauiContext?.Services.GetService<HouseGeneratorViewModel>();
    }
}
